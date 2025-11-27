namespace ConferenceHub.Services
{
    public interface IApiManagementClient
    {
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request);
    }
}
