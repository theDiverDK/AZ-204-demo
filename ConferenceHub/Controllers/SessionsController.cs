using ConferenceHub.Models;
using ConferenceHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace ConferenceHub.Controllers
{
    [Authorize]
    public class SessionsController : Controller
    {
        private readonly ICosmosDbService _cosmosDbService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuditLogService _auditLogService;
        private readonly AzureFunctionsConfig _functionsConfig;
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(
            ICosmosDbService cosmosDbService,
            IHttpClientFactory httpClientFactory,
            IAuditLogService auditLogService,
            IOptions<AzureFunctionsConfig> functionsConfig,
            ILogger<SessionsController> logger)
        {
            _cosmosDbService = cosmosDbService;
            _httpClientFactory = httpClientFactory;
            _auditLogService = auditLogService;
            _functionsConfig = functionsConfig.Value;
            _logger = logger;
        }

        // GET: Sessions
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? track, string? level)
        {
            IEnumerable<Session> sessions;
            
            if (!string.IsNullOrEmpty(track) || !string.IsNullOrEmpty(level))
            {
                sessions = await _cosmosDbService.GetSessionsByFilterAsync(track, level);
                ViewBag.SelectedTrack = track;
                ViewBag.SelectedLevel = level;
            }
            else
            {
                sessions = await _cosmosDbService.GetSessionsAsync();
            }

            // Get unique tracks and levels for filter dropdowns
            var allSessions = await _cosmosDbService.GetSessionsAsync();
            ViewBag.Tracks = allSessions.Select(s => s.Track).Distinct().OrderBy(t => t).ToList();
            ViewBag.Levels = allSessions.Select(s => s.Level).Distinct().OrderBy(l => l).ToList();

            return View(sessions);
        }

        // GET: Sessions/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(string id)
        {
            var session = await _cosmosDbService.GetSessionByIdAsync(id);
            if (session == null)
            {
                return NotFound();
            }

            // Get actual registration count from Cosmos DB
            session.CurrentRegistrations = await _cosmosDbService.GetRegistrationCountBySessionAsync(id);

            return View(session);
        }

        // POST: Sessions/Register
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Register(string sessionId, string attendeeName, string attendeeEmail)
        {
            var session = await _cosmosDbService.GetSessionByIdAsync(sessionId);
            if (session == null)
            {
                return NotFound();
            }

            // Check if registration is closed
            if (session.RegistrationClosed)
            {
                TempData["Error"] = "Registration for this session is closed.";
                return RedirectToAction(nameof(Details), new { id = sessionId });
            }

            // If the form did not provide an email/name, use authenticated user claims.
            attendeeEmail = string.IsNullOrWhiteSpace(attendeeEmail) ? GetCurrentUserEmail() ?? string.Empty : attendeeEmail;
            attendeeName = string.IsNullOrWhiteSpace(attendeeName) ? (User.Identity?.Name ?? "Authenticated User") : attendeeName;

            // Get current registration count
            var currentCount = await _cosmosDbService.GetRegistrationCountBySessionAsync(sessionId);
            if (currentCount >= session.Capacity)
            {
                TempData["Error"] = "This session is at full capacity.";
                return RedirectToAction(nameof(Details), new { id = sessionId });
            }

            var registration = new Registration
            {
                SessionId = sessionId,
                SessionTitle = session.Title,
                AttendeeName = attendeeName,
                AttendeeEmail = attendeeEmail
            };

            await _cosmosDbService.AddRegistrationAsync(registration);

            // Log to audit table
            await _auditLogService.LogRegistrationAsync(
                int.Parse(session.SessionNumber.ToString()), 
                session.Title, 
                attendeeName, 
                attendeeEmail);

            // Call Azure Function to send confirmation email
            await SendConfirmationEmailAsync(session, attendeeName, attendeeEmail);

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

            var allRegistrations = await _cosmosDbService.GetRegistrationsAsync();
            var myRegistrations = allRegistrations.Where(r => r.AttendeeEmail == userEmail).ToList();

            var sessions = await _cosmosDbService.GetSessionsAsync();
            var mySessionIds = myRegistrations.Select(r => r.SessionId).ToHashSet();
            var mySessions = sessions.Where(s => mySessionIds.Contains(s.Id)).ToList();

            return View(mySessions);
        }

        private string? GetCurrentUserEmail()
        {
            return User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue("preferred_username")
                ?? User.FindFirstValue("upn")
                ?? User.FindFirstValue("emails");
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

                var json = System.Text.Json.JsonSerializer.Serialize(registrationRequest);
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
