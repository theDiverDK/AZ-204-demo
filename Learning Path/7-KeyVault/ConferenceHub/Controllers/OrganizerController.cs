using ConferenceHub.Models;
using ConferenceHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;

namespace ConferenceHub.Controllers
{
    [Authorize(Policy = "OrganizerOnly")]
    public class OrganizerController : Controller
    {
        private readonly IDataService _dataService;
        private readonly IBlobStorageService _blobStorageService;
        private readonly IAuditLogService _auditLogService;
        private readonly IFeatureManager _featureManager;
        private readonly ILogger<OrganizerController> _logger;

        public OrganizerController(
            IDataService dataService,
            IBlobStorageService blobStorageService,
            IAuditLogService auditLogService,
            IFeatureManager featureManager,
            ILogger<OrganizerController> logger)
        {
            _dataService = dataService;
            _blobStorageService = blobStorageService;
            _auditLogService = auditLogService;
            _featureManager = featureManager;
            _logger = logger;
        }

        // GET: Organizer
        public async Task<IActionResult> Index()
        {
            var sessions = await _dataService.GetSessionsAsync();
            var registrations = await _dataService.GetRegistrationsAsync();
            ViewBag.TotalRegistrations = registrations.Count;
            return View(sessions);
        }

        // GET: Organizer/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Organizer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Session session)
        {
            if (ModelState.IsValid)
            {
                session.CurrentRegistrations = 0;
                await _dataService.AddSessionAsync(session);
                TempData["Success"] = "Session created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(session);
        }

        // GET: Organizer/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var session = await _dataService.GetSessionByIdAsync(id);
            if (session == null)
            {
                return NotFound();
            }
            return View(session);
        }

        // POST: Organizer/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Session session)
        {
            if (id != session.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                await _dataService.UpdateSessionAsync(session);
                TempData["Success"] = "Session updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(session);
        }

        // GET: Organizer/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var session = await _dataService.GetSessionByIdAsync(id);
            if (session == null)
            {
                return NotFound();
            }
            return View(session);
        }

        // POST: Organizer/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _dataService.DeleteSessionAsync(id);
            TempData["Success"] = "Session deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Organizer/Registrations
        public async Task<IActionResult> Registrations()
        {
            var registrations = await _dataService.GetRegistrationsAsync();
            var sessions = await _dataService.GetSessionsAsync();
            
            var registrationDetails = registrations.Select(r => new
            {
                Registration = r,
                Session = sessions.FirstOrDefault(s => s.Id == r.SessionId)
            }).ToList();

            return View(registrationDetails);
        }

        // GET: Organizer/UploadSlides/5
        [FeatureGate("SlideUpload")]
        public async Task<IActionResult> UploadSlides(int id)
        {
            var session = await _dataService.GetSessionByIdAsync(id);
            if (session == null)
            {
                return NotFound();
            }
            return View(session);
        }

        // POST: Organizer/UploadSlides/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [FeatureGate("SlideUpload")]
        public async Task<IActionResult> UploadSlides(int id, IFormFile slideFile)
        {
            try
            {
                var session = await _dataService.GetSessionByIdAsync(id);
                if (session == null)
                {
                    return NotFound();
                }

                if (slideFile == null || slideFile.Length == 0)
                {
                    TempData["Error"] = "Please select a file to upload.";
                    return RedirectToAction(nameof(UploadSlides), new { id });
                }

                // Upload to blob storage
                var blobUrl = await _blobStorageService.UploadSlideAsync(id, slideFile);

                // Update session with slide URL
                session.SlideUrl = blobUrl;
                session.SlideUploadedAt = DateTime.UtcNow;
                await _dataService.UpdateSessionAsync(session);

                // Log the upload
                await _auditLogService.LogSlideUploadAsync(id, session.Title, session.Speaker, blobUrl);

                _logger.LogInformation("Slides uploaded for session {SessionId}", id);
                TempData["Success"] = "Slides uploaded successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading slides for session {SessionId}", id);
                TempData["Error"] = "Error uploading slides. Please try again.";
                return RedirectToAction(nameof(UploadSlides), new { id });
            }
        }

        // GET: Organizer/AuditLogs
        public async Task<IActionResult> AuditLogs(int? sessionId)
        {
            List<ConferenceHub.Models.AuditLogEntity> logs;

            if (sessionId.HasValue)
            {
                var session = await _dataService.GetSessionByIdAsync(sessionId.Value);
                if (session != null)
                {
                    ViewBag.SessionTitle = session.Title;
                    logs = await _auditLogService.GetSessionAuditLogsAsync(sessionId.Value);
                }
                else
                {
                    logs = new List<ConferenceHub.Models.AuditLogEntity>();
                }
            }
            else
            {
                logs = await _auditLogService.GetAllAuditLogsAsync();
            }

            return View(logs);
        }
    }
}
