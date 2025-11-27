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
