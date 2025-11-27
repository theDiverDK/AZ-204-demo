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
