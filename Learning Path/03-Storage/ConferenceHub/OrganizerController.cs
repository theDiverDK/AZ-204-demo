using ConferenceHub.Models;
using ConferenceHub.Services;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceHub.Controllers
{
    public class OrganizerController : Controller
    {
        private readonly IDataService _dataService;
        private readonly IBlobStorageService _blobStorageService;
        private readonly IAuditLogService _auditLogService;
        private readonly ILogger<OrganizerController> _logger;

        public OrganizerController(
            IDataService dataService,
            IBlobStorageService blobStorageService,
            IAuditLogService auditLogService,
            ILogger<OrganizerController> logger)
        {
            _dataService = dataService;
            _blobStorageService = blobStorageService;
            _auditLogService = auditLogService;
            _logger = logger;
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
        public async Task<IActionResult> UploadSlides(int id, IFormFile slideFile)
        {
            var session = await _dataService.GetSessionByIdAsync(id);
            if (session == null)
            {
                return NotFound();
            }

            if (slideFile == null || slideFile.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return View(session);
            }

            // Validate file type
            var allowedExtensions = new[] { ".pdf", ".pptx", ".ppt" };
            var extension = Path.GetExtension(slideFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Only PDF and PowerPoint files are allowed.";
                return View(session);
            }

            // Validate file size (max 50MB)
            if (slideFile.Length > 50 * 1024 * 1024)
            {
                TempData["Error"] = "File size must be less than 50MB.";
                return View(session);
            }

            try
            {
                // Upload to blob storage
                using var stream = slideFile.OpenReadStream();
                var blobUrl = await _blobStorageService.UploadSlideAsync(
                    id, 
                    slideFile.FileName, 
                    stream, 
                    slideFile.ContentType);

                // Update session with slide URL
                session.SlideUrl = blobUrl;
                session.SlideUploadedAt = DateTime.UtcNow;
                await _dataService.UpdateSessionAsync(session);

                // Log to audit table
                await _auditLogService.LogSlideUploadAsync(id, session.Title, session.Speaker);

                TempData["Success"] = "Slides uploaded successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading slides for session {SessionId}", id);
                TempData["Error"] = "Error uploading slides. Please try again.";
                return View(session);
            }
        }

        // GET: Organizer/AuditLogs/5
        public async Task<IActionResult> AuditLogs(int? sessionId)
        {
            List<AuditLogEntity> logs;
            
            if (sessionId.HasValue)
            {
                logs = await _auditLogService.GetSessionAuditLogsAsync(sessionId.Value);
                var session = await _dataService.GetSessionByIdAsync(sessionId.Value);
                ViewBag.SessionTitle = session?.Title;
            }
            else
            {
                logs = await _auditLogService.GetAllAuditLogsAsync();
            }

            return View(logs);
        }
    }
}


