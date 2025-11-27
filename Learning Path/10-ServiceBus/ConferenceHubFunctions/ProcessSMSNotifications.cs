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
