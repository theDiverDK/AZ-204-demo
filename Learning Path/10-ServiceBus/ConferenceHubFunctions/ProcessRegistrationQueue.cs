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
