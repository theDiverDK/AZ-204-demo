using System.Text.Json;
using Azure.Storage.Queues;
using ConferenceHub.Models;
using Microsoft.Extensions.Options;

namespace ConferenceHub.Services
{
    public interface IThumbnailJobQueueService
    {
        Task EnqueueAsync(int sessionId, IEnumerable<string> slideUrls);
    }

    public class ThumbnailJobQueueService : IThumbnailJobQueueService
    {
        private readonly ThumbnailQueueConfig _config;
        private readonly ILogger<ThumbnailJobQueueService> _logger;

        public ThumbnailJobQueueService(IOptions<ThumbnailQueueConfig> config, ILogger<ThumbnailJobQueueService> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        public async Task EnqueueAsync(int sessionId, IEnumerable<string> slideUrls)
        {
            if (string.IsNullOrWhiteSpace(_config.ConnectionString) || string.IsNullOrWhiteSpace(_config.QueueName))
            {
                _logger.LogInformation("Thumbnail queue is not configured. Skipping thumbnail jobs.");
                return;
            }

            var queue = new QueueClient(_config.ConnectionString, _config.QueueName);
            await queue.CreateIfNotExistsAsync();

            foreach (var slideUrl in slideUrls)
            {
                var payload = JsonSerializer.Serialize(new
                {
                    sessionId,
                    slideUrl
                });

                await queue.SendMessageAsync(payload);
            }
        }
    }
}
