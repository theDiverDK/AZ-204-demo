using Azure;
using Azure.Data.Tables;

namespace ConferenceHub.Models
{
    public class AuditLogEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty; // SessionId
        public string RowKey { get; set; } = string.Empty; // Timestamp + RegistrationId
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        // Custom properties
        public string Action { get; set; } = string.Empty;
        public string AttendeeName { get; set; } = string.Empty;
        public string AttendeeEmail { get; set; } = string.Empty;
        public string SessionTitle { get; set; } = string.Empty;
        public DateTime ActionTimestamp { get; set; }
        public string? AdditionalInfo { get; set; }
    }
}
