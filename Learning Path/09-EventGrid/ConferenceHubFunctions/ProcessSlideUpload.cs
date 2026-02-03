using System.Text.Json;
using Azure.Messaging.EventGrid;
using Microsoft.Azure.Functions.Worker;
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
        public async Task Run(
            [EventGridTrigger] EventGridEvent eventGridEvent)
        {
            _logger.LogInformation(
                "Processing Event Grid event {EventType} (Id: {Id})",
                eventGridEvent.EventType,
                eventGridEvent.Id);

            if (eventGridEvent.EventType == "Microsoft.Storage.BlobCreated")
            {
                var blobCreatedData = JsonSerializer.Deserialize<StorageBlobCreatedEventData>(
                    eventGridEvent.Data.ToString());

                _logger.LogInformation("Blob created: {BlobUrl}", blobCreatedData?.Url);
                await ProcessSlideAsync(blobCreatedData);
            }
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
