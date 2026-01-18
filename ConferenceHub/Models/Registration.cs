namespace ConferenceHub.Models
{
    public class Registration
    {
        public string Id { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string SessionTitle { get; set; } = string.Empty;
        public string AttendeeName { get; set; } = string.Empty;
        public string AttendeeEmail { get; set; } = string.Empty;
        public DateTime RegisteredAt { get; set; }
        public string Status { get; set; } = "Confirmed";
    }
}
