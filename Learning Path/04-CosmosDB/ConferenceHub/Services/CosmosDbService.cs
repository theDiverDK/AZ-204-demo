using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using ConferenceHub.Models;

namespace ConferenceHub.Services
{
    public class CosmosDbService : ICosmosDbService
    {
        private readonly Container _sessionsContainer;
        private readonly Container _registrationsContainer;
        private readonly ILogger<CosmosDbService> _logger;

        public CosmosDbService(
            CosmosClient cosmosClient,
            string databaseName,
            ILogger<CosmosDbService> logger)
        {
            _sessionsContainer = cosmosClient.GetContainer(databaseName, "Sessions");
            _registrationsContainer = cosmosClient.GetContainer(databaseName, "Registrations");
            _logger = logger;
        }

        #region Sessions

        public async Task<IEnumerable<Session>> GetSessionsAsync()
        {
            try
            {
                var query = _sessionsContainer.GetItemLinqQueryable<Session>()
                    .OrderBy(s => s.StartTime);

                var iterator = query.ToFeedIterator();
                var results = new List<Session>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sessions from Cosmos DB");
                throw;
            }
        }

        public async Task<IEnumerable<Session>> GetSessionsByFilterAsync(string? track = null, string? level = null)
        {
            try
            {
                var query = _sessionsContainer.GetItemLinqQueryable<Session>();

                if (!string.IsNullOrEmpty(track))
                {
                    query = query.Where(s => s.Track == track);
                }

                if (!string.IsNullOrEmpty(level))
                {
                    query = query.Where(s => s.Level == level);
                }

                query = query.OrderBy(s => s.StartTime);

                var iterator = query.ToFeedIterator();
                var results = new List<Session>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering sessions in Cosmos DB");
                throw;
            }
        }

        public async Task<Session?> GetSessionByIdAsync(string id)
        {
            try
            {
                var response = await _sessionsContainer.ReadItemAsync<Session>(
                    id,
                    new PartitionKey("az204-2025"));
                
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving session {SessionId}", id);
                throw;
            }
        }

        public async Task<Session> AddSessionAsync(Session session)
        {
            try
            {
                session.Id = Guid.NewGuid().ToString();
                session.ConferenceId = "az204-2025";
                session.CreatedAt = DateTime.UtcNow;
                session.UpdatedAt = DateTime.UtcNow;

                var response = await _sessionsContainer.CreateItemAsync(
                    session,
                    new PartitionKey(session.ConferenceId));

                _logger.LogInformation("Created session {SessionId}", session.Id);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating session in Cosmos DB");
                throw;
            }
        }

        public async Task<Session> UpdateSessionAsync(Session session)
        {
            try
            {
                session.UpdatedAt = DateTime.UtcNow;

                var response = await _sessionsContainer.ReplaceItemAsync(
                    session,
                    session.Id,
                    new PartitionKey(session.ConferenceId));

                _logger.LogInformation("Updated session {SessionId}", session.Id);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating session {SessionId}", session.Id);
                throw;
            }
        }

        public async Task DeleteSessionAsync(string id)
        {
            try
            {
                await _sessionsContainer.DeleteItemAsync<Session>(
                    id,
                    new PartitionKey("az204-2025"));

                _logger.LogInformation("Deleted session {SessionId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting session {SessionId}", id);
                throw;
            }
        }

        #endregion

        #region Registrations

        public async Task<IEnumerable<Registration>> GetRegistrationsAsync()
        {
            try
            {
                var query = _registrationsContainer.GetItemLinqQueryable<Registration>()
                    .OrderByDescending(r => r.RegisteredAt);

                var iterator = query.ToFeedIterator();
                var results = new List<Registration>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registrations from Cosmos DB");
                throw;
            }
        }

        public async Task<IEnumerable<Registration>> GetSessionRegistrationsAsync(string sessionId)
        {
            try
            {
                var query = _registrationsContainer.GetItemLinqQueryable<Registration>()
                    .Where(r => r.SessionId == sessionId)
                    .OrderByDescending(r => r.RegisteredAt);

                var iterator = query.ToFeedIterator();
                var results = new List<Registration>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registrations for session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<Registration?> GetRegistrationByIdAsync(string id)
        {
            try
            {
                // Need to query since we don't have the partition key
                var query = _registrationsContainer.GetItemLinqQueryable<Registration>()
                    .Where(r => r.Id == id);

                var iterator = query.ToFeedIterator();
                var results = await iterator.ReadNextAsync();

                return results.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registration {RegistrationId}", id);
                throw;
            }
        }

        public async Task<Registration> AddRegistrationAsync(Registration registration)
        {
            try
            {
                registration.Id = Guid.NewGuid().ToString();
                registration.RegisteredAt = DateTime.UtcNow;
                registration.Status = "Confirmed";

                var response = await _registrationsContainer.CreateItemAsync(
                    registration,
                    new PartitionKey(registration.SessionId));

                _logger.LogInformation("Created registration {RegistrationId} for session {SessionId}",
                    registration.Id, registration.SessionId);

                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating registration in Cosmos DB");
                throw;
            }
        }

        public async Task<Registration> UpdateRegistrationAsync(Registration registration)
        {
            try
            {
                var response = await _registrationsContainer.ReplaceItemAsync(
                    registration,
                    registration.Id,
                    new PartitionKey(registration.SessionId));

                _logger.LogInformation("Updated registration {RegistrationId}", registration.Id);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating registration {RegistrationId}", registration.Id);
                throw;
            }
        }

        public async Task DeleteRegistrationAsync(string id, string sessionId)
        {
            try
            {
                await _registrationsContainer.DeleteItemAsync<Registration>(
                    id,
                    new PartitionKey(sessionId));

                _logger.LogInformation("Deleted registration {RegistrationId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting registration {RegistrationId}", id);
                throw;
            }
        }

        public async Task<int> GetRegistrationCountBySessionAsync(string sessionId)
        {
            try
            {
                var query = _registrationsContainer.GetItemLinqQueryable<Registration>()
                    .Where(r => r.SessionId == sessionId && r.Status == "Confirmed")
                    .Count();

                var iterator = query.ToFeedIterator();
                var response = await iterator.ReadNextAsync();

                return response.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting registrations for session {SessionId}", sessionId);
                return 0;
            }
        }

        #endregion
    }
}
