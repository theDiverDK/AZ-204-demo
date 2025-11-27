namespace ConferenceHub.Services
{
    public interface IBlobStorageService
    {
        Task<string> UploadSlideAsync(int sessionId, string fileName, Stream fileStream, string contentType);
        Task<bool> DeleteSlideAsync(string blobUrl);
        Task<Stream?> DownloadSlideAsync(string blobName);
        string GetBlobUrl(string blobName);
    }
}
