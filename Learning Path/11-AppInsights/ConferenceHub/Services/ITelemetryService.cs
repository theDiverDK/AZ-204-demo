namespace ConferenceHub.Services
{
    public interface ITelemetryService
    {
        void TrackSessionView(int sessionId, string sessionTitle, string userId);
        void TrackRegistration(int sessionId, string sessionTitle, string userId, bool success);
        void TrackSlideDownload(int sessionId, string sessionTitle, string userId);
        void TrackFeedbackSubmission(int sessionId, int rating, string userId);
        void TrackSearchQuery(string query, int resultCount, string userId);
    }
}
