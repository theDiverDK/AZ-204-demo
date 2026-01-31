using ConferenceHub.Models;

namespace ConferenceHub.Services
{
    public interface ICosmosDbService
    {
        // Sessions
        Task<IEnumerable<Session>> GetSessionsAsync();
        Task<IEnumerable<Session>> GetSessionsByFilterAsync(string? track = null, string? level = null);
        Task<Session?> GetSessionByIdAsync(string id);
        Task<Session> AddSessionAsync(Session session);
        Task<Session> UpdateSessionAsync(Session session);
        Task DeleteSessionAsync(string id);
        
        // Registrations
        Task<IEnumerable<Registration>> GetRegistrationsAsync();
        Task<IEnumerable<Registration>> GetSessionRegistrationsAsync(string sessionId);
        Task<Registration?> GetRegistrationByIdAsync(string id);
        Task<Registration> AddRegistrationAsync(Registration registration);
        Task<Registration> UpdateRegistrationAsync(Registration registration);
        Task DeleteRegistrationAsync(string id, string sessionId);
        Task<int> GetRegistrationCountBySessionAsync(string sessionId);
    }
}
