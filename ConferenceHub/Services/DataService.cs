using ConferenceHub.Models;
using System.Text.Json;

namespace ConferenceHub.Services
{
    public interface IDataService
    {
        Task<List<Session>> GetSessionsAsync();
        Task<Session?> GetSessionByIdAsync(int id);
        Task AddSessionAsync(Session session);
        Task UpdateSessionAsync(Session session);
        Task DeleteSessionAsync(int id);
        Task<List<Registration>> GetRegistrationsAsync();
        Task AddRegistrationAsync(Registration registration);
    }

    public class DataService : IDataService
    {
        private readonly string _seedSessionsFilePath;
        private List<Session> _sessions;
        private List<Registration> _registrations;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public DataService(IWebHostEnvironment env)
        {
            _seedSessionsFilePath = Path.Combine(env.ContentRootPath, "Data", "sessions.json");
            _sessions = new List<Session>();
            _registrations = new List<Registration>();
            LoadSessionsAsync().Wait();
        }

        private async Task LoadSessionsAsync()
        {
            try
            {
                if (File.Exists(_seedSessionsFilePath))
                {
                    var jsonContent = await File.ReadAllTextAsync(_seedSessionsFilePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var legacySessions = JsonSerializer.Deserialize<List<LegacySession>>(jsonContent, options)
                        ?? new List<LegacySession>();

                    _sessions = legacySessions.Select(s => new Session
                    {
                        Id = s.Id.ToString(),
                        SessionNumber = s.Id,
                        Title = s.Title,
                        Speaker = s.Speaker,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        Room = s.Room,
                        Description = s.Description,
                        Capacity = s.Capacity,
                        CurrentRegistrations = s.CurrentRegistrations,
                        SlideUrl = s.SlideUrl,
                        SlideUploadedAt = s.SlideUploadedAt
                    }).ToList();
                }
            }
            catch (Exception)
            {
                _sessions = new List<Session>();
            }
        }

        public async Task<List<Session>> GetSessionsAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                return _sessions.OrderBy(s => s.StartTime).ToList();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<Session?> GetSessionByIdAsync(int id)
        {
            await _semaphore.WaitAsync();
            try
            {
                return _sessions.FirstOrDefault(s => s.SessionNumber == id);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task AddSessionAsync(Session session)
        {
            await _semaphore.WaitAsync();
            try
            {
                session.SessionNumber = _sessions.Any() ? _sessions.Max(s => s.SessionNumber) + 1 : 1;
                session.Id = session.SessionNumber.ToString();
                _sessions.Add(session);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task UpdateSessionAsync(Session session)
        {
            await _semaphore.WaitAsync();
            try
            {
                var existingSession = _sessions.FirstOrDefault(s => s.SessionNumber == session.SessionNumber);
                if (existingSession != null)
                {
                    _sessions.Remove(existingSession);
                    _sessions.Add(session);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task DeleteSessionAsync(int id)
        {
            await _semaphore.WaitAsync();
            try
            {
                var session = _sessions.FirstOrDefault(s => s.SessionNumber == id);
                if (session != null)
                {
                    _sessions.Remove(session);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<Registration>> GetRegistrationsAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                return _registrations.ToList();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task AddRegistrationAsync(Registration registration)
        {
            await _semaphore.WaitAsync();
            try
            {
                registration.Id = Guid.NewGuid().ToString();
                registration.RegisteredAt = DateTime.Now;
                _registrations.Add(registration);

                // Update session registration count
                var session = _sessions.FirstOrDefault(s => s.Id == registration.SessionId)
                    ?? _sessions.FirstOrDefault(s => s.SessionNumber.ToString() == registration.SessionId);
                if (session != null)
                {
                    session.CurrentRegistrations++;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private sealed class LegacySession
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Speaker { get; set; } = string.Empty;
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public string Room { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int Capacity { get; set; }
            public int CurrentRegistrations { get; set; }
            public string? SlideUrl { get; set; }
            public DateTime? SlideUploadedAt { get; set; }
        }
    }
}
