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
