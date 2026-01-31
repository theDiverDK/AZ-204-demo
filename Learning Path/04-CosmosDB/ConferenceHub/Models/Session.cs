using Newtonsoft.Json;

namespace ConferenceHub.Models
{
    public class Session
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [JsonProperty("conferenceId")]
        public string ConferenceId { get; set; } = "az204-2025"; // Partition key
        
        [JsonProperty("sessionNumber")]
        public int SessionNumber { get; set; }
        
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;
        
        [JsonProperty("speaker")]
        public string Speaker { get; set; } = string.Empty;
        
        [JsonProperty("startTime")]
        public DateTime StartTime { get; set; }
        
        [JsonProperty("endTime")]
        public DateTime EndTime { get; set; }
        
        [JsonProperty("room")]
        public string Room { get; set; } = string.Empty;
        
        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonProperty("capacity")]
        public int Capacity { get; set; }
        
        [JsonProperty("currentRegistrations")]
        public int CurrentRegistrations { get; set; }
        
        [JsonProperty("slideUrl")]
        public string? SlideUrl { get; set; }
        
        [JsonProperty("slideUploadedAt")]
        public DateTime? SlideUploadedAt { get; set; }
        
        [JsonProperty("track")]
        public string Track { get; set; } = "General"; // e.g., "Cloud", "DevOps", "AI"
        
        [JsonProperty("level")]
        public string Level { get; set; } = "Intermediate"; // Beginner, Intermediate, Advanced
        
        [JsonProperty("registrationClosed")]
        public bool RegistrationClosed { get; set; } = false;
        
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
