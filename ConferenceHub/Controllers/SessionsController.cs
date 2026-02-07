using ConferenceHub.Models;
using ConferenceHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConferenceHub.Controllers
{
    public class SessionsController : Controller
    {
        private readonly IDataService _dataService;
        private readonly IRegistrationMessagePublisher _registrationMessagePublisher;
        private readonly IEventTelemetryService _eventTelemetryService;
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(
            IDataService dataService,
            IRegistrationMessagePublisher registrationMessagePublisher,
            IEventTelemetryService eventTelemetryService,
            ILogger<SessionsController> logger)
        {
            _dataService = dataService;
            _registrationMessagePublisher = registrationMessagePublisher;
            _eventTelemetryService = eventTelemetryService;
            _logger = logger;
        }

        // GET: Sessions
        public async Task<IActionResult> Index()
        {
            var sessions = await _dataService.GetSessionsAsync();
            await _eventTelemetryService.TrackAsync("sessions.list.viewed", new
            {
                totalSessions = sessions.Count,
                user = User.Identity?.Name
            });
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

            await _eventTelemetryService.TrackAsync("session.details.viewed", new
            {
                sessionId = session.Id,
                sessionTitle = session.Title,
                user = User.Identity?.Name
            });
            return View(session);
        }

        // POST: Sessions/Register
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Register(int sessionId)
        {
            var session = await _dataService.GetSessionByIdAsync(sessionId);
            if (session == null)
            {
                return NotFound();
            }

            var attendeeName = User.FindFirst("name")?.Value
                ?? User.Identity?.Name
                ?? "Unknown attendee";

            var attendeeEmail = User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("preferred_username")?.Value
                ?? User.Identity?.Name
                ?? "unknown@local";

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
            await _eventTelemetryService.TrackAsync("session.registration.created", new
            {
                sessionId = session.Id,
                sessionTitle = session.Title,
                attendeeEmail,
                attendeeName
            });

            TempData["Success"] = "Successfully registered for the session!";

            await PublishRegistrationMessageAsync(session, attendeeName, attendeeEmail);

            return RedirectToAction(nameof(Details), new { id = sessionId });
        }

        private async Task PublishRegistrationMessageAsync(Session session, string attendeeName, string attendeeEmail)
        {
            try
            {
                var registrationRequest = new RegistrationMessage
                {
                    SessionId = session.Id,
                    SessionTitle = session.Title,
                    AttendeeName = attendeeName,
                    AttendeeEmail = attendeeEmail,
                    SessionStartTime = session.StartTime,
                    Room = session.Room
                };

                await _registrationMessagePublisher.PublishAsync(registrationRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing registration message");
            }
        }
    }
}
