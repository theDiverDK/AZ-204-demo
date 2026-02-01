# Learning Path 9: Event Grid & Event Hub

## Overview
In this learning path, you'll implement event-driven architecture using Azure Event Grid for discrete events (blob uploads) and Azure Event Hub for high-throughput streaming data (session feedback).

## What You'll Build
1. **Event Grid Subscription**: React to blob storage events (slide uploads)
2. **Event Hub**: Stream real-time session feedback and ratings
3. **Event Processors**: Handle and process events
4. **Webhook Endpoints**: Receive Event Grid notifications

## Prerequisites
- Completed Learning Path 1-8
- Azure Storage Account with blob container
- Deployed Web App and Azure Functions

## Variables
Use base variables from `01-Init.md` (do not redefine):  
`location`, `resourceGroupName`, `random`, `appServicePlanName`, `webAppName`, `appRuntime`, `publishDir`, `zipPath`

Additional variables for this learning path:
```bash
eventHubNamespaceName="evhns-conferencehub-$random"
eventHubName="session-feedback"
eventGridTopicName="evgt-conferencehub-$random"
```

---

## Part 1: Create Event Grid Topic and Subscription

### Step 1: Enable Event Grid on Storage Account

```powershell
# Get storage account ID
$storageAccountId = az storage account show `
  --name $storageAccountName `
  --resource-group $resourceGroupNameName `
  --query id `
  --output tsv

Write-Host "Storage Account ID: $storageAccountId"
```

**Bash**
```bash
# Get storage account ID
storageAccountId=$(az storage account show \
  --name $storageAccountName \
  --resource-group $resourceGroupNameName \
  --query id \
  --output tsv)

echo Storage Account ID: $storageAccountId
```

### Step 2: Create Event Grid System Topic

```powershell
# Create Event Grid system topic for blob storage
az eventgrid system-topic create `
  --name eg-topic-conferencehub-storage `
  --resource-group $resourceGroupNameName `
  --location $location `
  --topic-type Microsoft.Storage.StorageAccounts `
  --source $storageAccountId

Write-Host "Event Grid system topic created"
```

**Bash**
```bash
# Create Event Grid system topic for blob storage
az eventgrid system-topic create \
  --name eg-topic-conferencehub-storage \
  --resource-group $resourceGroupNameName \
  --location $location \
  --topic-type Microsoft.Storage.StorageAccounts \
  --source $storageAccountId

echo Event Grid system topic created
```

### Step 3: Create Azure Function for Event Processing

Create new function `ConferenceHubFunctions/ProcessSlideUpload.cs`:
```csharp
using System.Net;
using System.Text.Json;
using Azure.Messaging.EventGrid;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ConferenceHubFunctions
{
    public class ProcessSlideUpload
    {
        private readonly ILogger _logger;

        public ProcessSlideUpload(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ProcessSlideUpload>();
        }

        [Function("ProcessSlideUpload")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Processing Event Grid notification");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            // Parse Event Grid events
            EventGridEvent[] events = JsonSerializer.Deserialize<EventGridEvent[]>(requestBody);

            if (events == null || events.Length == 0)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("No events received");
                return badResponse;
            }

            foreach (var eventGridEvent in events)
            {
                // Handle validation event (Event Grid subscription validation)
                if (eventGridEvent.EventType == "Microsoft.EventGrid.SubscriptionValidationEvent")
                {
                    var validationData = JsonSerializer.Deserialize<SubscriptionValidationEventData>(
                        eventGridEvent.Data.ToString());
                    
                    var validationResponse = new
                    {
                        validationResponse = validationData?.ValidationCode
                    };

                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/json");
                    await response.WriteStringAsync(JsonSerializer.Serialize(validationResponse));
                    return response;
                }

                // Handle blob created event
                if (eventGridEvent.EventType == "Microsoft.Storage.BlobCreated")
                {
                    var blobCreatedData = JsonSerializer.Deserialize<StorageBlobCreatedEventData>(
                        eventGridEvent.Data.ToString());

                    _logger.LogInformation("Blob created: {BlobUrl}", blobCreatedData?.Url);

                    // Process the slide upload
                    await ProcessSlideAsync(blobCreatedData);
                }
            }

            var successResponse = req.CreateResponse(HttpStatusCode.OK);
            await successResponse.WriteStringAsync("Events processed successfully");
            return successResponse;
        }

        private async Task ProcessSlideAsync(StorageBlobCreatedEventData? blobData)
        {
            if (blobData == null) return;

            _logger.LogInformation("Processing slide: {BlobUrl}", blobData.Url);

            // Extract session ID from blob path (e.g., session-5/guid.pdf)
            var blobPath = new Uri(blobData.Url).AbsolutePath;
            var parts = blobPath.Split('/');
            
            if (parts.Length >= 3 && parts[^2].StartsWith("session-"))
            {
                var sessionIdStr = parts[^2].Replace("session-", "");
                if (int.TryParse(sessionIdStr, out int sessionId))
                {
                    _logger.LogInformation("Slide uploaded for session {SessionId}", sessionId);

                    // TODO: Send notification to organizers
                    // TODO: Update session metadata
                    // TODO: Trigger additional processing (OCR, thumbnail generation, etc.)
                    
                    await Task.Delay(100); // Simulate processing
                }
            }
        }

        private class SubscriptionValidationEventData
        {
            public string? ValidationCode { get; set; }
            public string? ValidationUrl { get; set; }
        }

        private class StorageBlobCreatedEventData
        {
            public string? Api { get; set; }
            public string? ClientRequestId { get; set; }
            public string? RequestId { get; set; }
            public string? ETag { get; set; }
            public string? ContentType { get; set; }
            public long ContentLength { get; set; }
            public string? BlobType { get; set; }
            public string? Url { get; set; }
            public string? Sequencer { get; set; }
            public Dictionary<string, object>? StorageDiagnostics { get; set; }
        }
    }
}
```

Add NuGet package:
```powershell
cd ../ConferenceHubFunctions
dotnet add package Azure.Messaging.EventGrid
```

**Bash**
```bash
cd ../ConferenceHubFunctions
dotnet add package Azure.Messaging.EventGrid
```

Deploy function:
```powershell
func azure functionapp publish $functionAppName
```

**Bash**
```bash
func azure functionapp publish $functionAppName
```

### Step 4: Create Event Grid Subscription

```powershell
# Get function URL
$functionUrl = az functionapp function show `
  --name $functionAppName `
  --resource-group $resourceGroupNameName `
  --function-name ProcessSlideUpload `
  --query invokeUrlTemplate `
  --output tsv

# Get function key
$functionKey = az functionapp keys list `
  --name $functionAppName `
  --resource-group $resourceGroupNameName `
  --query "functionKeys.default" `
  --output tsv

# Create event subscription for BlobCreated events
az eventgrid system-topic event-subscription create `
  --name slide-upload-subscription `
  --resource-group $resourceGroupNameName `
  --system-topic-name eg-topic-conferencehub-storage `
  --endpoint "$functionUrl&code=$functionKey" `
  --endpoint-type webhook `
  --included-event-types Microsoft.Storage.BlobCreated `
  --subject-begins-with /blobServices/default/containers/speaker-slides/

Write-Host "Event Grid subscription created"
```

**Bash**
```bash
# Get function URL
functionUrl=$(az functionapp function show \
  --name $functionAppName \
  --resource-group $resourceGroupNameName \
  --function-name ProcessSlideUpload \
  --query invokeUrlTemplate \
  --output tsv)

# Get function key
functionKey=$(az functionapp keys list \
  --name $functionAppName \
  --resource-group $resourceGroupNameName \
  --query "functionKeys.default" \
  --output tsv)

# Create event subscription for BlobCreated events
az eventgrid system-topic event-subscription create \
  --name slide-upload-subscription \
  --resource-group $resourceGroupNameName \
  --system-topic-name eg-topic-conferencehub-storage \
  --endpoint "$functionUrl&code=$functionKey" \
  --endpoint-type webhook \
  --included-event-types Microsoft.Storage.BlobCreated \
  --subject-begins-with /blobServices/default/containers/speaker-slides/

echo Event Grid subscription created
```

---

## Part 2: Create Azure Event Hub

### Step 1: Create Event Hub Namespace

```powershell
# Create Event Hub namespace
az eventhubs namespace create `
  --name $eventHubNamespaceName `
  --resource-group $resourceGroupNameName `
  --location $location `
  --sku Standard `
  --capacity 1

Write-Host "Event Hub namespace created"
```

**Bash**
```bash
# Create Event Hub namespace
az eventhubs namespace create \
  --name $eventHubNamespaceName \
  --resource-group $resourceGroupNameName \
  --location $location \
  --sku Standard \
  --capacity 1

echo Event Hub namespace created
```

### Step 2: Create Event Hub

```powershell
# Create Event Hub for session feedback
az eventhubs eventhub create `
  --name session-feedback `
  --namespace-name $eventHubNamespaceName `
  --resource-group $resourceGroupNameName `
  --partition-count 2 `
  --message-retention 1

# Create consumer group for processing
az eventhubs eventhub consumer-group create `
  --eventhub-name session-feedback `
  --namespace-name $eventHubNamespaceName `
  --resource-group $resourceGroupNameName `
  --name feedback-processor

Write-Host "Event Hub created"
```

**Bash**
```bash
# Create Event Hub for session feedback
az eventhubs eventhub create \
  --name session-feedback \
  --namespace-name $eventHubNamespaceName \
  --resource-group $resourceGroupNameName \
  --partition-count 2 \
  --message-retention 1

# Create consumer group for processing
az eventhubs eventhub consumer-group create \
  --eventhub-name session-feedback \
  --namespace-name $eventHubNamespaceName \
  --resource-group $resourceGroupNameName \
  --name feedback-processor

echo Event Hub created
```

### Step 3: Get Connection Strings

```powershell
# Get Event Hub connection string
$eventHubConnectionString = az eventhubs namespace authorization-rule keys list `
  --namespace-name $eventHubNamespaceName `
  --resource-group $resourceGroupNameName `
  --name RootManageSharedAccessKey `
  --query primaryConnectionString `
  --output tsv

Write-Host "Event Hub Connection String: $eventHubConnectionString"

# Store in Key Vault
az keyvault secret set `
  --vault-name $keyVaultName `
  --name "EventHub--ConnectionString" `
  --value $eventHubConnectionString
```

**Bash**
```bash
# Get Event Hub connection string
eventHubConnectionString=$(az eventhubs namespace authorization-rule keys list \
  --namespace-name $eventHubNamespaceName \
  --resource-group $resourceGroupNameName \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString \
  --output tsv)

echo Event Hub Connection String: $eventHubConnectionString

# Store in Key Vault
az keyvault secret set \
  --vault-name $keyVaultName \
  --name "EventHub--ConnectionString" \
  --value $eventHubConnectionString
```

---

## Part 3: Create Feedback Model and Service

### Step 1: Create Feedback Models

Create `ConferenceHub/Models/SessionFeedback.cs`:
```csharp
namespace ConferenceHub.Models
{
    public class SessionFeedback
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int SessionId { get; set; }
        public string SessionTitle { get; set; } = string.Empty;
        public string AttendeeEmail { get; set; } = string.Empty;
        public string AttendeeName { get; set; } = string.Empty;
        public int Rating { get; set; } // 1-5 stars
        public string? Comment { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public bool IsRecommended { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class FeedbackStatistics
    {
        public int SessionId { get; set; }
        public double AverageRating { get; set; }
        public int TotalFeedbacks { get; set; }
        public int FiveStars { get; set; }
        public int FourStars { get; set; }
        public int ThreeStars { get; set; }
        public int TwoStars { get; set; }
        public int OneStar { get; set; }
        public int RecommendationCount { get; set; }
        public double RecommendationPercentage { get; set; }
    }
}
```

### Step 2: Create Event Hub Producer Service

Create `ConferenceHub/Services/IEventHubService.cs`:
```csharp
using ConferenceHub.Models;

namespace ConferenceHub.Services
{
    public interface IEventHubService
    {
        Task SendFeedbackAsync(SessionFeedback feedback);
        Task SendBatchFeedbackAsync(List<SessionFeedback> feedbacks);
    }
}
```

Create `ConferenceHub/Services/EventHubService.cs`:
```csharp
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using ConferenceHub.Models;
using System.Text;
using System.Text.Json;

namespace ConferenceHub.Services
{
    public class EventHubService : IEventHubService, IAsyncDisposable
    {
        private readonly EventHubProducerClient _producerClient;
        private readonly ILogger<EventHubService> _logger;

        public EventHubService(string connectionString, string eventHubName, ILogger<EventHubService> logger)
        {
            _producerClient = new EventHubProducerClient(connectionString, eventHubName);
            _logger = logger;
        }

        public async Task SendFeedbackAsync(SessionFeedback feedback)
        {
            try
            {
                var eventData = new EventData(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(feedback)));

                // Add properties for routing/filtering
                eventData.Properties.Add("SessionId", feedback.SessionId);
                eventData.Properties.Add("Rating", feedback.Rating);
                eventData.Properties.Add("SubmittedAt", feedback.SubmittedAt);

                await _producerClient.SendAsync(new[] { eventData });

                _logger.LogInformation("Feedback sent to Event Hub for session {SessionId}", feedback.SessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending feedback to Event Hub");
                throw;
            }
        }

        public async Task SendBatchFeedbackAsync(List<SessionFeedback> feedbacks)
        {
            try
            {
                using var eventBatch = await _producerClient.CreateBatchAsync();

                foreach (var feedback in feedbacks)
                {
                    var eventData = new EventData(
                        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(feedback)));
                    
                    eventData.Properties.Add("SessionId", feedback.SessionId);
                    eventData.Properties.Add("Rating", feedback.Rating);

                    if (!eventBatch.TryAdd(eventData))
                    {
                        // Batch is full, send it and create a new one
                        await _producerClient.SendAsync(eventBatch);
                        eventBatch.Dispose();
                        
                        var newBatch = await _producerClient.CreateBatchAsync();
                        newBatch.TryAdd(eventData);
                    }
                }

                if (eventBatch.Count > 0)
                {
                    await _producerClient.SendAsync(eventBatch);
                }

                _logger.LogInformation("Batch of {Count} feedbacks sent to Event Hub", feedbacks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending batch feedback to Event Hub");
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _producerClient.DisposeAsync();
        }
    }
}
```

Add NuGet package:
```powershell
cd ../ConferenceHub
dotnet add package Azure.Messaging.EventHubs
```

**Bash**
```bash
cd ../ConferenceHub
dotnet add package Azure.Messaging.EventHubs
```

### Step 3: Register Service

Update `ConferenceHub/Program.cs`:
```csharp
// Configure Event Hub service
var eventHubConnectionString = builder.Configuration["EventHub:ConnectionString"];
var eventHubName = "session-feedback";
builder.Services.AddSingleton<IEventHubService>(sp => 
    new EventHubService(
        eventHubConnectionString!, 
        eventHubName, 
        sp.GetRequiredService<ILogger<EventHubService>>()));
```

---

## Part 4: Create Feedback Controller and Views

### Step 1: Create Feedback Controller

Create `ConferenceHub/Controllers/FeedbackController.cs`:
```csharp
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
```

### Step 2: Create Feedback View

Create `ConferenceHub/Views/Feedback/Submit.cshtml`:
```cshtml
@model ConferenceHub.Models.SessionFeedback

@{
    ViewData["Title"] = "Submit Feedback";
    var session = ViewBag.Session as ConferenceHub.Models.Session;
}

<div class="container mt-4">
    <div class="row justify-content-center">
        <div class="col-md-8">
            <div class="card">
                <div class="card-header bg-primary text-white">
                    <h3 class="mb-0"><i class="bi bi-star"></i> Submit Feedback</h3>
                </div>
                <div class="card-body">
                    <div class="alert alert-info">
                        <h5>@session.Title</h5>
                        <p class="mb-0">
                            <strong>Speaker:</strong> @session.Speaker<br />
                            <strong>Date:</strong> @session.StartTime.ToString("MMM dd, yyyy h:mm tt")
                        </p>
                    </div>

                    <form asp-action="Submit" method="post">
                        <input type="hidden" asp-for="SessionId" value="@session.Id" />

                        <div class="mb-4">
                            <label class="form-label">How would you rate this session?</label>
                            <div class="d-flex gap-2 justify-content-center mb-2">
                                @for (int i = 1; i <= 5; i++)
                                {
                                    var starValue = i;
                                    <label class="star-rating">
                                        <input type="radio" asp-for="Rating" value="@starValue" required />
                                        <i class="bi bi-star-fill" style="font-size: 2rem; cursor: pointer;"></i>
                                    </label>
                                }
                            </div>
                            <small class="text-muted d-block text-center">1 = Poor, 5 = Excellent</small>
                        </div>

                        <div class="mb-3">
                            <label asp-for="Comment" class="form-label">Comments (Optional)</label>
                            <textarea asp-for="Comment" class="form-control" rows="5" 
                                placeholder="Share your thoughts about this session..."></textarea>
                        </div>

                        <div class="mb-3 form-check">
                            <input type="checkbox" asp-for="IsRecommended" class="form-check-input" id="isRecommended" />
                            <label class="form-check-label" for="isRecommended">
                                I would recommend this session to others
                            </label>
                        </div>

                        <div class="mb-3">
                            <label class="form-label">Tags (Select all that apply)</label>
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="form-check">
                                        <input type="checkbox" name="Tags" value="Informative" class="form-check-input" id="tagInformative" />
                                        <label class="form-check-label" for="tagInformative">Informative</label>
                                    </div>
                                    <div class="form-check">
                                        <input type="checkbox" name="Tags" value="Engaging" class="form-check-input" id="tagEngaging" />
                                        <label class="form-check-label" for="tagEngaging">Engaging</label>
                                    </div>
                                    <div class="form-check">
                                        <input type="checkbox" name="Tags" value="Well-Organized" class="form-check-input" id="tagOrganized" />
                                        <label class="form-check-label" for="tagOrganized">Well-Organized</label>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="form-check">
                                        <input type="checkbox" name="Tags" value="Technical-Depth" class="form-check-input" id="tagTechnical" />
                                        <label class="form-check-label" for="tagTechnical">Technical Depth</label>
                                    </div>
                                    <div class="form-check">
                                        <input type="checkbox" name="Tags" value="Good-Examples" class="form-check-input" id="tagExamples" />
                                        <label class="form-check-label" for="tagExamples">Good Examples</label>
                                    </div>
                                    <div class="form-check">
                                        <input type="checkbox" name="Tags" value="Practical" class="form-check-input" id="tagPractical" />
                                        <label class="form-check-label" for="tagPractical">Practical</label>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div class="d-flex justify-content-between">
                            <a asp-controller="Sessions" asp-action="Details" asp-route-id="@session.Id" class="btn btn-secondary">
                                Cancel
                            </a>
                            <button type="submit" class="btn btn-primary">
                                <i class="bi bi-send"></i> Submit Feedback
                            </button>
                        </div>
                    </form>
                </div>
            </div>
        </div>
    </div>
</div>

<style>
    .star-rating input[type="radio"] {
        display: none;
    }
    .star-rating i {
        color: #ddd;
        transition: color 0.2s;
    }
    .star-rating input[type="radio"]:checked ~ i,
    .star-rating:hover i {
        color: #ffc107;
    }
</style>
```

---

## Part 5: Create Event Hub Consumer Function

### Step 1: Create Feedback Processor Function

Create `ConferenceHubFunctions/ProcessFeedback.cs`:
```csharp
using System.Text;
using System.Text.Json;
using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ConferenceHubFunctions
{
    public class ProcessFeedback
    {
        private readonly ILogger _logger;

        public ProcessFeedback(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ProcessFeedback>();
        }

        [Function("ProcessFeedback")]
        public async Task Run(
            [EventHubTrigger("session-feedback", Connection = "EventHubConnectionString", ConsumerGroup = "feedback-processor")] EventData[] events)
        {
            foreach (var eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.EventBody.ToArray());
                    var feedback = JsonSerializer.Deserialize<SessionFeedback>(messageBody);

                    if (feedback != null)
                    {
                        _logger.LogInformation("Processing feedback for session {SessionId}: Rating {Rating}/5",
                            feedback.SessionId, feedback.Rating);

                        // Process feedback
                        await ProcessFeedbackAsync(feedback);

                        // Store aggregated statistics
                        await UpdateStatisticsAsync(feedback);

                        // Send notifications for low ratings
                        if (feedback.Rating <= 2)
                        {
                            await SendAlertForLowRatingAsync(feedback);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing feedback event");
                }
            }

            await Task.CompletedTask;
        }

        private async Task ProcessFeedbackAsync(SessionFeedback feedback)
        {
            // TODO: Store feedback in Cosmos DB or Table Storage
            // TODO: Update real-time dashboard
            _logger.LogInformation("Feedback processed: {FeedbackId}", feedback.Id);
            await Task.Delay(50); // Simulate processing
        }

        private async Task UpdateStatisticsAsync(SessionFeedback feedback)
        {
            // TODO: Update aggregated statistics in cache/database
            // TODO: Calculate average rating, recommendation percentage
            _logger.LogInformation("Statistics updated for session {SessionId}", feedback.SessionId);
            await Task.Delay(50);
        }

        private async Task SendAlertForLowRatingAsync(SessionFeedback feedback)
        {
            _logger.LogWarning("Low rating alert: Session {SessionId} received {Rating}/5 stars",
                feedback.SessionId, feedback.Rating);
            
            // TODO: Send notification to organizers
            // TODO: Trigger follow-up workflow
            await Task.Delay(50);
        }

        private class SessionFeedback
        {
            public string Id { get; set; } = string.Empty;
            public int SessionId { get; set; }
            public string SessionTitle { get; set; } = string.Empty;
            public string AttendeeEmail { get; set; } = string.Empty;
            public string AttendeeName { get; set; } = string.Empty;
            public int Rating { get; set; }
            public string? Comment { get; set; }
            public DateTime SubmittedAt { get; set; }
            public bool IsRecommended { get; set; }
            public List<string> Tags { get; set; } = new();
        }
    }
}
```

### Step 2: Configure Function App Settings

```powershell
# Get Event Hub connection string from Key Vault
$eventHubConnectionString = az keyvault secret show `
  --vault-name $keyVaultName `
  --name "EventHub--ConnectionString" `
  --query value `
  --output tsv

# Add to Function App settings
az functionapp config appsettings set `
  --name $functionAppName `
  --resource-group $resourceGroupNameName `
  --settings EventHubConnectionString="$eventHubConnectionString"
```

**Bash**
```bash
# Get Event Hub connection string from Key Vault
eventHubConnectionString=$(az keyvault secret show \
  --vault-name $keyVaultName \
  --name "EventHub--ConnectionString" \
  --query value \
  --output tsv)

# Add to Function App settings
az functionapp config appsettings set \
  --name $functionAppName \
  --resource-group $resourceGroupNameName \
  --settings EventHubConnectionString="$eventHubConnectionString"
```

### Step 3: Deploy Function

```powershell
cd ConferenceHubFunctions
func azure functionapp publish $functionAppName
```

**Bash**
```bash
cd ConferenceHubFunctions
func azure functionapp publish $functionAppName
```

---

## Part 6: Test Event-Driven Architecture

### Test 1: Test Event Grid (Blob Upload)

```powershell
# Upload a test file to trigger Event Grid
$testFile = "test-slide.pdf"
New-Item -Path $testFile -ItemType File -Force
Set-Content -Path $testFile -Value "Test content"

az storage blob upload `
  --account-name $storageAccountName `
  --container-name speaker-slides `
  --name "session-1/test-slide.pdf" `
  --file $testFile

# Check Function App logs
az functionapp log tail `
  --name $functionAppName `
  --resource-group $resourceGroupNameName
```

**Bash**
```bash
# Upload a test file to trigger Event Grid
testFile=$("test-slide.pdf")
New-Item -Path $testFile -ItemType File -Force
Set-Content -Path $testFile -Value "Test content"

az storage blob upload \
  --account-name $storageAccountName \
  --container-name speaker-slides \
  --name "session-1/test-slide.pdf" \
  --file $testFile

# Check Function App logs
az functionapp log tail \
  --name $functionAppName \
  --resource-group $resourceGroupNameName
```

### Test 2: Test Event Hub (Submit Feedback)

1. Navigate to a completed session details page
2. Click "Submit Feedback"
3. Fill out the feedback form with rating and comments
4. Submit the form
5. Check Function App logs to see ProcessFeedback execution

Alternative - Send test event programmatically:
```powershell
# Install Azure.Messaging.EventHubs for testing
# Create test script to send feedback
```

**Bash**
```bash
# Install Azure.Messaging.EventHubs for testing
# Create test script to send feedback
```

### Test 3: Monitor Events

```powershell
# View Event Hub metrics
az monitor metrics list `
  --resource "/subscriptions/YOUR_SUB_ID/resourceGroups/$resourceGroupNameName/providers/Microsoft.EventHub/namespaces/$eventHubNamespaceName" `
  --metric "IncomingMessages" `
  --start-time 2024-01-01T00:00:00Z

# View Event Grid metrics
az monitor metrics list `
  --resource $storageAccountId `
  --metric "BlobCount" `
  --start-time 2024-01-01T00:00:00Z
```

**Bash**
```bash
# View Event Hub metrics
az monitor metrics list \
  --resource "/subscriptions/YOUR_SUB_ID/resourceGroups/$resourceGroupNameName/providers/Microsoft.EventHub/namespaces/$eventHubNamespaceName" \
  --metric "IncomingMessages" \
  --start-time 2024-01-01T00:00:00Z

# View Event Grid metrics
az monitor metrics list \
  --resource $storageAccountId \
  --metric "BlobCount" \
  --start-time 2024-01-01T00:00:00Z
```

---

## Part 7: Deploy and Configure

### Step 1: Update Web App Configuration

```powershell
# Add Event Hub connection string to Web App
az webapp config appsettings set `
  --name conferencehub-demo-az204reinke `
  --resource-group $resourceGroupNameName `
  --settings EventHub__ConnectionString="@Microsoft.KeyVault(SecretUri=https://$keyVaultName.vault.azure.net/secrets/EventHub--ConnectionString/)"
```

**Bash**
```bash
# Add Event Hub connection string to Web App
az webapp config appsettings set \
  --name conferencehub-demo-az204reinke \
  --resource-group $resourceGroupNameName \
  --settings EventHub__ConnectionString="@Microsoft.KeyVault(SecretUri=https://$keyVaultName.vault.azure.net/secrets/EventHub--ConnectionString/)"
```

### Step 2: Deploy Updated Web App

```powershell
cd ConferenceHub
dotnet publish -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./app.zip -Force
az webapp deployment source config-zip `
  --resource-group $resourceGroupNameName `
  --name conferencehub-demo-az204reinke `
  --src ./app.zip
```

**Bash**
```bash
cd ConferenceHub
dotnet publish -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./app.zip -Force
az webapp deployment source config-zip \
  --resource-group $resourceGroupNameName \
  --name conferencehub-demo-az204reinke \
  --src ./app.zip
```

---

## Summary

You've successfully:
- ✅ Created Event Grid system topic for Storage events
- ✅ Implemented Event Grid subscription for blob uploads
- ✅ Created Azure Event Hub for streaming feedback data
- ✅ Built Event Hub producer service to send feedback events
- ✅ Created Event Hub consumer function to process feedback
- ✅ Implemented feedback submission UI
- ✅ Set up event-driven workflows and notifications

## Next Steps

In **Learning Path 10**, you'll:
- Implement **Azure Service Bus** queues for reliable message processing
- Use **Storage Queues** for lightweight async tasks
- Create queue processors and message handlers
- Handle dead-letter queues and poison messages

---

## Troubleshooting

### Event Grid subscription validation fails
- Ensure webhook endpoint returns validation response correctly
- Check function authorization level (use Function level, not Anonymous)
- Verify function URL is publicly accessible

### Events not being received
- Check Event Grid subscription status and filters
- Verify subject filters match blob paths
- Review Function App logs for errors
- Test with Event Grid Viewer tool

### Event Hub connection issues
- Verify connection string is correct
- Check Event Hub namespace and hub name
- Ensure consumer group exists
- Verify Function App has correct binding configuration

### Feedback not processing
- Check Event Hub consumer function logs
- Verify connection string in Function App settings
- Ensure partition count and consumer group are configured
- Check for throttling or processing errors

## Azure DevOps Pipeline (Incremental Deployment)
- Pipeline: `Learning Path/09-EventGrid/azure-pipelines.yml`
- Bicep: `Learning Path/09-EventGrid/infra.bicep`
- Required variables: `azureSubscription`, `resourceGroupName`, `location`, `mainWebAppName`, `storageAccountName`, `cosmosAccountName`, `cosmosDatabaseName`, `functionAppName`, `keyVaultUri`, `appConfigEndpoint`, `azureAdTenantId`, `azureAdClientId`, `AzureAdClientSecret`, `apiManagementGatewayUrl`, `ApiManagementSubscriptionKey`, `eventHubNamespaceName`, `eventHubName`, `eventGridTopicName`
- Notes: The pipeline provisions Event Hub + Event Grid Topic and updates web app settings for `EventHub__ConnectionString` and `EventHub__Name`.
