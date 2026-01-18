using ConferenceHub.Models;
using ConferenceHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace ConferenceHub.Controllers
{
    public class SessionsController : Controller
    {
        private readonly IDataService _dataService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuditLogService _auditLogService;
        private readonly AzureFunctionsConfig _functionsConfig;
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(
            IDataService dataService,
            IHttpClientFactory httpClientFactory,
            IAuditLogService auditLogService,
            IOptions<AzureFunctionsConfig> functionsConfig,
            ILogger<SessionsController> logger)
        {
            _dataService = dataService;
            _httpClientFactory = httpClientFactory;
            _auditLogService = auditLogService;
            _functionsConfig = functionsConfig.Value;
            _logger = logger;
        }

        // GET: Sessions
        public async Task<IActionResult> Index()
        {
            var sessions = await _dataService.GetSessionsAsync();
            return View(sessions);
        }

        // GET: Sessions/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var session = await _dataService.GetSessionByIdAsync(id);
            if (session == null)
            {
                return NotFound();
            }
            return View(session);
        }

        // POST: Sessions/Register
        [HttpPost]
        public async Task<IActionResult> Register(int sessionId, string attendeeName, string attendeeEmail)
        {
            var session = await _dataService.GetSessionByIdAsync(sessionId);
            if (session == null)
            {
                return NotFound();
            }

            if (session.CurrentRegistrations >= session.Capacity)
            {
                TempData["Error"] = "This session is at full capacity.";
                return RedirectToAction(nameof(Details), new { id = sessionId });
            }

            var registration = new Registration
            {
                SessionId = sessionId,
                AttendeeName = attendeeName,
                AttendeeEmail = attendeeEmail
            };

            await _dataService.AddRegistrationAsync(registration);

            // Log to audit table
            await _auditLogService.LogRegistrationAsync(sessionId, session.Title, attendeeName, attendeeEmail);

            // Call Azure Function to send confirmation email
            await SendConfirmationEmailAsync(session, attendeeName, attendeeEmail);

            TempData["Success"] = "Successfully registered for the session!";
            
            return RedirectToAction(nameof(Details), new { id = sessionId });
        }

        private async Task SendConfirmationEmailAsync(Session session, string attendeeName, string attendeeEmail)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                
                var registrationRequest = new
                {
                    sessionId = session.Id,
                    sessionTitle = session.Title,
                    attendeeName = attendeeName,
                    attendeeEmail = attendeeEmail,
                    sessionStartTime = session.StartTime,
                    room = session.Room
                };

                var json = JsonSerializer.Serialize(registrationRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = _functionsConfig.SendConfirmationUrl;
                if (!string.IsNullOrEmpty(_functionsConfig.FunctionKey))
                {
                    url += $"?code={_functionsConfig.FunctionKey}";
                }

                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Confirmation email sent successfully for {Email}", attendeeEmail);
                }
                else
                {
                    _logger.LogWarning("Failed to send confirmation email. Status: {Status}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling SendConfirmation function");
            }
        }
    }
}