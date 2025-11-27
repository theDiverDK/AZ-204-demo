namespace ConferenceHub.Models
{
    public class ConferenceHubSettings
    {
        public int MaxSessionCapacity { get; set; } = 100;
        public int RegistrationOpenDays { get; set; } = 30;
        public bool AllowWaitlist { get; set; } = false;
    }

    public class EmailSettings
    {
        public string FromAddress { get; set; } = "noreply@conferencehub.com";
        public string FromName { get; set; } = "ConferenceHub";
    }
}
