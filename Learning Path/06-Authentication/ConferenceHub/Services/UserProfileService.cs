using System.Security.Claims;

namespace ConferenceHub.Services
{
    public class UserProfileService : IUserProfileService
    {
        public string GetUserEmail(ClaimsPrincipal user)
        {
            return user.FindFirst(ClaimTypes.Email)?.Value 
                ?? user.FindFirst("preferred_username")?.Value 
                ?? "unknown@email.com";
        }

        public string GetUserName(ClaimsPrincipal user)
        {
            return user.FindFirst(ClaimTypes.Name)?.Value 
                ?? user.FindFirst("name")?.Value 
                ?? "Unknown User";
        }

        public string GetUserId(ClaimsPrincipal user)
        {
            return user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? user.FindFirst("oid")?.Value 
                ?? Guid.NewGuid().ToString();
        }

        public bool IsOrganizer(ClaimsPrincipal user)
        {
            return user.IsInRole("Organizer");
        }

        public List<string> GetUserRoles(ClaimsPrincipal user)
        {
            return user.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
        }
    }
}
