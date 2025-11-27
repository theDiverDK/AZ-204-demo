namespace ConferenceHub.Services
{
    public interface IStorageQueueService
    {
        Task EnqueueBackgroundTaskAsync(string taskType, string taskData);
        Task EnqueueSlideProcessingAsync(int sessionId, string blobUrl);
    }
}
