# Learning Path 3: Azure Storage (Blobs & Tables)

## Overview
In this learning path, you'll integrate Azure Storage into the ConferenceHub application to handle file uploads and audit logging using Blob Storage and Table Storage.

## What You'll Build
1. **Blob Storage**: Allow speakers to upload presentation slides (PDF/PPTX)
2. **Table Storage**: Track audit logs for all registration activities
3. **Web App Integration**: Add file upload functionality and automatic audit logging

## Prerequisites
- Completed Learning Path 1 & 2
- Azure Storage Account (or create a new one)
- Azure Storage SDK for .NET

## Variables
Use base variables from `01-Init.md` (do not redefine):  
`location`, `resourceGroupName`, `random`, `appServicePlanName`, `webAppName`, `appRuntime`, `publishDir`, `zipPath`

Additional variables for this learning path:
```bash
storageAccountName="stconferencehub$random"
```

---

## Part 1: Create Azure Storage Resources

### Step 1: Create Storage Account

```powershell
# Create a storage account for the application
az storage account create `
  --name $storageAccountName `
  --resource-group $resourceGroupNameName `
  --location $location `
  --sku Standard_LRS `
  --kind StorageV2

# Get the connection string
az storage account show-connection-string `
  --name $storageAccountName `
  --resource-group $resourceGroupNameName `
  --output tsv
```

Save the connection string - you'll need it later.

### Step 2: Create Blob Container

```powershell
# Get storage account key
$storageKey = az storage account keys list `
  --account-name $storageAccountName `
  --resource-group $resourceGroupNameName `
  --query "[0].value" `
  --output tsv

# Create container for speaker slides
az storage container create `
  --name speaker-slides `
  --account-name $storageAccountName `
  --account-key $storageKey `
  --public-access blob
```

### Step 3: Create Table Storage

```powershell
# Create table for audit logs
az storage table create `
  --name AuditLogs `
  --account-name $storageAccountName `
  --account-key $storageKey
```

---

## Part 2: Update Data Models

### Step 1: Add NuGet Packages

Add to `ConferenceHub/ConferenceHub.csproj`:
```powershell
cd ConferenceHub
dotnet add package Azure.Storage.Blobs
dotnet add package Azure.Data.Tables
```

### Step 2: Update Session Model

Update `Models/Session.cs` to include slide URL:
```csharp
namespace ConferenceHub.Models
{
    public class Session
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Speaker { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Room { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public int CurrentRegistrations { get; set; }
        public string? SlideUrl { get; set; }
        public DateTime? SlideUploadedAt { get; set; }
    }
}
```

### Step 3: Create Audit Log Model

Create `Models/AuditLogEntity.cs`:
```csharp
using Azure;
using Azure.Data.Tables;

namespace ConferenceHub.Models
{
    public class AuditLogEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty; // SessionId
        public string RowKey { get; set; } = string.Empty; // Timestamp + RegistrationId
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        // Custom properties
        public string Action { get; set; } = string.Empty;
        public string AttendeeName { get; set; } = string.Empty;
        public string AttendeeEmail { get; set; } = string.Empty;
        public string SessionTitle { get; set; } = string.Empty;
        public DateTime ActionTimestamp { get; set; }
        public string? AdditionalInfo { get; set; }
    }
}
```

---

## Part 3: Create Storage Services

### Step 1: Create Blob Storage Service

Create `Services/IBlobStorageService.cs`:
```csharp
namespace ConferenceHub.Services
{
    public interface IBlobStorageService
    {
        Task<string> UploadSlideAsync(int sessionId, string fileName, Stream fileStream, string contentType);
        Task<bool> DeleteSlideAsync(string blobUrl);
        Task<Stream?> DownloadSlideAsync(string blobName);
        string GetBlobUrl(string blobName);
    }
}
```

Create `Services/BlobStorageService.cs`:
```csharp
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ConferenceHub.Services
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName = "speaker-slides";
        private readonly ILogger<BlobStorageService> _logger;

        public BlobStorageService(string connectionString, ILogger<BlobStorageService> logger)
        {
            _blobServiceClient = new BlobServiceClient(connectionString);
            _logger = logger;
        }

        public async Task<string> UploadSlideAsync(int sessionId, string fileName, Stream fileStream, string contentType)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

                // Create unique blob name
                var extension = Path.GetExtension(fileName);
                var blobName = $"session-{sessionId}/{Guid.NewGuid()}{extension}";
                var blobClient = containerClient.GetBlobClient(blobName);

                // Upload with metadata
                var blobHttpHeaders = new BlobHttpHeaders { ContentType = contentType };
                var metadata = new Dictionary<string, string>
                {
                    { "SessionId", sessionId.ToString() },
                    { "OriginalFileName", fileName },
                    { "UploadedAt", DateTime.UtcNow.ToString("o") }
                };

                await blobClient.UploadAsync(fileStream, new BlobUploadOptions
                {
                    HttpHeaders = blobHttpHeaders,
                    Metadata = metadata
                });

                _logger.LogInformation("Uploaded slide for session {SessionId}: {BlobName}", sessionId, blobName);
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading slide for session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<bool> DeleteSlideAsync(string blobUrl)
        {
            try
            {
                var uri = new Uri(blobUrl);
                var blobName = uri.Segments[^1]; // Get last segment
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                var response = await blobClient.DeleteIfExistsAsync();
                return response.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting blob: {BlobUrl}", blobUrl);
                return false;
            }
        }

        public async Task<Stream?> DownloadSlideAsync(string blobName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                if (await blobClient.ExistsAsync())
                {
                    var downloadInfo = await blobClient.DownloadAsync();
                    return downloadInfo.Value.Content;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading blob: {BlobName}", blobName);
                return null;
            }
        }

        public string GetBlobUrl(string blobName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            return blobClient.Uri.ToString();
        }
    }
}
```

### Step 2: Create Table Storage Service

Create `Services/IAuditLogService.cs`:
```csharp
using ConferenceHub.Models;

namespace ConferenceHub.Services
{
    public interface IAuditLogService
    {
        Task LogRegistrationAsync(int sessionId, string sessionTitle, string attendeeName, string attendeeEmail);
        Task LogSlideUploadAsync(int sessionId, string sessionTitle, string uploadedBy);
        Task<List<AuditLogEntity>> GetSessionAuditLogsAsync(int sessionId);
        Task<List<AuditLogEntity>> GetAllAuditLogsAsync();
    }
}
```

Create `Services/AuditLogService.cs`:
```csharp
using Azure.Data.Tables;
using ConferenceHub.Models;

namespace ConferenceHub.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly TableClient _tableClient;
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(string connectionString, ILogger<AuditLogService> logger)
        {
            var tableServiceClient = new TableServiceClient(connectionString);
            _tableClient = tableServiceClient.GetTableClient("AuditLogs");
            _tableClient.CreateIfNotExists();
            _logger = logger;
        }

        public async Task LogRegistrationAsync(int sessionId, string sessionTitle, string attendeeName, string attendeeEmail)
        {
            try
            {
                var entity = new AuditLogEntity
                {
                    PartitionKey = sessionId.ToString(),
                    RowKey = $"{DateTime.UtcNow.Ticks}_{Guid.NewGuid()}",
                    Action = "Register",
                    AttendeeName = attendeeName,
                    AttendeeEmail = attendeeEmail,
                    SessionTitle = sessionTitle,
                    ActionTimestamp = DateTime.UtcNow
                };

                await _tableClient.AddEntityAsync(entity);
                _logger.LogInformation("Logged registration for {Email} to session {SessionId}", attendeeEmail, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging registration audit");
            }
        }

        public async Task LogSlideUploadAsync(int sessionId, string sessionTitle, string uploadedBy)
        {
            try
            {
                var entity = new AuditLogEntity
                {
                    PartitionKey = sessionId.ToString(),
                    RowKey = $"{DateTime.UtcNow.Ticks}_{Guid.NewGuid()}",
                    Action = "SlideUpload",
                    AttendeeName = uploadedBy,
                    SessionTitle = sessionTitle,
                    ActionTimestamp = DateTime.UtcNow,
                    AdditionalInfo = "Speaker slides uploaded"
                };

                await _tableClient.AddEntityAsync(entity);
                _logger.LogInformation("Logged slide upload for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging slide upload audit");
            }
        }

        public async Task<List<AuditLogEntity>> GetSessionAuditLogsAsync(int sessionId)
        {
            var logs = new List<AuditLogEntity>();
            try
            {
                await foreach (var entity in _tableClient.QueryAsync<AuditLogEntity>(e => e.PartitionKey == sessionId.ToString()))
                {
                    logs.Add(entity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit logs for session {SessionId}", sessionId);
            }
            return logs.OrderByDescending(l => l.ActionTimestamp).ToList();
        }

        public async Task<List<AuditLogEntity>> GetAllAuditLogsAsync()
        {
            var logs = new List<AuditLogEntity>();
            try
            {
                await foreach (var entity in _tableClient.QueryAsync<AuditLogEntity>())
                {
                    logs.Add(entity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all audit logs");
            }
            return logs.OrderByDescending(l => l.ActionTimestamp).ToList();
        }
    }
}
```

---

## Part 4: Update Configuration and Dependency Injection

### Step 1: Update appsettings.json

Add to `ConferenceHub/appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AzureStorage": {
    "ConnectionString": "YOUR_STORAGE_CONNECTION_STRING"
  },
  "AzureFunctions": {
    "SendConfirmationUrl": "http://localhost:7071/api/SendConfirmation",
    "FunctionKey": ""
  }
}
```

### Step 2: Update Program.cs

Update `ConferenceHub/Program.cs`:
```csharp
using ConferenceHub.Services;
using ConferenceHub.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IDataService, DataService>();

// Configure Azure Functions settings
builder.Services.Configure<AzureFunctionsConfig>(
    builder.Configuration.GetSection("AzureFunctions"));

// Add HttpClient for calling Azure Functions
builder.Services.AddHttpClient();

// Configure Azure Storage services
var storageConnectionString = builder.Configuration["AzureStorage:ConnectionString"];
builder.Services.AddSingleton<IBlobStorageService>(sp => 
    new BlobStorageService(storageConnectionString!, sp.GetRequiredService<ILogger<BlobStorageService>>()));
builder.Services.AddSingleton<IAuditLogService>(sp => 
    new AuditLogService(storageConnectionString!, sp.GetRequiredService<ILogger<AuditLogService>>()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

---

## Part 5: Update Controllers

### Step 1: Update SessionsController

Update `Controllers/SessionsController.cs` to include audit logging:
```csharp
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
```

### Step 2: Update OrganizerController for Slides

Update `Controllers/OrganizerController.cs`:
```csharp
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

        // ... existing Index, Create, Edit, Delete, Registrations methods ...

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
```

---

## Part 6: Create Views

### Step 1: Create Upload Slides View

Create `Views/Organizer/UploadSlides.cshtml`:
```cshtml
@model ConferenceHub.Models.Session

@{
    ViewData["Title"] = "Upload Slides";
}

<div class="container mt-4">
    <nav aria-label="breadcrumb">
        <ol class="breadcrumb">
            <li class="breadcrumb-item"><a asp-action="Index">Organizer Dashboard</a></li>
            <li class="breadcrumb-item active" aria-current="page">Upload Slides</li>
        </ol>
    </nav>

    <div class="card">
        <div class="card-header bg-info text-white">
            <h2 class="mb-0">Upload Slides for: @Model.Title</h2>
        </div>
        <div class="card-body">
            @if (TempData["Error"] != null)
            {
                <div class="alert alert-danger">@TempData["Error"]</div>
            }

            @if (!string.IsNullOrEmpty(Model.SlideUrl))
            {
                <div class="alert alert-info">
                    <strong>Current Slides:</strong> Uploaded on @Model.SlideUploadedAt?.ToString("MMM dd, yyyy h:mm tt")<br />
                    <a href="@Model.SlideUrl" target="_blank" class="btn btn-sm btn-outline-primary mt-2">
                        <i class="bi bi-file-earmark-pdf"></i> View Current Slides
                    </a>
                </div>
            }

            <form asp-action="UploadSlides" method="post" enctype="multipart/form-data">
                <input type="hidden" asp-for="Id" />

                <div class="mb-3">
                    <label for="slideFile" class="form-label">Select Slide File</label>
                    <input type="file" class="form-control" id="slideFile" name="slideFile" accept=".pdf,.ppt,.pptx" required />
                    <div class="form-text">Accepted formats: PDF, PPT, PPTX (Max size: 50MB)</div>
                </div>

                <div class="alert alert-warning">
                    <strong>Note:</strong> Uploading new slides will replace the existing ones.
                </div>

                <div class="d-flex justify-content-between mt-4">
                    <a asp-action="Index" class="btn btn-secondary">
                        <i class="bi bi-arrow-left"></i> Cancel
                    </a>
                    <button type="submit" class="btn btn-info">
                        <i class="bi bi-upload"></i> Upload Slides
                    </button>
                </div>
            </form>
        </div>
    </div>
</div>
```

### Step 2: Create Audit Logs View

Create `Views/Organizer/AuditLogs.cshtml`:
```cshtml
@model List<ConferenceHub.Models.AuditLogEntity>

@{
    ViewData["Title"] = "Audit Logs";
    var sessionTitle = ViewBag.SessionTitle as string;
}

<div class="container mt-4">
    <nav aria-label="breadcrumb">
        <ol class="breadcrumb">
            <li class="breadcrumb-item"><a asp-action="Index">Organizer Dashboard</a></li>
            <li class="breadcrumb-item active" aria-current="page">Audit Logs</li>
        </ol>
    </nav>

    <div class="card">
        <div class="card-header">
            <h2 class="mb-0">
                Audit Logs
                @if (!string.IsNullOrEmpty(sessionTitle))
                {
                    <span class="text-muted">- @sessionTitle</span>
                }
            </h2>
        </div>
        <div class="card-body">
            @if (Model.Any())
            {
                <div class="table-responsive">
                    <table class="table table-striped table-hover">
                        <thead>
                            <tr>
                                <th>Timestamp</th>
                                <th>Action</th>
                                <th>Session</th>
                                <th>User</th>
                                <th>Details</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var log in Model)
                            {
                                <tr>
                                    <td>@log.ActionTimestamp.ToString("MMM dd, yyyy h:mm:ss tt")</td>
                                    <td>
                                        @if (log.Action == "Register")
                                        {
                                            <span class="badge bg-success">Registration</span>
                                        }
                                        else if (log.Action == "SlideUpload")
                                        {
                                            <span class="badge bg-info">Slide Upload</span>
                                        }
                                        else
                                        {
                                            <span class="badge bg-secondary">@log.Action</span>
                                        }
                                    </td>
                                    <td>
                                        <strong>@log.SessionTitle</strong><br />
                                        <small class="text-muted">Session ID: @log.PartitionKey</small>
                                    </td>
                                    <td>
                                        @log.AttendeeName
                                        @if (!string.IsNullOrEmpty(log.AttendeeEmail))
                                        {
                                            <br />
                                            <small class="text-muted">@log.AttendeeEmail</small>
                                        }
                                    </td>
                                    <td>@log.AdditionalInfo</td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
                <div class="mt-3">
                    <p class="text-muted">Total Entries: <strong>@Model.Count</strong></p>
                </div>
            }
            else
            {
                <div class="alert alert-info">
                    <h5>No audit logs yet</h5>
                    <p>Audit logs will appear here as users register for sessions and speakers upload slides.</p>
                </div>
            }
        </div>
        <div class="card-footer">
            <a asp-action="Index" class="btn btn-secondary">
                <i class="bi bi-arrow-left"></i> Back to Dashboard
            </a>
        </div>
    </div>
</div>
```

### Step 3: Update Organizer Index View

Update `Views/Organizer/Index.cshtml` to add upload slides button:
Add this to the Actions column in the table (inside the btn-group):
```cshtml
<a asp-action="UploadSlides" asp-route-id="@session.Id" class="btn btn-sm btn-outline-info">
    <i class="bi bi-upload"></i> Slides
</a>
```

And add this button near the top:
```cshtml
<a asp-action="AuditLogs" class="btn btn-warning me-2">
    <i class="bi bi-journal-text"></i> View Audit Logs
</a>
```

### Step 4: Update Session Details View

Update `Views/Sessions/Details.cshtml` to show slides link:
Add this after the Location section:
```cshtml
@if (!string.IsNullOrEmpty(Model.SlideUrl))
{
    <div class="mt-3">
        <h5>Presentation Slides</h5>
        <a href="@Model.SlideUrl" target="_blank" class="btn btn-outline-primary">
            <i class="bi bi-file-earmark-pdf"></i> View Slides
        </a>
        <small class="text-muted d-block mt-1">
            Uploaded: @Model.SlideUploadedAt?.ToString("MMM dd, yyyy")
        </small>
    </div>
}
```

---

## Part 7: Deploy to Azure

### Step 1: Update Azure App Configuration

```powershell
# Set storage connection string in Web App
$connectionString = az storage account show-connection-string `
  --name $storageAccountName `
  --resource-group $resourceGroupNameName `
  --output tsv

az webapp config appsettings set `
  --name conferencehub-demo-az204reinke `
  --resource-group $resourceGroupNameName `
  --settings AzureStorage__ConnectionString="$connectionString"
```

### Step 2: Deploy Updated Application

```powershell
cd ConferenceHub
dotnet publish -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./app.zip -Force
az webapp deployment source config-zip `
  --resource-group $resourceGroupNameName `
  --name conferencehub-demo-az204reinke `
  --src ./app.zip
```

---

## Part 8: Test the Application

### Test Blob Storage

1. Navigate to Organizer Dashboard
2. Click "Slides" for any session
3. Upload a PDF or PowerPoint file
4. Verify the file appears in Azure Storage Explorer or Portal
5. Check that the slides link appears on the session details page

### Test Table Storage

1. Register for a session
2. Navigate to "View Audit Logs" in Organizer Dashboard
3. Verify registration appears in audit log
4. Upload slides and verify that action is logged
5. Filter by session ID to see session-specific logs

### Verify in Azure Portal

1. **Blob Storage**:
   - Navigate to Storage Account → Containers → speaker-slides
   - Verify uploaded files are there with proper metadata

2. **Table Storage**:
   - Navigate to Storage Account → Tables → AuditLogs
   - Query and view audit entries
   - Verify PartitionKey (SessionId) and RowKey structure

---

## Summary

You've successfully:
- ✅ Integrated Azure Blob Storage for file uploads
- ✅ Implemented Azure Table Storage for audit logging
- ✅ Added slide upload functionality for speakers
- ✅ Created comprehensive audit trail for all actions
- ✅ Updated UI to display slides and audit logs

## Next Steps

In **Learning Path 4**, you'll:
- Replace JSON file storage with **Azure Cosmos DB**
- Migrate session and registration data to Cosmos DB
- Update all services to use Cosmos DB for data persistence
- Implement advanced querying and filtering

---

## Troubleshooting

### Blob upload fails
- Check storage connection string
- Verify container exists and has proper permissions
- Check file size limits

### Table storage not logging
- Verify table name is correct ("AuditLogs")
- Check storage account connection
- Review application logs for errors

### Can't view uploaded slides
- Ensure blob container has public read access
- Verify blob URL is correctly saved to session
- Check CORS settings if accessing from different domain

## Azure DevOps Pipeline (Incremental Deployment)
- Pipeline: `Learning Path/03-Storage/azure-pipelines.yml`
- Bicep: `Learning Path/03-Storage/infra.bicep`
- Required variables: `azureSubscription`, `resourceGroupName`, `location`, `storageAccountName`, `functionAppName`, `mainWebAppName`
- Notes: The pipeline provisions Storage (blob container + table) and updates web app settings for `AzureStorage__ConnectionString` plus Functions settings.
