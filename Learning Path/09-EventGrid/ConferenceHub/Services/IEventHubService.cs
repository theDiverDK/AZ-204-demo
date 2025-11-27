using ConferenceHub.Models;

namespace ConferenceHub.Services
{
    public interface IEventHubService
    {
        Task SendFeedbackAsync(SessionFeedback feedback);
        Task SendBatchFeedbackAsync(List<SessionFeedback> feedbacks);
    }
}
