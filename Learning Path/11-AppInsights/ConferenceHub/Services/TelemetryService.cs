using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace ConferenceHub.Services
{
    public class TelemetryService : ITelemetryService
    {
        private readonly TelemetryClient _telemetryClient;

        public TelemetryService(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient;
        }

        public void TrackSessionView(int sessionId, string sessionTitle, string userId)
        {
            var properties = new Dictionary<string, string>
            {
                { "SessionId", sessionId.ToString() },
                { "SessionTitle", sessionTitle },
                { "UserId", userId }
            };

            var metrics = new Dictionary<string, double>
            {
                { "ViewCount", 1 }
            };

            _telemetryClient.TrackEvent("SessionViewed", properties, metrics);
        }

        public void TrackRegistration(int sessionId, string sessionTitle, string userId, bool success)
        {
            var properties = new Dictionary<string, string>
            {
                { "SessionId", sessionId.ToString() },
                { "SessionTitle", sessionTitle },
                { "UserId", userId },
                { "Success", success.ToString() }
            };

            var metrics = new Dictionary<string, double>
            {
                { "RegistrationCount", success ? 1 : 0 },
                { "FailedRegistrationCount", success ? 0 : 1 }
            };

            _telemetryClient.TrackEvent("RegistrationAttempt", properties, metrics);

            // Track metric for monitoring
            _telemetryClient.GetMetric("Registrations.Total").TrackValue(success ? 1 : 0);
            
            if (!success)
            {
                _telemetryClient.TrackTrace($"Registration failed for session {sessionId}", SeverityLevel.Warning);
            }
        }

        public void TrackSlideDownload(int sessionId, string sessionTitle, string userId)
        {
            var properties = new Dictionary<string, string>
            {
                { "SessionId", sessionId.ToString() },
                { "SessionTitle", sessionTitle },
                { "UserId", userId }
            };

            var metrics = new Dictionary<string, double>
            {
                { "DownloadCount", 1 }
            };

            _telemetryClient.TrackEvent("SlideDownloaded", properties, metrics);
        }

        public void TrackFeedbackSubmission(int sessionId, int rating, string userId)
        {
            var properties = new Dictionary<string, string>
            {
                { "SessionId", sessionId.ToString() },
                { "UserId", userId }
            };

            var metrics = new Dictionary<string, double>
            {
                { "Rating", rating },
                { "FeedbackCount", 1 }
            };

            _telemetryClient.TrackEvent("FeedbackSubmitted", properties, metrics);
            _telemetryClient.GetMetric("Session.AverageRating", "SessionId").TrackValue(rating, sessionId.ToString());
        }

        public void TrackSearchQuery(string query, int resultCount, string userId)
        {
            var properties = new Dictionary<string, string>
            {
                { "Query", query },
                { "UserId", userId },
                { "HasResults", (resultCount > 0).ToString() }
            };

            var metrics = new Dictionary<string, double>
            {
                { "ResultCount", resultCount }
            };

            _telemetryClient.TrackEvent("SearchPerformed", properties, metrics);
        }
    }
}
