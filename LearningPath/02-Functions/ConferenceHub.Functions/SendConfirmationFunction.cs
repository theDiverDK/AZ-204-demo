using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ConferenceHub.Functions;

public sealed class SendConfirmationFunction
{
    private readonly ILogger<SendConfirmationFunction> _logger;

    public SendConfirmationFunction(ILogger<SendConfirmationFunction> logger)
    {
        _logger = logger;
    }

    [Function("SendConfirmation")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "SendConfirmation")] HttpRequestData req)
    {
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var payload = JsonSerializer.Deserialize<RegistrationPayload>(
            requestBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new RegistrationPayload();

        var sender = Environment.GetEnvironmentVariable("CONFIRMATION_SENDER_EMAIL") ?? "noreply@conferencehub.local";
        var receiver = payload.AttendeeEmail ?? string.Empty;
        var subject = $"ConferenceHub Confirmation: {payload.SessionTitle}";
        var body =
            $"Hello {payload.AttendeeName}, you are registered for '{payload.SessionTitle}' in room {payload.Room} starting {payload.SessionStartTime:yyyy-MM-dd HH:mm}.";

        _logger.LogInformation("Confirmation email log output");
        _logger.LogInformation("Sender: {Sender}", sender);
        _logger.LogInformation("Receiver: {Receiver}", receiver);
        _logger.LogInformation("Subject: {Subject}", subject);
        _logger.LogInformation("Body: {Body}", body);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            ok = true,
            receiver,
            subject,
            message = "Confirmation email content logged."
        });

        return response;
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
