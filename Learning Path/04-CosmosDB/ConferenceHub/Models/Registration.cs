using Newtonsoft.Json;

namespace ConferenceHub.Models
{
    public class Registration
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = string.Empty; // Partition key
        
        [JsonProperty("sessionTitle")]
        public string SessionTitle { get; set; } = string.Empty;
        
        [JsonProperty("attendeeName")]
        public string AttendeeName { get; set; } = string.Empty;
        
        [JsonProperty("attendeeEmail")]
        public string AttendeeEmail { get; set; } = string.Empty;
        
        [JsonProperty("registeredAt")]
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
        
        [JsonProperty("status")]
        public string Status { get; set; } = "Confirmed"; // Confirmed, Cancelled, Waitlist
        
        [JsonProperty("userId")]
        public string? UserId { get; set; } // For future auth integration
    }
}
