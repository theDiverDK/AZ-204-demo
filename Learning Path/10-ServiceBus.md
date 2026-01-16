# Learning Path 10: Service Bus & Queue Storage

## Overview
In this learning path, you'll implement reliable message-based communication using Azure Service Bus for enterprise messaging scenarios and Azure Storage Queues for lightweight asynchronous task processing.

## What You'll Build
1. **Service Bus Queue**: Process registration confirmations with guaranteed delivery
2. **Service Bus Topics**: Pub/sub for notifications (email, SMS, mobile push)
3. **Storage Queues**: Handle simple background tasks (thumbnail generation, cleanup)
4. **Dead Letter Queues**: Handle poison messages and failures

## Prerequisites
- Completed Learning Path 1-9
- Azure subscription
- Deployed Web App and Azure Functions

---

## Part 1: Create Azure Service Bus

### Step 1: Create Service Bus Namespace

```powershell
# Create Service Bus namespace
az servicebus namespace create `
  --name sb-conferencehub `
  --resource-group rg-conferencehub `
  --location eastus `
  --sku Standard

Write-Host "Service Bus namespace created"
```

### Step 2: Create Queue for Registrations

```powershell
# Create queue for registration processing
az servicebus queue create `
  --namespace-name sb-conferencehub `
  --resource-group rg-conferencehub `
  --name registration-queue `
  --max-delivery-count 5 `
  --lock-duration PT5M `
  --default-message-time-to-live P14D `
  --enable-dead-lettering-on-message-expiration true

Write-Host "Registration queue created"
```

### Step 3: Create Topic for Notifications

```powershell
# Create topic for notifications
az servicebus topic create `
  --namespace-name sb-conferencehub `
  --resource-group rg-conferencehub `
  --name notification-topic `
  --default-message-time-to-live P14D

# Create subscription for email notifications
az servicebus topic subscription create `
  --namespace-name sb-conferencehub `
  --resource-group rg-conferencehub `
  --topic-name notification-topic `
  --name email-subscription

# Create subscription for SMS notifications
az servicebus topic subscription create `
  --namespace-name sb-conferencehub `
  --resource-group rg-conferencehub `
  --topic-name notification-topic `
  --name sms-subscription `
  --max-delivery-count 3

# Create subscription for mobile push notifications
az servicebus topic subscription create `
  --namespace-name sb-conferencehub `
  --resource-group rg-conferencehub `
  --topic-name notification-topic `
  --name mobile-subscription

Write-Host "Notification topic and subscriptions created"
```

### Step 4: Get Connection String

```powershell
# Get Service Bus connection string
$serviceBusConnectionString = az servicebus namespace authorization-rule keys list `
  --namespace-name sb-conferencehub `
  --resource-group rg-conferencehub `
  --name RootManageSharedAccessKey `
  --query primaryConnectionString `
  --output tsv

Write-Host "Service Bus Connection String: $serviceBusConnectionString"

# Store in Key Vault
az keyvault secret set `
  --vault-name kv-conferencehub-az204 `
  --name "ServiceBus--ConnectionString" `
  --value $serviceBusConnectionString
```

---

## Part 2: Create Storage Queue

### Step 1: Create Storage Queue

```powershell
# Get storage account key
$storageKey = az storage account keys list `
  --account-name stconferencehub `
  --resource-group rg-conferencehub `
  --query "[0].value" `
  --output tsv

# Create queue for background tasks
az storage queue create `
  --name background-tasks `
  --account-name stconferencehub `
  --account-key $storageKey

# Create queue for slide processing
az storage queue create `
  --name slide-processing `
  --account-name stconferencehub `
  --account-key $storageKey

Write-Host "Storage queues created"
```

---

## Part 3: Implement Service Bus Producer

### Step 1: Add NuGet Packages

```powershell
cd ConferenceHub
dotnet add package Azure.Messaging.ServiceBus
```

### Step 2: Create Message Models

Create `ConferenceHub/Models/QueueMessages.cs`:
```csharp
namespace ConferenceHub.Models
{
    public class RegistrationMessage
    {
        public int RegistrationId { get; set; }
        public int SessionId { get; set; }
        public string SessionTitle { get; set; } = string.Empty;
        public string AttendeeName { get; set; } = string.Empty;
        public string AttendeeEmail { get; set; } = string.Empty;
        public DateTime SessionStartTime { get; set; }
        public string Room { get; set; } = string.Empty;
        public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    }

    public class NotificationMessage
    {
        public string NotificationType { get; set; } = string.Empty; // Email, SMS, Mobile
        public string Recipient { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

### Step 3: Create Service Bus Service

Create `ConferenceHub/Services/IServiceBusService.cs`:
```csharp
using ConferenceHub.Models;

namespace ConferenceHub.Services
{
    public interface IServiceBusService
    {
        Task SendRegistrationMessageAsync(RegistrationMessage message);
        Task PublishNotificationAsync(NotificationMessage notification);
    }
}
```

Create `ConferenceHub/Services/ServiceBusService.cs`:
```csharp
using Azure.Messaging.ServiceBus;
using ConferenceHub.Models;
using System.Text;
using System.Text.Json;

namespace ConferenceHub.Services
{
    public class ServiceBusService : IServiceBusService, IAsyncDisposable
    {
        private readonly ServiceBusClient _client;
        private readonly ServiceBusSender _queueSender;
        private readonly ServiceBusSender _topicSender;
        private readonly ILogger<ServiceBusService> _logger;

        public ServiceBusService(string connectionString, ILogger<ServiceBusService> logger)
        {
            _client = new ServiceBusClient(connectionString);
            _queueSender = _client.CreateSender("registration-queue");
            _topicSender = _client.CreateSender("notification-topic");
            _logger = logger;
        }

        public async Task SendRegistrationMessageAsync(RegistrationMessage message)
        {
            try
            {
                var messageBody = JsonSerializer.Serialize(message);
                var serviceBusMessage = new ServiceBusMessage(Encoding.UTF8.GetBytes(messageBody))
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "application/json",
                    Subject = "RegistrationConfirmation"
                };

                // Add custom properties
                serviceBusMessage.ApplicationProperties.Add("SessionId", message.SessionId);
                serviceBusMessage.ApplicationProperties.Add("AttendeeEmail", message.AttendeeEmail);
                
                // Schedule message for immediate processing
                serviceBusMessage.ScheduledEnqueueTime = DateTimeOffset.UtcNow;

                await _queueSender.SendMessageAsync(serviceBusMessage);

                _logger.LogInformation("Registration message sent to queue for {Email}", message.AttendeeEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending registration message to Service Bus");
                throw;
            }
        }

        public async Task PublishNotificationAsync(NotificationMessage notification)
        {
            try
            {
                var messageBody = JsonSerializer.Serialize(notification);
                var serviceBusMessage = new ServiceBusMessage(Encoding.UTF8.GetBytes(messageBody))
                {
                    MessageId = Guid.NewGuid().ToString(),
                    ContentType = "application/json",
                    Subject = notification.NotificationType
                };

                // Add properties for subscription filtering
                serviceBusMessage.ApplicationProperties.Add("NotificationType", notification.NotificationType);
                serviceBusMessage.ApplicationProperties.Add("Recipient", notification.Recipient);

                await _topicSender.SendMessageAsync(serviceBusMessage);

                _logger.LogInformation("Notification published to topic: {Type} for {Recipient}",
                    notification.NotificationType, notification.Recipient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing notification to Service Bus topic");
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _queueSender.DisposeAsync();
            await _topicSender.DisposeAsync();
            await _client.DisposeAsync();
        }
    }
}
```

### Step 4: Register Service

Update `ConferenceHub/Program.cs`:
```csharp
// Configure Service Bus
var serviceBusConnectionString = builder.Configuration["ServiceBus:ConnectionString"];
builder.Services.AddSingleton<IServiceBusService>(sp => 
    new ServiceBusService(
        serviceBusConnectionString!, 
        sp.GetRequiredService<ILogger<ServiceBusService>>()));
```

---

## Part 4: Implement Storage Queue Producer

### Step 1: Create Storage Queue Service

Create `ConferenceHub/Services/IStorageQueueService.cs`:
```csharp
namespace ConferenceHub.Services
{
    public interface IStorageQueueService
    {
        Task EnqueueBackgroundTaskAsync(string taskType, string taskData);
        Task EnqueueSlideProcessingAsync(int sessionId, string blobUrl);
    }
}
```

Create `ConferenceHub/Services/StorageQueueService.cs`:
```csharp
using Azure.Storage.Queues;
using System.Text;
using System.Text.Json;

namespace ConferenceHub.Services
{
    public class StorageQueueService : IStorageQueueService
    {
        private readonly QueueClient _backgroundTaskQueue;
        private readonly QueueClient _slideProcessingQueue;
        private readonly ILogger<StorageQueueService> _logger;

        public StorageQueueService(string connectionString, ILogger<StorageQueueService> logger)
        {
            _backgroundTaskQueue = new QueueClient(connectionString, "background-tasks");
            _slideProcessingQueue = new QueueClient(connectionString, "slide-processing");
            _logger = logger;

            // Ensure queues exist
            _backgroundTaskQueue.CreateIfNotExists();
            _slideProcessingQueue.CreateIfNotExists();
        }

        public async Task EnqueueBackgroundTaskAsync(string taskType, string taskData)
        {
            try
            {
                var message = new
                {
                    TaskType = taskType,
                    TaskData = taskData,
                    EnqueuedAt = DateTime.UtcNow
                };

                var messageJson = JsonSerializer.Serialize(message);
                var messageBytes = Encoding.UTF8.GetBytes(messageJson);
                var base64Message = Convert.ToBase64String(messageBytes);

                await _backgroundTaskQueue.SendMessageAsync(base64Message);

                _logger.LogInformation("Background task enqueued: {TaskType}", taskType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enqueueing background task");
                throw;
            }
        }

        public async Task EnqueueSlideProcessingAsync(int sessionId, string blobUrl)
        {
            try
            {
                var message = new
                {
                    SessionId = sessionId,
                    BlobUrl = blobUrl,
                    ProcessingType = "ThumbnailGeneration",
                    EnqueuedAt = DateTime.UtcNow
                };

                var messageJson = JsonSerializer.Serialize(message);
                var messageBytes = Encoding.UTF8.GetBytes(messageJson);
                var base64Message = Convert.ToBase64String(messageBytes);

                // Message will be invisible for 10 seconds (processing delay)
                await _slideProcessingQueue.SendMessageAsync(base64Message, visibilityTimeout: TimeSpan.FromSeconds(10));

                _logger.LogInformation("Slide processing task enqueued for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enqueueing slide processing task");
                throw;
            }
        }
    }
}
```

Register service:
```csharp
builder.Services.AddSingleton<IStorageQueueService>(sp => 
    new StorageQueueService(
        storageConnectionString!, 
        sp.GetRequiredService<ILogger<StorageQueueService>>()));
```

---

## Part 5: Update Controllers to Use Queues

### Step 1: Update SessionsController

Update `Controllers/SessionsController.cs` to use Service Bus:
```csharp
private readonly IServiceBusService _serviceBusService;

public SessionsController(
    IDataService dataService,
    IHttpClientFactory httpClientFactory,
    IAuditLogService auditLogService,
    IServiceBusService serviceBusService,
    // ... other dependencies
{
    _serviceBusService = serviceBusService;
    // ...
}

[HttpPost]
[Authorize]
public async Task<IActionResult> Register(int sessionId, string attendeeName, string attendeeEmail)
{
    // ... existing validation ...

    var registration = new Registration
    {
        SessionId = sessionId,
        AttendeeName = userName,
        AttendeeEmail = userEmail
    };

    await _dataService.AddRegistrationAsync(registration);
    await _auditLogService.LogRegistrationAsync(sessionId, session.Title, userName, userEmail);

    // Send message to Service Bus queue instead of calling Function directly
    var registrationMessage = new RegistrationMessage
    {
        RegistrationId = registration.Id,
        SessionId = sessionId,
        SessionTitle = session.Title,
        AttendeeName = userName,
        AttendeeEmail = userEmail,
        SessionStartTime = session.StartTime,
        Room = session.Room
    };

    await _serviceBusService.SendRegistrationMessageAsync(registrationMessage);

    // Publish notification to topic
    var notificationMessage = new NotificationMessage
    {
        NotificationType = "Email",
        Recipient = userEmail,
        Subject = $"Registration Confirmed: {session.Title}",
        Body = $"You are registered for {session.Title} on {session.StartTime:MMM dd, yyyy}",
        Metadata = new Dictionary<string, string>
        {
            { "SessionId", sessionId.ToString() },
            { "RegistrationId", registration.Id.ToString() }
        }
    };

    await _serviceBusService.PublishNotificationAsync(notificationMessage);

    TempData["Success"] = "Successfully registered! Confirmation will be sent shortly.";
    return RedirectToAction(nameof(Details), new { id = sessionId });
}
```

### Step 2: Update OrganizerController for Slide Processing

Update `Controllers/OrganizerController.cs`:
```csharp
private readonly IStorageQueueService _storageQueueService;

public OrganizerController(
    // ... existing dependencies,
    IStorageQueueService storageQueueService)
{
    _storageQueueService = storageQueueService;
    // ...
}

[HttpPost]
[ValidateAntiForgeryToken]
[FeatureGate("SlideUpload")]
public async Task<IActionResult> UploadSlides(int id, IFormFile slideFile)
{
    // ... existing upload logic ...

    // Upload to blob storage
    using var stream = slideFile.OpenReadStream();
    var blobUrl = await _blobStorageService.UploadSlideAsync(
        id, slideFile.FileName, stream, slideFile.ContentType);

    session.SlideUrl = blobUrl;
    session.SlideUploadedAt = DateTime.UtcNow;
    await _dataService.UpdateSessionAsync(session);
    await _auditLogService.LogSlideUploadAsync(id, session.Title, session.Speaker);

    // Enqueue slide processing task
    await _storageQueueService.EnqueueSlideProcessingAsync(id, blobUrl);

    TempData["Success"] = "Slides uploaded successfully! Processing will begin shortly.";
    return RedirectToAction(nameof(Index));
}
```

---

## Part 6: Create Service Bus Consumer Functions

### Step 1: Create Registration Queue Processor

Create `ConferenceHubFunctions/ProcessRegistrationQueue.cs`:
```csharp
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ConferenceHubFunctions
{
    public class ProcessRegistrationQueue
    {
        private readonly ILogger<ProcessRegistrationQueue> _logger;

        public ProcessRegistrationQueue(ILogger<ProcessRegistrationQueue> logger)
        {
            _logger = logger;
        }

        [Function("ProcessRegistrationQueue")]
        public async Task Run(
            [ServiceBusTrigger("registration-queue", Connection = "ServiceBusConnectionString")] ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            try
            {
                _logger.LogInformation("Processing registration message: {MessageId}", message.MessageId);

                var messageBody = Encoding.UTF8.GetString(message.Body);
                var registration = JsonSerializer.Deserialize<RegistrationMessage>(messageBody);

                if (registration != null)
                {
                    // Process registration
                    await ProcessRegistrationAsync(registration);

                    // Complete the message
                    await messageActions.CompleteMessageAsync(message);

                    _logger.LogInformation("Registration processed successfully: {Email}", registration.AttendeeEmail);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid message format - moving to dead letter queue");
                await messageActions.DeadLetterMessageAsync(message, "InvalidFormat", "Message is not valid JSON");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing registration message");
                
                // Increment delivery count and retry
                if (message.DeliveryCount >= 3)
                {
                    _logger.LogWarning("Max delivery count reached - moving to dead letter queue");
                    await messageActions.DeadLetterMessageAsync(message, "MaxDeliveryCountExceeded", ex.Message);
                }
                else
                {
                    // Abandon message for retry
                    await messageActions.AbandonMessageAsync(message);
                }
            }
        }

        private async Task ProcessRegistrationAsync(RegistrationMessage registration)
        {
            // Send confirmation email
            _logger.LogInformation("Sending confirmation email to {Email}", registration.AttendeeEmail);
            
            // Simulate email sending
            await Task.Delay(100);

            // TODO: Integrate with actual email service (SendGrid, etc.)
            // TODO: Store confirmation record in database
        }

        private class RegistrationMessage
        {
            public int RegistrationId { get; set; }
            public int SessionId { get; set; }
            public string SessionTitle { get; set; } = string.Empty;
            public string AttendeeName { get; set; } = string.Empty;
            public string AttendeeEmail { get; set; } = string.Empty;
            public DateTime SessionStartTime { get; set; }
            public string Room { get; set; } = string.Empty;
            public DateTime EnqueuedAt { get; set; }
        }
    }
}
```

### Step 2: Create Notification Topic Subscribers

Create `ConferenceHubFunctions/ProcessEmailNotifications.cs`:
```csharp
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ConferenceHubFunctions
{
    public class ProcessEmailNotifications
    {
        private readonly ILogger<ProcessEmailNotifications> _logger;

        public ProcessEmailNotifications(ILogger<ProcessEmailNotifications> logger)
        {
            _logger = logger;
        }

        [Function("ProcessEmailNotifications")]
        public async Task Run(
            [ServiceBusTrigger("notification-topic", "email-subscription", Connection = "ServiceBusConnectionString")] ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            try
            {
                var messageBody = Encoding.UTF8.GetString(message.Body);
                var notification = JsonSerializer.Deserialize<NotificationMessage>(messageBody);

                if (notification != null && notification.NotificationType == "Email")
                {
                    _logger.LogInformation("Sending email to {Recipient}: {Subject}", 
                        notification.Recipient, notification.Subject);

                    await SendEmailAsync(notification);
                    await messageActions.CompleteMessageAsync(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing email notification");
                await messageActions.DeadLetterMessageAsync(message, "ProcessingError", ex.Message);
            }
        }

        private async Task SendEmailAsync(NotificationMessage notification)
        {
            // TODO: Integrate with SendGrid, Azure Communication Services, etc.
            _logger.LogInformation("Email sent to {Recipient}", notification.Recipient);
            await Task.Delay(50);
        }

        private class NotificationMessage
        {
            public string NotificationType { get; set; } = string.Empty;
            public string Recipient { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
            public Dictionary<string, string> Metadata { get; set; } = new();
        }
    }
}
```

Create `ConferenceHubFunctions/ProcessSMSNotifications.cs`:
```csharp
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ConferenceHubFunctions
{
    public class ProcessSMSNotifications
    {
        private readonly ILogger<ProcessSMSNotifications> _logger;

        public ProcessSMSNotifications(ILogger<ProcessSMSNotifications> logger)
        {
            _logger = logger;
        }

        [Function("ProcessSMSNotifications")]
        public async Task Run(
            [ServiceBusTrigger("notification-topic", "sms-subscription", Connection = "ServiceBusConnectionString")] ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            try
            {
                var messageBody = Encoding.UTF8.GetString(message.Body);
                var notification = JsonSerializer.Deserialize<NotificationMessage>(messageBody);

                if (notification != null && notification.NotificationType == "SMS")
                {
                    _logger.LogInformation("Sending SMS to {Recipient}", notification.Recipient);

                    await SendSMSAsync(notification);
                    await messageActions.CompleteMessageAsync(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SMS notification");
                await messageActions.DeadLetterMessageAsync(message, "ProcessingError", ex.Message);
            }
        }

        private async Task SendSMSAsync(NotificationMessage notification)
        {
            // TODO: Integrate with Twilio, Azure Communication Services, etc.
            _logger.LogInformation("SMS sent to {Recipient}", notification.Recipient);
            await Task.Delay(50);
        }

        private class NotificationMessage
        {
            public string NotificationType { get; set; } = string.Empty;
            public string Recipient { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
        }
    }
}
```

Add NuGet package:
```powershell
cd ConferenceHubFunctions
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.ServiceBus
```

---

## Part 7: Create Storage Queue Consumer Function

Create `ConferenceHubFunctions/ProcessSlideQueue.cs`:
```csharp
using System.Text;
using System.Text.Json;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ConferenceHubFunctions
{
    public class ProcessSlideQueue
    {
        private readonly ILogger<ProcessSlideQueue> _logger;

        public ProcessSlideQueue(ILogger<ProcessSlideQueue> logger)
        {
            _logger = logger;
        }

        [Function("ProcessSlideQueue")]
        public async Task Run(
            [QueueTrigger("slide-processing", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            try
            {
                _logger.LogInformation("Processing slide: DequeueCount = {DequeueCount}", message.DequeueCount);

                var messageBody = Encoding.UTF8.GetString(Convert.FromBase64String(message.MessageText));
                var slideTask = JsonSerializer.Deserialize<SlideProcessingTask>(messageBody);

                if (slideTask != null)
                {
                    await ProcessSlideAsync(slideTask);
                    _logger.LogInformation("Slide processed for session {SessionId}", slideTask.SessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing slide - DequeueCount: {Count}", message.DequeueCount);
                
                // Message will automatically go to poison queue after 5 dequeue attempts
                throw;
            }
        }

        private async Task ProcessSlideAsync(SlideProcessingTask task)
        {
            _logger.LogInformation("Generating thumbnail for {BlobUrl}", task.BlobUrl);
            
            // TODO: Download blob
            // TODO: Generate thumbnail
            // TODO: Upload thumbnail to storage
            // TODO: Update session metadata
            
            await Task.Delay(200); // Simulate processing
        }

        private class SlideProcessingTask
        {
            public int SessionId { get; set; }
            public string BlobUrl { get; set; } = string.Empty;
            public string ProcessingType { get; set; } = string.Empty;
            public DateTime EnqueuedAt { get; set; }
        }
    }
}
```

Add NuGet package:
```powershell
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues
```

---

## Part 8: Configure and Deploy

### Step 1: Configure Function App Settings

```powershell
# Add Service Bus connection string
az functionapp config appsettings set `
  --name func-conferencehub-az204reinke `
  --resource-group rg-conferencehub `
  --settings ServiceBusConnectionString="@Microsoft.KeyVault(SecretUri=https://kv-conferencehub-az204.vault.azure.net/secrets/ServiceBus--ConnectionString/)"
```

### Step 2: Deploy Functions

```powershell
cd ConferenceHubFunctions
func azure functionapp publish func-conferencehub-az204reinke
```

### Step 3: Deploy Web App

```powershell
cd ../ConferenceHub
dotnet publish -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./app.zip -Force
az webapp deployment source config-zip `
  --resource-group rg-conferencehub `
  --name conferencehub-demo-az204reinke `
  --src ./app.zip
```

---

## Part 9: Monitor and Handle Dead Letter Queues

### View Dead Letter Queue Messages

```powershell
# Install Service Bus Explorer or use Azure Portal
# Navigate to Service Bus → Queues → registration-queue → Dead-letter queue

# View dead letter messages using Azure CLI (requires additional setup)
# Or use Service Bus Explorer desktop application
```

### Create Dead Letter Queue Processor

Create `ConferenceHubFunctions/ProcessDeadLetterQueue.cs`:
```csharp
using System.Text;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ConferenceHubFunctions
{
    public class ProcessDeadLetterQueue
    {
        private readonly ILogger<ProcessDeadLetterQueue> _logger;

        public ProcessDeadLetterQueue(ILogger<ProcessDeadLetterQueue> logger)
        {
            _logger = logger;
        }

        [Function("ProcessDeadLetterQueue")]
        public async Task Run(
            [ServiceBusTrigger("registration-queue/$DeadLetterQueue", Connection = "ServiceBusConnectionString")] ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            _logger.LogWarning("Processing dead letter message: {MessageId}", message.MessageId);
            _logger.LogWarning("Dead letter reason: {Reason}", message.DeadLetterReason);
            _logger.LogWarning("Dead letter description: {Description}", message.DeadLetterErrorDescription);

            var messageBody = Encoding.UTF8.GetString(message.Body);
            _logger.LogWarning("Message body: {Body}", messageBody);

            // TODO: Log to monitoring system
            // TODO: Send alert to operations team
            // TODO: Store for manual review

            await messageActions.CompleteMessageAsync(message);
        }
    }
}
```

---

## Part 10: Test Message Processing

### Test Service Bus Queue

```powershell
# Register for a session through the web app
# Monitor Function App logs to see message processing

# Check queue metrics
az servicebus queue show `
  --namespace-name sb-conferencehub `
  --resource-group rg-conferencehub `
  --name registration-queue `
  --query "{ActiveMessages:countDetails.activeMessageCount, DeadLetter:countDetails.deadLetterMessageCount}"
```

### Test Service Bus Topic

```powershell
# View subscription metrics
az servicebus topic subscription show `
  --namespace-name sb-conferencehub `
  --resource-group rg-conferencehub `
  --topic-name notification-topic `
  --name email-subscription `
  --query "countDetails"
```

### Test Storage Queue

```powershell
# View queue messages
az storage queue peek `
  --name slide-processing `
  --account-name stconferencehub `
  --num-messages 10
```

---

## Summary

You've successfully:
- ✅ Created Azure Service Bus namespace with queues and topics
- ✅ Implemented Service Bus producers for reliable messaging
- ✅ Created pub/sub pattern with topics and subscriptions
- ✅ Implemented Storage Queues for lightweight background tasks
- ✅ Built Service Bus consumer functions with error handling
- ✅ Implemented dead letter queue processing
- ✅ Configured poison message handling
- ✅ Monitored message processing and queue metrics

## Next Steps

In **Learning Path 11** (Final), you'll:
- Implement **Azure Application Insights** for comprehensive monitoring
- Add custom telemetry and metrics
- Create dashboards and workbooks
- Set up alerts and notifications
- Implement distributed tracing

---

## Troubleshooting

### Messages not being processed
- Check Service Bus connection string in Function App settings
- Verify queue/topic names match configuration
- Review Function App logs for binding errors
- Ensure managed identity has correct permissions

### Messages going to dead letter queue
- Review dead letter reason and description
- Check message format and deserialization
- Verify business logic doesn't throw unhandled exceptions
- Consider increasing max delivery count

### Storage queue messages not processing
- Verify storage connection string is correct
- Check queue name matches trigger configuration
- Review poison queue for failed messages
- Ensure message format is base64 encoded

### High message latency
- Check Service Bus namespace tier (Basic vs Standard)
- Review function execution time and timeout settings
- Consider scaling out Function App
- Monitor queue length and processing rate

## Azure DevOps Pipeline (Incremental Deployment)
- Pipeline: `Learning Path/10-ServiceBus/azure-pipelines.yml`
- Bicep: `Learning Path/10-ServiceBus/infra.bicep`
- Required variables: `azureSubscription`, `resourceGroupName`, `location`, `mainWebAppName`, `storageAccountName`, `cosmosAccountName`, `cosmosDatabaseName`, `functionAppName`, `keyVaultUri`, `appConfigEndpoint`, `azureAdTenantId`, `azureAdClientId`, `AzureAdClientSecret`, `apiManagementGatewayUrl`, `ApiManagementSubscriptionKey`, `eventHubNamespaceName`, `eventHubName`, `serviceBusNamespaceName`, `serviceBusQueueName`, `serviceBusTopicName`
- Notes: The pipeline provisions Service Bus + Storage queues and updates web app settings for `ServiceBus__ConnectionString`.
