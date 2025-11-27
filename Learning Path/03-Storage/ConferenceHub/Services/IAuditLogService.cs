using ConferenceHub.Models;

namespace ConferenceHub.Services
{
    public interface IAuditLogService
    {
        Task LogRegistrationAsync(int sessionId, string sessionTitle, string attendeeName, string attendeeEmail);
        Task LogSlideUploadAsync(int sessionId, string sessionTitle, string uploadedBy);
        Task<List<AuditLogEntity>> GetSessionAuditLogsAsync(int sessionId);
        Task<List<AuditLogEntity>> GetAllAuditLogsAsync();
    }
}
