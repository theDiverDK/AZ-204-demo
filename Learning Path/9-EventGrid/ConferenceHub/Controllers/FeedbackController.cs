using ConferenceHub.Models;
using ConferenceHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ConferenceHub.Controllers
{
    [Authorize]
    public class FeedbackController : Controller
    {
        private readonly IDataService _dataService;
        private readonly IEventHubService _eventHubService;
        private readonly ILogger<FeedbackController> _logger;

        public FeedbackController(
            IDataService dataService,
            IEventHubService eventHubService,
            ILogger<FeedbackController> logger)
        {
            _dataService = dataService;
            _eventHubService = eventHubService;
            _logger = logger;
        }

        // GET: Feedback/Submit/5
        public async Task<IActionResult> Submit(int sessionId)
        {
            var session = await _dataService.GetSessionByIdAsync(sessionId);
            if (session == null)
            {
                return NotFound();
            }

            // Check if session has ended
            if (session.EndTime > DateTime.Now)
            {
                TempData["Error"] = "Feedback can only be submitted after the session ends.";
                return RedirectToAction("Details", "Sessions", new { id = sessionId });
            }

            ViewBag.Session = session;
            return View();
        }

        // POST: Feedback/Submit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(SessionFeedback feedback)
        {
            try
            {
                // Get user info
                feedback.AttendeeEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "unknown@email.com";
                feedback.AttendeeName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
                feedback.SubmittedAt = DateTime.UtcNow;

                // Get session info
                var session = await _dataService.GetSessionByIdAsync(feedback.SessionId);
                if (session == null)
                {
                    return NotFound();
                }
                feedback.SessionTitle = session.Title;

                // Send to Event Hub
                await _eventHubService.SendFeedbackAsync(feedback);

                TempData["Success"] = "Thank you for your feedback!";
                return RedirectToAction("Details", "Sessions", new { id = feedback.SessionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting feedback");
                TempData["Error"] = "Error submitting feedback. Please try again.";
                return RedirectToAction("Submit", new { sessionId = feedback.SessionId });
            }
        }
    }
}
