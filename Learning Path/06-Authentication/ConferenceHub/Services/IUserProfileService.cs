using System.Security.Claims;

namespace ConferenceHub.Services
{
    public interface IUserProfileService
    {
        string GetUserEmail(ClaimsPrincipal user);
        string GetUserName(ClaimsPrincipal user);
        string GetUserId(ClaimsPrincipal user);
        bool IsOrganizer(ClaimsPrincipal user);
        List<string> GetUserRoles(ClaimsPrincipal user);
    }
}
