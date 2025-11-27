using Microsoft.Extensions.Configuration;

namespace ConferenceHub.Services
{
    public class ApiManagementClient : IApiManagementClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _subscriptionKey;

        public ApiManagementClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _subscriptionKey = configuration["ApiManagement:SubscriptionKey"]!;
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            return await _httpClient.SendAsync(request);
        }
    }
}
