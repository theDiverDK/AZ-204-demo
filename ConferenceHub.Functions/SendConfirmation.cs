using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ConferenceHub.Functions.Models;

namespace ConferenceHub.Functions
{
    public class SendConfirmation
    {
        private readonly ILogger<SendConfirmation> _logger;

        public SendConfirmation(ILogger<SendConfirmation> logger)
        {
            _logger = logger;
        }

        [Function("SendConfirmation")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("SendConfirmation function triggered");

            try
            {
                // Read the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var registration = JsonSerializer.Deserialize<RegistrationRequest>(requestBody, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (registration == null)
                {
                    return new BadRequestObjectResult("Invalid registration data");
                }

                // Simulate sending email (will be replaced with actual email service later)
                _logger.LogInformation(
                    "Sending confirmation email to {Email} for session '{SessionTitle}'",
                    registration.AttendeeEmail,
                    registration.SessionTitle);

                _logger.LogInformation(
                    "Email Details - Attendee: {Name}, Session: {Title}, Time: {Time}, Room: {Room}",
                    registration.AttendeeName,
                    registration.SessionTitle,
                    registration.SessionStartTime,
                    registration.Room);

                // Simulate email content
                var emailContent = new
                {
                    To = registration.AttendeeEmail,
                    Subject = $"Registration Confirmed: {registration.SessionTitle}",
                    Body = $@"
                        Dear {registration.AttendeeName},
                        
                        Your registration for the following session has been confirmed:
                        
                        Session: {registration.SessionTitle}
                        Date & Time: {registration.SessionStartTime:MMMM dd, yyyy 'at' h:mm tt}
                        Room: {registration.Room}
                        
                        We look forward to seeing you at the conference!
                        
                        Best regards,
                        ConferenceHub Team
                    "
                };

                _logger.LogInformation("Email content: {@EmailContent}", emailContent);

                return new OkObjectResult(new
                {
                    success = true,
                    message = "Confirmation email sent successfully",
                    recipient = registration.AttendeeEmail
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing confirmation email");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}