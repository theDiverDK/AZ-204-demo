using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ConferenceHub.Services
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName = "speaker-slides";
        private readonly ILogger<BlobStorageService> _logger;

        public BlobStorageService(string connectionString, ILogger<BlobStorageService> logger)
        {
            _blobServiceClient = new BlobServiceClient(connectionString);
            _logger = logger;
        }

        public async Task<string> UploadSlideAsync(int sessionId, string fileName, Stream fileStream, string contentType)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

                // Create unique blob name
                var extension = Path.GetExtension(fileName);
                var blobName = $"session-{sessionId}/{Guid.NewGuid()}{extension}";
                var blobClient = containerClient.GetBlobClient(blobName);

                // Upload with metadata
                var blobHttpHeaders = new BlobHttpHeaders { ContentType = contentType };
                var metadata = new Dictionary<string, string>
                {
                    { "SessionId", sessionId.ToString() },
                    { "OriginalFileName", fileName },
                    { "UploadedAt", DateTime.UtcNow.ToString("o") }
                };

                await blobClient.UploadAsync(fileStream, new BlobUploadOptions
                {
                    HttpHeaders = blobHttpHeaders,
                    Metadata = metadata
                });

                _logger.LogInformation("Uploaded slide for session {SessionId}: {BlobName}", sessionId, blobName);
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading slide for session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<bool> DeleteSlideAsync(string blobUrl)
        {
            try
            {
                var uri = new Uri(blobUrl);
                var blobName = uri.Segments[^1]; // Get last segment
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                var response = await blobClient.DeleteIfExistsAsync();
                return response.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting blob: {BlobUrl}", blobUrl);
                return false;
            }
        }

        public async Task<Stream?> DownloadSlideAsync(string blobName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                if (await blobClient.ExistsAsync())
                {
                    var downloadInfo = await blobClient.DownloadAsync();
                    return downloadInfo.Value.Content;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading blob: {BlobName}", blobName);
                return null;
            }
        }

        public string GetBlobUrl(string blobName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            return blobClient.Uri.ToString();
        }
    }
}
