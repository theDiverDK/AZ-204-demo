using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ConferenceHub.Functions;

public sealed class RegistrationMessageFunction
{
    private readonly ILogger<RegistrationMessageFunction> _logger;

    public RegistrationMessageFunction(ILogger<RegistrationMessageFunction> logger)
    {
        _logger = logger;
    }

    [Function("ProcessRegistrationMessage")]
    public void Run(
        [ServiceBusTrigger("%ServiceBusTopicName%", "%ServiceBusSubscriptionName%", Connection = "ServiceBusConnection")] string message)
    {
        var payload = JsonSerializer.Deserialize<RegistrationPayload>(
            message,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new RegistrationPayload();

        var sender = Environment.GetEnvironmentVariable("CONFIRMATION_SENDER_EMAIL") ?? "noreply@conferencehub.local";
        var receiver = payload.AttendeeEmail ?? string.Empty;
        var subject = $"ConferenceHub Confirmation: {payload.SessionTitle}";
        var body =
            $"Hello {payload.AttendeeName}, you are registered for '{payload.SessionTitle}' in room {payload.Room} starting {payload.SessionStartTime:yyyy-MM-dd HH:mm}.";

        _logger.LogInformation("Confirmation email log output from Service Bus message");
        _logger.LogInformation("Sender: {Sender}", sender);
        _logger.LogInformation("Receiver: {Receiver}", receiver);
        _logger.LogInformation("Subject: {Subject}", subject);
        _logger.LogInformation("Body: {Body}", body);
    }

    private sealed class RegistrationPayload
    {
        public int SessionId { get; set; }
        public string? SessionTitle { get; set; }
        public string? AttendeeName { get; set; }
        public string? AttendeeEmail { get; set; }
        public DateTime SessionStartTime { get; set; }
        public string? Room { get; set; }
    }
}
