using ConferenceHub.Models;

namespace ConferenceHub.Services
{
    public interface IServiceBusService
    {
        Task SendRegistrationMessageAsync(RegistrationMessage message);
        Task PublishNotificationAsync(NotificationMessage notification);
    }
}
