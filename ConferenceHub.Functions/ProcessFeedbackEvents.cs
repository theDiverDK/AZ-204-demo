using System.Text;
using System.Text.Json;
using Azure.Messaging.EventHubs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ConferenceHubFunctions
{
    public class ProcessFeedbackEvents
    {
        private readonly ILogger _logger;

        public ProcessFeedbackEvents(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ProcessFeedbackEvents>();
        }

        [Function("ProcessFeedbackEvents")]
        public async Task Run(
            [EventHubTrigger("%EventHubName%", Connection = "EventHubConnectionString", ConsumerGroup = "%FeedbackConsumerGroup%")]
            EventData[] events)
        {
            foreach (var eventData in events)
            {
                try
                {
                    var payload = Encoding.UTF8.GetString(eventData.EventBody.ToArray());
                    var parsed = JsonDocument.Parse(payload).RootElement;

                    var sessionId = parsed.TryGetProperty("SessionId", out var s) ? s.GetString() : null;
                    var rating = parsed.TryGetProperty("Rating", out var r) ? r.GetInt32() : 0;
                    var attendee = parsed.TryGetProperty("AttendeeEmail", out var a) ? a.GetString() : null;

                    _logger.LogInformation(
                        "Feedback event received. SessionId={SessionId}, Rating={Rating}, Attendee={Attendee}, Partition={PartitionKey}, Offset={Offset}",
                        sessionId ?? "<unknown>",
                        rating,
                        attendee ?? "<unknown>",
                        eventData.PartitionKey ?? "<none>",
                        eventData.Offset);

                    _logger.LogInformation("Feedback payload: {Payload}", payload);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process feedback event.");
                }
            }

            await Task.CompletedTask;
        }
    }
}
