using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ConferenceHubFunctions.Authorization;

namespace ConferenceHubFunctions
{
    public class SendConfirmation
    {
        private readonly ILogger _logger;
        private readonly JwtValidator _jwtValidator;

        public SendConfirmation(ILoggerFactory loggerFactory, JwtValidator jwtValidator)
        {
            _logger = loggerFactory.CreateLogger<SendConfirmation>();
            _jwtValidator = jwtValidator;
        }

        [Function("SendConfirmation")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("SendConfirmation function triggered");

            // Validate JWT token
            if (!req.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Missing Authorization header");
                return unauthorizedResponse;
            }

            var token = authHeaders.FirstOrDefault()?.Replace("Bearer ", "");
            if (string.IsNullOrEmpty(token))
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Invalid Authorization header");
                return unauthorizedResponse;
            }

            var principal = await _jwtValidator.ValidateTokenAsync(token);
            if (principal == null)
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Invalid or expired token");
                return unauthorizedResponse;
            }

            // Process the request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var registrationRequest = JsonSerializer.Deserialize<RegistrationRequest>(requestBody);

            if (registrationRequest == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid request body");
                return badResponse;
            }

            _logger.LogInformation("Sending confirmation email to {Email} for session {SessionTitle}",
                registrationRequest.AttendeeEmail, registrationRequest.SessionTitle);

            // Simulate email sending
            await Task.Delay(100);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($"Confirmation email sent to {registrationRequest.AttendeeEmail}");

            return response;
        }

        private class RegistrationRequest
        {
            public int SessionId { get; set; }
            public string SessionTitle { get; set; } = string.Empty;
            public string AttendeeName { get; set; } = string.Empty;
            public string AttendeeEmail { get; set; } = string.Empty;
            public DateTime SessionStartTime { get; set; }
            public string Room { get; set; } = string.Empty;
        }
    }
}