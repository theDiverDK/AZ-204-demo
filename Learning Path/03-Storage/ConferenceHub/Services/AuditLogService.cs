using Azure.Data.Tables;
using ConferenceHub.Models;

namespace ConferenceHub.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly TableClient _tableClient;
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(string connectionString, ILogger<AuditLogService> logger)
        {
            var tableServiceClient = new TableServiceClient(connectionString);
            _tableClient = tableServiceClient.GetTableClient("AuditLogs");
            _tableClient.CreateIfNotExists();
            _logger = logger;
        }

        public async Task LogRegistrationAsync(int sessionId, string sessionTitle, string attendeeName, string attendeeEmail)
        {
            try
            {
                var entity = new AuditLogEntity
                {
                    PartitionKey = sessionId.ToString(),
                    RowKey = $"{DateTime.UtcNow.Ticks}_{Guid.NewGuid()}",
                    Action = "Register",
                    AttendeeName = attendeeName,
                    AttendeeEmail = attendeeEmail,
                    SessionTitle = sessionTitle,
                    ActionTimestamp = DateTime.UtcNow
                };

                await _tableClient.AddEntityAsync(entity);
                _logger.LogInformation("Logged registration for {Email} to session {SessionId}", attendeeEmail, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging registration audit");
            }
        }

        public async Task LogSlideUploadAsync(int sessionId, string sessionTitle, string uploadedBy)
        {
            try
            {
                var entity = new AuditLogEntity
                {
                    PartitionKey = sessionId.ToString(),
                    RowKey = $"{DateTime.UtcNow.Ticks}_{Guid.NewGuid()}",
                    Action = "SlideUpload",
                    AttendeeName = uploadedBy,
                    SessionTitle = sessionTitle,
                    ActionTimestamp = DateTime.UtcNow,
                    AdditionalInfo = "Speaker slides uploaded"
                };

                await _tableClient.AddEntityAsync(entity);
                _logger.LogInformation("Logged slide upload for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging slide upload audit");
            }
        }

        public async Task<List<AuditLogEntity>> GetSessionAuditLogsAsync(int sessionId)
        {
            var logs = new List<AuditLogEntity>();
            try
            {
                await foreach (var entity in _tableClient.QueryAsync<AuditLogEntity>(e => e.PartitionKey == sessionId.ToString()))
                {
                    logs.Add(entity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving audit logs for session {SessionId}", sessionId);
            }
            return logs.OrderByDescending(l => l.ActionTimestamp).ToList();
        }

        public async Task<List<AuditLogEntity>> GetAllAuditLogsAsync()
        {
            var logs = new List<AuditLogEntity>();
            try
            {
                await foreach (var entity in _tableClient.QueryAsync<AuditLogEntity>())
                {
                    logs.Add(entity);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all audit logs");
            }
            return logs.OrderByDescending(l => l.ActionTimestamp).ToList();
        }
    }
}
