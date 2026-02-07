namespace ConferenceHub.Models
{
    public class ServiceBusConfig
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string TopicName { get; set; } = string.Empty;
    }
}
