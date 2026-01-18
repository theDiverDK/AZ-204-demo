using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ConferenceHub.Functions
{
    public class CloseRegistrations
    {
        private readonly ILogger _logger;

        public CloseRegistrations(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CloseRegistrations>();
        }

        // Runs every night at 2 AM (CRON: 0 0 2 * * *)
        // For testing, use "0 */5 * * * *" to run every 5 minutes
        [Function("CloseRegistrations")]
        public async Task Run([TimerTrigger("0 * * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation("CloseRegistrations function triggered at: {Time}", DateTime.Now);

            try
            {
                // Calculate the cutoff time (24 hours from now)
                var cutoffTime = DateTime.Now.AddHours(24);
                _logger.LogInformation("Checking for sessions starting before: {CutoffTime}", cutoffTime);

                // TODO: In a future learning path, this will:
                // 1. Connect to Azure Table Storage or Cosmos DB
                // 2. Query for sessions where StartTime < cutoffTime AND RegistrationClosed = false
                // 3. Update those sessions to set RegistrationClosed = true

                // For now, simulate the logic
                _logger.LogInformation("Simulating closing registrations for upcoming sessions");
                
                // Simulate finding sessions
                var sessionsToClose = new[]
                {
                    new { Id = 1, Title = "Sample Session 1", StartTime = DateTime.Now.AddHours(20) },
                    new { Id = 2, Title = "Sample Session 2", StartTime = DateTime.Now.AddHours(22) }
                };

                foreach (var session in sessionsToClose)
                {
                    _logger.LogInformation(
                        "Closing registration for session {SessionId}: {Title} (starts at {StartTime})",
                        session.Id,
                        session.Title,
                        session.StartTime);
                }

                _logger.LogInformation("Closed registration for {Count} sessions", sessionsToClose.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing registrations");
            }

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation("Next timer schedule at: {NextRun}", myTimer.ScheduleStatus.Next);
            }
        }
    }
}