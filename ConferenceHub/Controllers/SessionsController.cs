using ConferenceHub.Models;
using ConferenceHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace ConferenceHub.Controllers
{
    [Authorize]
    public class SessionsController : Controller
    {
        private readonly IDataService _dataService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuditLogService _auditLogService;
        private readonly IFeatureManager _featureManager;
        private readonly AzureFunctionsConfig _functionsConfig;
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(
            IDataService dataService,
            IHttpClientFactory httpClientFactory,
            IAuditLogService auditLogService,
            IFeatureManager featureManager,
            IOptions<AzureFunctionsConfig> functionsConfig,
            ILogger<SessionsController> logger)
        {
            _dataService = dataService;
            _httpClientFactory = httpClientFactory;
            _auditLogService = auditLogService;
            _featureManager = featureManager;
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
        public async Task<IActionResult> Details(string id)
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
        [Authorize]
        public async Task<IActionResult> Register(string sessionId, string attendeeName, string attendeeEmail)
        {
            var userEmail = GetCurrentUserEmail() ?? attendeeEmail;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? attendeeName;

            var session = await _dataService.GetSessionByIdAsync(sessionId);
            if (session == null)
            {
                return NotFound();
            }

            // Check if session is full
            if (session.CurrentRegistrations >= session.Capacity)
            {
                // Check if waitlist is enabled
                var waitlistEnabled = await _featureManager.IsEnabledAsync("Waitlist");
                
                if (waitlistEnabled)
                {
                    TempData["Info"] = "This session is full. You have been added to the waitlist.";
                    // TODO: Implement waitlist logic
                }
                else
                {
                    TempData["Error"] = "This session is at full capacity.";
                }
                
                return RedirectToAction(nameof(Details), new { id = sessionId });
            }

            var registration = new Registration
            {
                SessionId = sessionId,
                AttendeeName = userName,
                AttendeeEmail = userEmail
            };

            await _dataService.AddRegistrationAsync(registration);
            await _auditLogService.LogRegistrationAsync(ToNumericSessionId(session), session.Title, userName, userEmail);
            await SendConfirmationEmailAsync(session, userName, userEmail);

            TempData["Success"] = "Successfully registered for the session!";
            
            return RedirectToAction(nameof(Details), new { id = sessionId });
        }

        // GET: Sessions/MyRegistrations
        [Authorize]
        public async Task<IActionResult> MyRegistrations()
        {
            var userEmail = GetCurrentUserEmail();
            if (string.IsNullOrEmpty(userEmail))
            {
                return RedirectToAction(nameof(Index));
            }

            var allRegistrations = await _dataService.GetRegistrationsAsync();
            var userRegistrations = allRegistrations.Where(r => r.AttendeeEmail == userEmail).ToList();
            
            var sessions = await _dataService.GetSessionsAsync();
            var userSessions = sessions.Where(s => userRegistrations.Any(r => r.SessionId == s.Id)).ToList();

            return View(userSessions);
        }

        private async Task SendConfirmationEmailAsync(Session session, string attendeeName, string attendeeEmail)
        {
            try
            {
                if (string.IsNullOrEmpty(_functionsConfig.SendConfirmationUrl))
                {
                    _logger.LogWarning("SendConfirmationUrl not configured");
                    return;
                }

                var request = new
                {
                    SessionTitle = session.Title,
                    SessionDate = session.StartTime.ToString("MMMM dd, yyyy"),
                    SessionTime = $"{session.StartTime:h:mm tt} - {session.EndTime:h:mm tt}",
                    SessionRoom = session.Room,
                    Speaker = session.Speaker,
                    AttendeeName = attendeeName,
                    AttendeeEmail = attendeeEmail
                };

                var httpClient = _httpClientFactory.CreateClient();
                
                if (!string.IsNullOrEmpty(_functionsConfig.FunctionKey))
                {
                    httpClient.DefaultRequestHeaders.Add("x-functions-key", _functionsConfig.FunctionKey);
                }

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json");

                var response = await httpClient.PostAsync(_functionsConfig.SendConfirmationUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Confirmation email sent to {Email}", attendeeEmail);
                }
                else
                {
                    _logger.LogWarning("Failed to send confirmation email: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending confirmation email to {Email}", attendeeEmail);
            }
        }

        private static int ToNumericSessionId(Session session)
        {
            if (session.SessionNumber > 0)
            {
                return session.SessionNumber;
            }

            return int.TryParse(session.Id, out var value) ? value : 0;
        }

        private string? GetCurrentUserEmail()
        {
            return User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue("preferred_username")
                ?? User.FindFirstValue("upn")
                ?? User.FindFirstValue("emails");
        }
    }
}
