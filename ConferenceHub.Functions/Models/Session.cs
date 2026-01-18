namespace ConferenceHub.Functions.Models
{
    public class Session
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public bool RegistrationClosed { get; set; }
    }
}
