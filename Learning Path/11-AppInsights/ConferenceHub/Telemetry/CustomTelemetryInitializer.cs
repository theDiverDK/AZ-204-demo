using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace ConferenceHub.Telemetry
{
    public class CustomTelemetryInitializer : ITelemetryInitializer
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CustomTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void Initialize(ITelemetry telemetry)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context != null)
            {
                // Add custom properties
                telemetry.Context.Cloud.RoleName = "ConferenceHub-WebApp";
                
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    telemetry.Context.User.AuthenticatedUserId = context.User.Identity.Name;
                    
                    // Add custom dimensions
                    if (telemetry is ISupportProperties propertiesTelemetry)
                    {
                        propertiesTelemetry.Properties["UserEmail"] = 
                            context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Unknown";
                        
                        propertiesTelemetry.Properties["IsOrganizer"] = 
                            context.User.IsInRole("Organizer").ToString();
                    }
                }

                // Add session information
                if (!string.IsNullOrEmpty(context.Session?.Id))
                {
                    telemetry.Context.Session.Id = context.Session.Id;
                }
            }
        }
    }
}
