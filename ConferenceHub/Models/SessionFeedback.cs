namespace ConferenceHub.Models
{
    public class SessionFeedback
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SessionId { get; set; } = string.Empty;
        public string SessionTitle { get; set; } = string.Empty;
        public string AttendeeEmail { get; set; } = string.Empty;
        public string AttendeeName { get; set; } = string.Empty;
        public int Rating { get; set; } // 1-5 stars
        public string? Comment { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public bool IsRecommended { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class FeedbackStatistics
    {
        public string SessionId { get; set; } = string.Empty;
        public double AverageRating { get; set; }
        public int TotalFeedbacks { get; set; }
        public int FiveStars { get; set; }
        public int FourStars { get; set; }
        public int ThreeStars { get; set; }
        public int TwoStars { get; set; }
        public int OneStar { get; set; }
        public int RecommendationCount { get; set; }
        public double RecommendationPercentage { get; set; }
    }
}
