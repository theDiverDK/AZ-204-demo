namespace ConferenceHub.Models
{
    public class RegistrationMessage
    {
        public int RegistrationId { get; set; }
        public int SessionId { get; set; }
        public string SessionTitle { get; set; } = string.Empty;
        public string AttendeeName { get; set; } = string.Empty;
        public string AttendeeEmail { get; set; } = string.Empty;
        public DateTime SessionStartTime { get; set; }
        public string Room { get; set; } = string.Empty;
        public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    }

    public class NotificationMessage
    {
        public string NotificationType { get; set; } = string.Empty; // Email, SMS, Mobile
        public string Recipient { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
