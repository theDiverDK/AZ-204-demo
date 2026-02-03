using ConferenceHub.Models;

namespace ConferenceHub.Services
{
    // Adapter so existing controllers can keep using IDataService while persistence is backed by Cosmos DB.
    public class CosmosDataService : IDataService
    {
        private readonly ICosmosDbService _cosmosDbService;

        public CosmosDataService(ICosmosDbService cosmosDbService)
        {
            _cosmosDbService = cosmosDbService;
        }

        public async Task<List<Session>> GetSessionsAsync()
        {
            return (await _cosmosDbService.GetSessionsAsync()).ToList();
        }

        public Task<Session?> GetSessionByIdAsync(string id)
        {
            return _cosmosDbService.GetSessionByIdAsync(id);
        }

        public async Task AddSessionAsync(Session session)
        {
            await _cosmosDbService.AddSessionAsync(session);
        }

        public async Task UpdateSessionAsync(Session session)
        {
            await _cosmosDbService.UpdateSessionAsync(session);
        }

        public Task DeleteSessionAsync(string id)
        {
            return _cosmosDbService.DeleteSessionAsync(id);
        }

        public async Task<List<Registration>> GetRegistrationsAsync()
        {
            return (await _cosmosDbService.GetRegistrationsAsync()).ToList();
        }

        public async Task AddRegistrationAsync(Registration registration)
        {
            await _cosmosDbService.AddRegistrationAsync(registration);
        }
    }
}

