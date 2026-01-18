using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json.Linq;

namespace ConferenceHub.Functions
{
    public class CloseRegistrations
    {
        private readonly ILogger _logger;
        private readonly CosmosClient _cosmosClient;

        public CloseRegistrations(ILoggerFactory loggerFactory, CosmosClient cosmosClient)
        {
            _logger = loggerFactory.CreateLogger<CloseRegistrations>();
            _cosmosClient = cosmosClient;
        }

        [Function("CloseRegistrations")]
        public async Task Run([TimerTrigger("0 0 2 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation("CloseRegistrations function triggered at: {Time}", DateTime.Now);

            try
            {
                var container = _cosmosClient.GetContainer("ConferenceHubDB", "Sessions");
                var cutoffTime = DateTime.UtcNow.AddHours(24);

                _logger.LogInformation("Checking for sessions starting before: {CutoffTime}", cutoffTime);

                // Query for sessions that need to be closed
                var query = new QueryDefinition(
                    "SELECT * FROM Sessions s WHERE s.startTime < @cutoffTime AND s.registrationClosed = false")
                    .WithParameter("@cutoffTime", cutoffTime);

                var iterator = container.GetItemQueryIterator<dynamic>(query);
                var sessionsToClose = new List<dynamic>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    sessionsToClose.AddRange(response);
                }

                // Update each session
                foreach (var session in sessionsToClose)
                {
                    var sessionDoc = (JObject)session;
                    sessionDoc["registrationClosed"] = true;
                    sessionDoc["updatedAt"] = DateTime.UtcNow;

                    var sessionId = sessionDoc.Value<string>("id") ?? string.Empty;
                    var conferenceId = sessionDoc.Value<string>("conferenceId") ?? string.Empty;
                    var sessionTitle = sessionDoc.Value<string>("title") ?? string.Empty;

                    await container.ReplaceItemAsync(

                        sessionDoc,
                        sessionId,
                        new PartitionKey(conferenceId));

                    _logger.LogInformation(
                        "Closed registration for session {SessionId}: {Title}",
                        sessionId,
                        sessionTitle);
                }

                _logger.LogInformation("Closed registration for {Count} sessions", sessionsToClose.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing registrations");
            }
        }
    }
}
