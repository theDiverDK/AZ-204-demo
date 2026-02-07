using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ConferenceHub.Models;
using Microsoft.Extensions.Options;

namespace ConferenceHub.Services
{
    public interface IRegistrationMessagePublisher
    {
        Task PublishAsync(RegistrationMessage message);
    }

    public class RegistrationMessagePublisher : IRegistrationMessagePublisher
    {
        private readonly ServiceBusConfig _config;
        private readonly ILogger<RegistrationMessagePublisher> _logger;

        public RegistrationMessagePublisher(IOptions<ServiceBusConfig> config, ILogger<RegistrationMessagePublisher> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        public async Task PublishAsync(RegistrationMessage message)
        {
            if (string.IsNullOrWhiteSpace(_config.ConnectionString) || string.IsNullOrWhiteSpace(_config.TopicName))
            {
                _logger.LogInformation("Service Bus messaging is not configured. Skipping registration event.");
                return;
            }

            try
            {
                await using var client = new ServiceBusClient(_config.ConnectionString);
                ServiceBusSender sender = client.CreateSender(_config.TopicName);
                var body = JsonSerializer.Serialize(message);
                await sender.SendMessageAsync(new ServiceBusMessage(body));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish registration message for session {SessionId}", message.SessionId);
            }
        }
    }

    public class RegistrationMessage
    {
        public int SessionId { get; set; }
        public string SessionTitle { get; set; } = string.Empty;
        public string AttendeeName { get; set; } = string.Empty;
        public string AttendeeEmail { get; set; } = string.Empty;
        public DateTime SessionStartTime { get; set; }
        public string Room { get; set; } = string.Empty;
    }
}
