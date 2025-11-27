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
