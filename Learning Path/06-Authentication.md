# Learning Path 6: Authentication & Authorization with Microsoft Entra ID

## Overview
In this learning path, you'll implement Microsoft Entra ID (formerly Azure AD) authentication and role-based authorization to secure the ConferenceHub application, distinguishing between Organizers and Attendees.

## What You'll Build
1. **Microsoft Entra ID Integration**: OAuth 2.0 / OpenID Connect authentication
2. **Role-Based Authorization**: Organizer vs Attendee roles
3. **Secure API Endpoints**: Protect sensitive operations
4. **User Profile Management**: Display logged-in user information

## Prerequisites
- Completed Learning Path 1-5
- Azure subscription with permissions to create App Registrations
- Microsoft Entra ID tenant (comes with every Azure subscription)

---

## Part 1: Register Application in Microsoft Entra ID

Set variables used in the commands:
PowerShell:
```powershell
$RG_NAME="rg-conferencehub"
$LOCATION="swedencentral"
$APP_NAME="conferencehub-$RANDOM"
```
Bash:
```bash
RG_NAME="rg-conferencehub"
LOCATION="swedencentral"
APP_NAME="conferencehub-$RANDOM"
```

### Step 1: Create App Registration

PowerShell:
```powershell
# Create the app registration
az ad app create `
  --display-name "ConferenceHub-WebApp" `
  --sign-in-audience AzureADMyOrg `
  --web-redirect-uris "https://$APP_NAME.azurewebsites.net/signin-oidc" "https://localhost:7055/signin-oidc" `
  --enable-id-token-issuance true

# Get the Application (client) ID
$appId = az ad app list --display-name "ConferenceHub-WebApp" --query "[0].appId" -o tsv
Write-Host "Application ID: $appId"

# Get the Tenant ID
$tenantId = az account show --query tenantId -o tsv
Write-Host "Tenant ID: $tenantId"
```
Bash:
```bash
# Create the app registration
az ad app create \
  --display-name "ConferenceHub-WebApp" \
  --sign-in-audience AzureADMyOrg \
  --web-redirect-uris "https://$APP_NAME.azurewebsites.net/signin-oidc" "https://localhost:7055/signin-oidc" \
  --enable-id-token-issuance true

# Get the Application (client) ID
appId=$(az ad app list --display-name "ConferenceHub-WebApp" --query "[0].appId" -o tsv)
echo "Application ID: $appId"

# Get the Tenant ID
tenantId=$(az account show --query tenantId -o tsv)
echo "Tenant ID: $tenantId"
```

### Step 2: Create Client Secret

PowerShell:
```powershell
# Create a client secret (valid for 1 year)
$secretResult = az ad app credential reset `
  --id $appId `
  --append `
  --years 1

# Extract the secret value
$clientSecret = ($secretResult | ConvertFrom-Json).password
Write-Host "Client Secret: $clientSecret"

# IMPORTANT: Save these values - you'll need them in appsettings.json
Write-Host "`nSave these values:"
Write-Host "TenantId: $tenantId"
Write-Host "ClientId: $appId"
Write-Host "ClientSecret: $clientSecret"
```
Bash:
```bash
# Create a client secret (valid for 1 year)
clientSecret=$(az ad app credential reset \
  --id $appId \
  --append \
  --years 1 \
  --query password -o tsv)

echo "Client Secret: $clientSecret"

# IMPORTANT: Save these values - you'll need them in appsettings.json
echo ""
echo "Save these values:"
echo "TenantId: $tenantId"
echo "ClientId: $appId"
echo "ClientSecret: $clientSecret"
```

### Step 3: Configure App Roles

Create `app-roles.json`:
```json
[
  {
    "allowedMemberTypes": ["User"],
    "description": "Organizers can manage sessions and view registrations",
    "displayName": "Organizer",
    "isEnabled": true,
    "value": "Organizer"
  },
  {
    "allowedMemberTypes": ["User"],
    "description": "Attendees can view and register for sessions",
    "displayName": "Attendee",
    "isEnabled": true,
    "value": "Attendee"
  }
]
```

PowerShell:
```powershell
# Add app roles to the application
az ad app update --id $appId --app-roles @app-roles.json
```
Bash:
```bash
# Add app roles to the application
az ad app update --id $appId --app-roles @app-roles.json
```

### Step 4: Assign Users to Roles

PowerShell:
```powershell
# Get the Enterprise App (Service Principal) object ID
$spObjectId = az ad sp list --display-name "ConferenceHub-WebApp" --query "[0].id" -o tsv

# Get your user object ID
$userObjectId = az ad signed-in-user show --query id -o tsv

# Get the Organizer role ID
$organizerRoleId = az ad app show --id $appId --query "appRoles[?value=='Organizer'].id" -o tsv

# Assign yourself the Organizer role
az rest --method POST `
  --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$spObjectId/appRoleAssignments" `
  --headers "Content-Type=application/json" `
  --body "{`"principalId`":`"$userObjectId`",`"resourceId`":`"$spObjectId`",`"appRoleId`":`"$organizerRoleId`"}"
```
Bash:
```bash
# Get the Enterprise App (Service Principal) object ID
spObjectId=$(az ad sp list --display-name "ConferenceHub-WebApp" --query "[0].id" -o tsv)

# Get your user object ID
userObjectId=$(az ad signed-in-user show --query id -o tsv)

# Get the Organizer role ID
organizerRoleId=$(az ad app show --id $appId --query "appRoles[?value=='Organizer'].id" -o tsv)

# Assign yourself the Organizer role
az rest --method POST \
  --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$spObjectId/appRoleAssignments" \
  --headers "Content-Type=application/json" \
  --body "{`"principalId`":`"$userObjectId`",`"resourceId`":`"$spObjectId`",`"appRoleId`":`"$organizerRoleId`"}"
```

---

## Part 2: Add Authentication to Web App

### Step 1: Add NuGet Packages

PowerShell:
```powershell
cd ConferenceHub
dotnet add package Microsoft.Identity.Web
dotnet add package Microsoft.Identity.Web.UI
```
Bash:
```bash
cd ConferenceHub
dotnet add package Microsoft.Identity.Web
dotnet add package Microsoft.Identity.Web.UI
```

### Step 2: Update appsettings.json

Update `ConferenceHub/appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  },
  "AzureStorage": {
    "ConnectionString": "YOUR_STORAGE_CONNECTION_STRING"
  },
  "AzureFunctions": {
    "SendConfirmationUrl": "http://localhost:7071/api/SendConfirmation",
    "FunctionKey": ""
  }
}
```

### Step 3: Update Program.cs

Update `ConferenceHub/Program.cs`:
```csharp
using ConferenceHub.Services;
using ConferenceHub.Models;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add Microsoft Identity authentication
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OrganizerOnly", policy =>
        policy.RequireRole("Organizer"));
    options.AddPolicy("RequireAuthentication", policy =>
        policy.RequireAuthenticatedUser());
});

// Add services to the container with authorization
builder.Services.AddControllersWithViews(options =>
{
    // Require authentication by default for all controllers
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

// Add Razor Pages for Microsoft Identity UI
builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

builder.Services.AddSingleton<IDataService, DataService>();

// Configure Azure Functions settings
builder.Services.Configure<AzureFunctionsConfig>(
    builder.Configuration.GetSection("AzureFunctions"));

// Add HttpClient for calling Azure Functions
builder.Services.AddHttpClient();

// Configure Azure Storage services
var storageConnectionString = builder.Configuration["AzureStorage:ConnectionString"];
builder.Services.AddSingleton<IBlobStorageService>(sp => 
    new BlobStorageService(storageConnectionString!, sp.GetRequiredService<ILogger<BlobStorageService>>()));
builder.Services.AddSingleton<IAuditLogService>(sp => 
    new AuditLogService(storageConnectionString!, sp.GetRequiredService<ILogger<AuditLogService>>()));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
```

---

## Part 3: Update Controllers with Authorization

### Step 1: Update HomeController

Update `Controllers/HomeController.cs`:
```csharp
using ConferenceHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ConferenceHub.Controllers
{
    [AllowAnonymous] // Home page accessible to everyone
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
```

### Step 2: Update SessionsController

Update `Controllers/SessionsController.cs`:
```csharp
using ConferenceHub.Models;
using ConferenceHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace ConferenceHub.Controllers
{
    [Authorize] // Require authentication for all actions
    public class SessionsController : Controller
    {
        private readonly IDataService _dataService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuditLogService _auditLogService;
        private readonly AzureFunctionsConfig _functionsConfig;
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(
            IDataService dataService,
            IHttpClientFactory httpClientFactory,
            IAuditLogService auditLogService,
            IOptions<AzureFunctionsConfig> functionsConfig,
            ILogger<SessionsController> logger)
        {
            _dataService = dataService;
            _httpClientFactory = httpClientFactory;
            _auditLogService = auditLogService;
            _functionsConfig = functionsConfig.Value;
            _logger = logger;
        }

        // GET: Sessions
        [AllowAnonymous] // Allow unauthenticated users to view sessions
        public async Task<IActionResult> Index()
        {
            var sessions = await _dataService.GetSessionsAsync();
            return View(sessions);
        }

        // GET: Sessions/Details/5
        [AllowAnonymous] // Allow unauthenticated users to view details
        public async Task<IActionResult> Details(int id)
        {
            var session = await _dataService.GetSessionByIdAsync(id);
            if (session == null)
            {
                return NotFound();
            }
            return View(session);
        }

        // POST: Sessions/Register
        [HttpPost]
        [Authorize] // Must be authenticated to register
        public async Task<IActionResult> Register(int sessionId, string attendeeName, string attendeeEmail)
        {
            // Get authenticated user info
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? attendeeEmail;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? attendeeName;

            var session = await _dataService.GetSessionByIdAsync(sessionId);
            if (session == null)
            {
                return NotFound();
            }

            if (session.CurrentRegistrations >= session.Capacity)
            {
                TempData["Error"] = "This session is at full capacity.";
                return RedirectToAction(nameof(Details), new { id = sessionId });
            }

            var registration = new Registration
            {
                SessionId = sessionId,
                AttendeeName = userName,
                AttendeeEmail = userEmail
            };

            await _dataService.AddRegistrationAsync(registration);

            // Log to audit table
            await _auditLogService.LogRegistrationAsync(sessionId, session.Title, userName, userEmail);

            // Call Azure Function to send confirmation email
            await SendConfirmationEmailAsync(session, userName, userEmail);

            TempData["Success"] = "Successfully registered for the session!";
            
            return RedirectToAction(nameof(Details), new { id = sessionId });
        }

        // GET: Sessions/MyRegistrations
        [Authorize]
        public async Task<IActionResult> MyRegistrations()
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(userEmail))
            {
                return RedirectToAction(nameof(Index));
            }

            var allRegistrations = await _dataService.GetAllRegistrationsAsync();
            var myRegistrations = allRegistrations.Where(r => r.AttendeeEmail == userEmail).ToList();

            var sessions = await _dataService.GetSessionsAsync();
            var mySessionIds = myRegistrations.Select(r => r.SessionId).ToHashSet();
            var mySessions = sessions.Where(s => mySessionIds.Contains(s.Id)).ToList();

            return View(mySessions);
        }

        private async Task SendConfirmationEmailAsync(Session session, string attendeeName, string attendeeEmail)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                
                var registrationRequest = new
                {
                    sessionId = session.Id,
                    sessionTitle = session.Title,
                    attendeeName = attendeeName,
                    attendeeEmail = attendeeEmail,
                    sessionStartTime = session.StartTime,
                    room = session.Room
                };

                var json = JsonSerializer.Serialize(registrationRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = _functionsConfig.SendConfirmationUrl;
                if (!string.IsNullOrEmpty(_functionsConfig.FunctionKey))
                {
                    url += $"?code={_functionsConfig.FunctionKey}";
                }

                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Confirmation email sent successfully for {Email}", attendeeEmail);
                }
                else
                {
                    _logger.LogWarning("Failed to send confirmation email. Status: {Status}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling SendConfirmation function");
            }
        }
    }
}
```

### Step 3: Update OrganizerController

Update `Controllers/OrganizerController.cs`:
```csharp
using ConferenceHub.Models;
using ConferenceHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceHub.Controllers
{
    [Authorize(Policy = "OrganizerOnly")] // Require Organizer role
    public class OrganizerController : Controller
    {
        private readonly IDataService _dataService;
        private readonly IBlobStorageService _blobStorageService;
        private readonly IAuditLogService _auditLogService;
        private readonly ILogger<OrganizerController> _logger;

        public OrganizerController(
            IDataService dataService,
            IBlobStorageService blobStorageService,
            IAuditLogService auditLogService,
            ILogger<OrganizerController> logger)
        {
            _dataService = dataService;
            _blobStorageService = blobStorageService;
            _auditLogService = auditLogService;
            _logger = logger;
        }

        // ... All existing actions remain the same ...
        // Index, Create, Edit, Delete, Registrations, UploadSlides, AuditLogs
    }
}
```

---

## Part 4: Update Views with User Information

### Step 1: Update Shared Layout

Update `Views/Shared/_Layout.cshtml` to add login/logout:
```cshtml
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - ConferenceHub</title>
    <link rel="stylesheet" href="~/lib/bootstrap/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.10.0/font/bootstrap-icons.css">
</head>
<body>
    <header>
        <nav class="navbar navbar-expand-sm navbar-toggleable-sm navbar-light bg-white border-bottom box-shadow mb-3">
            <div class="container-fluid">
                <a class="navbar-brand" asp-area="" asp-controller="Home" asp-action="Index">
                    <i class="bi bi-calendar-event"></i> ConferenceHub
                </a>
                <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target=".navbar-collapse" aria-controls="navbarSupportedContent"
                        aria-expanded="false" aria-label="Toggle navigation">
                    <span class="navbar-toggler-icon"></span>
                </button>
                <div class="navbar-collapse collapse d-sm-inline-flex justify-content-between">
                    <ul class="navbar-nav flex-grow-1">
                        <li class="nav-item">
                            <a class="nav-link text-dark" asp-area="" asp-controller="Home" asp-action="Index">Home</a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link text-dark" asp-area="" asp-controller="Sessions" asp-action="Index">Sessions</a>
                        </li>
                        @if (User.Identity?.IsAuthenticated == true)
                        {
                            <li class="nav-item">
                                <a class="nav-link text-dark" asp-area="" asp-controller="Sessions" asp-action="MyRegistrations">My Registrations</a>
                            </li>
                            @if (User.IsInRole("Organizer"))
                            {
                                <li class="nav-item">
                                    <a class="nav-link text-dark" asp-area="" asp-controller="Organizer" asp-action="Index">Organizer Dashboard</a>
                                </li>
                            }
                        }
                    </ul>
                    <ul class="navbar-nav">
                        @if (User.Identity?.IsAuthenticated == true)
                        {
                            <li class="nav-item dropdown">
                                <a class="nav-link dropdown-toggle" href="#" id="userDropdown" role="button" data-bs-toggle="dropdown" aria-expanded="false">
                                    <i class="bi bi-person-circle"></i> @User.Identity.Name
                                    @if (User.IsInRole("Organizer"))
                                    {
                                        <span class="badge bg-primary">Organizer</span>
                                    }
                                </a>
                                <ul class="dropdown-menu dropdown-menu-end" aria-labelledby="userDropdown">
                                    <li><h6 class="dropdown-header">@User.FindFirst("preferred_username")?.Value</h6></li>
                                    <li><hr class="dropdown-divider"></li>
                                    <li>
                                        <form method="post" asp-area="MicrosoftIdentity" asp-controller="Account" asp-action="SignOut">
                                            <button type="submit" class="dropdown-item">
                                                <i class="bi bi-box-arrow-right"></i> Sign out
                                            </button>
                                        </form>
                                    </li>
                                </ul>
                            </li>
                        }
                        else
                        {
                            <li class="nav-item">
                                <a class="nav-link text-dark" asp-area="MicrosoftIdentity" asp-controller="Account" asp-action="SignIn">
                                    <i class="bi bi-box-arrow-in-right"></i> Sign in
                                </a>
                            </li>
                        }
                    </ul>
                </div>
            </div>
        </nav>
    </header>
    <div class="container">
        <main role="main" class="pb-3">
            @RenderBody()
        </main>
    </div>

    <footer class="border-top footer text-muted">
        <div class="container">
            &copy; 2024 - ConferenceHub - <a asp-area="" asp-controller="Home" asp-action="Privacy">Privacy</a>
        </div>
    </footer>
    <script src="~/lib/jquery/dist/jquery.min.js"></script>
    <script src="~/lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
    <script src="~/js/site.js" asp-append-version="true"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

### Step 2: Create My Registrations View

Create `Views/Sessions/MyRegistrations.cshtml`:
```cshtml
@model List<ConferenceHub.Models.Session>

@{
    ViewData["Title"] = "My Registrations";
}

<div class="container mt-4">
    <div class="d-flex justify-content-between align-items-center mb-4">
        <h2><i class="bi bi-calendar-check"></i> My Registered Sessions</h2>
        <a asp-action="Index" class="btn btn-outline-primary">
            <i class="bi bi-search"></i> Browse Sessions
        </a>
    </div>

    @if (Model.Any())
    {
        <div class="row">
            @foreach (var session in Model.OrderBy(s => s.StartTime))
            {
                <div class="col-md-6 mb-4">
                    <div class="card h-100 @(session.StartTime < DateTime.Now ? "border-secondary" : "border-primary")">
                        <div class="card-header @(session.StartTime < DateTime.Now ? "bg-secondary" : "bg-primary") text-white">
                            <h5 class="card-title mb-0">@session.Title</h5>
                            @if (session.StartTime < DateTime.Now)
                            {
                                <span class="badge bg-light text-dark">Completed</span>
                            }
                            else if (session.StartTime < DateTime.Now.AddHours(2))
                            {
                                <span class="badge bg-warning">Starting Soon</span>
                            }
                        </div>
                        <div class="card-body">
                            <p class="mb-2">
                                <strong><i class="bi bi-person"></i> Speaker:</strong> @session.Speaker
                            </p>
                            <p class="mb-2">
                                <strong><i class="bi bi-clock"></i> Time:</strong> 
                                @session.StartTime.ToString("MMM dd, yyyy h:mm tt") - @session.EndTime.ToString("h:mm tt")
                            </p>
                            <p class="mb-2">
                                <strong><i class="bi bi-geo-alt"></i> Location:</strong> @session.Room
                            </p>
                            <p class="mb-3">@session.Description</p>
                            
                            @if (!string.IsNullOrEmpty(session.SlideUrl))
                            {
                                <a href="@session.SlideUrl" target="_blank" class="btn btn-sm btn-outline-primary">
                                    <i class="bi bi-file-earmark-pdf"></i> View Slides
                                </a>
                            }
                        </div>
                        <div class="card-footer">
                            <small class="text-muted">
                                Registered: <i class="bi bi-check-circle-fill text-success"></i>
                            </small>
                            <a asp-action="Details" asp-route-id="@session.Id" class="btn btn-sm btn-outline-info float-end">
                                View Details
                            </a>
                        </div>
                    </div>
                </div>
            }
        </div>
    }
    else
    {
        <div class="alert alert-info">
            <h5><i class="bi bi-info-circle"></i> No Registrations Yet</h5>
            <p>You haven't registered for any sessions yet. Browse our available sessions to get started!</p>
            <a asp-action="Index" class="btn btn-primary">
                <i class="bi bi-search"></i> Browse Sessions
            </a>
        </div>
    }
</div>
```

### Step 3: Update Session Details View

Update `Views/Sessions/Details.cshtml` to use authenticated user info:
Find the registration form and update it:
```cshtml
@if (User.Identity?.IsAuthenticated == true)
{
    <form asp-action="Register" method="post">
        <input type="hidden" name="sessionId" value="@Model.Id" />
        <input type="hidden" name="attendeeName" value="@User.Identity.Name" />
        <input type="hidden" name="attendeeEmail" value="@User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value" />
        
        <button type="submit" class="btn btn-primary btn-lg w-100" @(isSessionFull ? "disabled" : "")>
            <i class="bi bi-person-plus"></i> @(isSessionFull ? "Session Full" : "Register for This Session")
        </button>
    </form>
}
else
{
    <div class="alert alert-warning">
        <i class="bi bi-exclamation-triangle"></i> Please <a asp-area="MicrosoftIdentity" asp-controller="Account" asp-action="SignIn">sign in</a> to register for this session.
    </div>
}
```

---

## Part 5: Configure Azure App Service Authentication

### Step 1: Enable Authentication in Azure

PowerShell:
```powershell
# Configure authentication for the App Service
az webapp auth update `
  --resource-group $RG_NAME `
  --name $APP_NAME `
  --enabled true `
  --action LoginWithAzureActiveDirectory `
  --aad-client-id $appId `
  --aad-client-secret $clientSecret `
  --aad-token-issuer-url "https://login.microsoftonline.com/$tenantId/v2.0"
```
Bash:
```bash
# Configure authentication for the App Service
az webapp auth update \
  --resource-group $RG_NAME \
  --name $APP_NAME \
  --enabled true \
  --action LoginWithAzureActiveDirectory \
  --aad-client-id $appId \
  --aad-client-secret $clientSecret \
  --aad-token-issuer-url "https://login.microsoftonline.com/$tenantId/v2.0"
```

### Step 2: Add App Settings

PowerShell:
```powershell
# Add Azure AD configuration to App Service
az webapp config appsettings set `
  --name $APP_NAME `
  --resource-group $RG_NAME `
  --settings `
    AzureAd__Instance="https://login.microsoftonline.com/" `
    AzureAd__TenantId="$tenantId" `
    AzureAd__ClientId="$appId" `
    AzureAd__ClientSecret="$clientSecret"
```
Bash:
```bash
# Add Azure AD configuration to App Service
az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RG_NAME \
  --settings \
    AzureAd__Instance="https://login.microsoftonline.com/" \
    AzureAd__TenantId="$tenantId" \
    AzureAd__ClientId="$appId" \
    AzureAd__ClientSecret="$clientSecret"
```

---

## Part 6: Secure Azure Functions with JWT

### Step 1: Update Functions Project

Add to `ConferenceHubFunctions/ConferenceHubFunctions.csproj`:
PowerShell:
```powershell
cd ../ConferenceHubFunctions
dotnet add package Microsoft.Identity.Web
```
Bash:
```bash
cd ../ConferenceHubFunctions
dotnet add package Microsoft.Identity.Web
```

### Step 2: Create Authorization Helper

Create `ConferenceHubFunctions/Authorization/JwtValidator.cs`:
```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ConferenceHubFunctions.Authorization
{
    public class JwtValidator
    {
        private readonly IConfiguration _configuration;
        private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;

        public JwtValidator(IConfiguration configuration)
        {
            _configuration = configuration;
            var tenantId = _configuration["AzureAd:TenantId"];
            var metadataAddress = $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration";
            _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever());
        }

        public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
        {
            try
            {
                var config = await _configManager.GetConfigurationAsync();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = config.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _configuration["AzureAd:ClientId"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = config.SigningKeys,
                    ValidateLifetime = true
                };

                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, validationParameters, out _);
                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}
```

### Step 3: Update SendConfirmation Function

Update `ConferenceHubFunctions/SendConfirmation.cs` to validate JWT:
```csharp
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ConferenceHubFunctions.Authorization;

namespace ConferenceHubFunctions
{
    public class SendConfirmation
    {
        private readonly ILogger _logger;
        private readonly JwtValidator _jwtValidator;

        public SendConfirmation(ILoggerFactory loggerFactory, JwtValidator jwtValidator)
        {
            _logger = loggerFactory.CreateLogger<SendConfirmation>();
            _jwtValidator = jwtValidator;
        }

        [Function("SendConfirmation")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("SendConfirmation function triggered");

            // Validate JWT token
            if (!req.Headers.TryGetValues("Authorization", out var authHeaders))
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Missing Authorization header");
                return unauthorizedResponse;
            }

            var token = authHeaders.FirstOrDefault()?.Replace("Bearer ", "");
            if (string.IsNullOrEmpty(token))
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Invalid Authorization header");
                return unauthorizedResponse;
            }

            var principal = await _jwtValidator.ValidateTokenAsync(token);
            if (principal == null)
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Invalid or expired token");
                return unauthorizedResponse;
            }

            // Process the request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var registrationRequest = JsonSerializer.Deserialize<RegistrationRequest>(requestBody);

            if (registrationRequest == null)
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid request body");
                return badResponse;
            }

            _logger.LogInformation("Sending confirmation email to {Email} for session {SessionTitle}",
                registrationRequest.AttendeeEmail, registrationRequest.SessionTitle);

            // Simulate email sending
            await Task.Delay(100);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync($"Confirmation email sent to {registrationRequest.AttendeeEmail}");

            return response;
        }

        private class RegistrationRequest
        {
            public int SessionId { get; set; }
            public string SessionTitle { get; set; } = string.Empty;
            public string AttendeeName { get; set; } = string.Empty;
            public string AttendeeEmail { get; set; } = string.Empty;
            public DateTime SessionStartTime { get; set; }
            public string Room { get; set; } = string.Empty;
        }
    }
}
```

### Step 4: Update Function Startup Configuration

Update `ConferenceHubFunctions/Program.cs`:
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ConferenceHubFunctions.Authorization;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<JwtValidator>();
    })
    .Build();

host.Run();
```

---

## Part 7: Deploy and Test

### Step 1: Deploy Web App

PowerShell:
```powershell
cd ../ConferenceHub
dotnet publish -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./app.zip -Force
az webapp deploy `
  --resource-group $RG_NAME `
  --name $APP_NAME `
  --src-path ./app.zip
  --type zip
```
Bash:
```bash
cd ../ConferenceHub
dotnet publish -c Release -o ./publish
cd publish
zip -r ../app.zip .
cd ..
az webapp deploy \
  --resource-group $RG_NAME \
  --name $APP_NAME \
  --src-path ./app.zip
  --type zip
```

### Step 2: Deploy Functions

PowerShell:
```powershell
cd ../ConferenceHubFunctions
func azure functionapp publish $FUNC_APP_NAME
```
Bash:
```bash
cd ../ConferenceHubFunctions
func azure functionapp publish $FUNC_APP_NAME
```

### Step 3: Test Authentication Flow

1. **Test Anonymous Access**:
   - Navigate to https://conferencehub-demo-az204reinke.azurewebsites.net
   - View sessions list (should work without login)
   - Click on a session detail (should work)
   - Try to register (should prompt for login)

2. **Test Attendee Role**:
   - Sign in with a user assigned the Attendee role
   - Register for a session (should work)
   - View "My Registrations" (should show registered sessions)
   - Try to access Organizer Dashboard (should be denied)

3. **Test Organizer Role**:
   - Sign in with a user assigned the Organizer role
   - Access Organizer Dashboard (should work)
   - Create, edit, delete sessions (should work)
   - View registrations and audit logs (should work)
   - Upload slides (should work)

4. **Test Sign Out**:
   - Click on user dropdown
   - Click "Sign out"
   - Verify redirected to signed-out page
   - Verify can no longer access protected pages

---

## Part 8: Add Custom Claims and Enhance Security

### Step 1: Add Custom Claims

Create `Services/IUserProfileService.cs`:
```csharp
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
```

Create `Services/UserProfileService.cs`:
```csharp
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
```

Register in `Program.cs`:
```csharp
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
```

---

## Summary

You've successfully:
- ✅ Integrated Microsoft Entra ID authentication with OpenID Connect
- ✅ Implemented role-based authorization (Organizer/Attendee)
- ✅ Secured controllers and actions with [Authorize] attributes
- ✅ Added user profile display with sign-in/sign-out functionality
- ✅ Protected Azure Functions with JWT token validation
- ✅ Created personalized "My Registrations" view
- ✅ Configured Azure App Service authentication

## Next Steps

In **Learning Path 7**, you'll:
- Store secrets in **Azure Key Vault** (connection strings, client secrets)
- Use **Azure App Configuration** for centralized configuration
- Implement **Feature Flags** for controlled feature rollout
- Secure all sensitive configuration with managed identities

---

## Troubleshooting

### Cannot sign in
- Verify App Registration redirect URIs include your local and Azure URLs
- Check TenantId and ClientId in appsettings.json
- Ensure user is added to Enterprise App in Entra ID

### Access Denied to Organizer Dashboard
- Verify user is assigned the Organizer role in Entra ID
- Check app roles are properly configured in App Registration
- Review claims in JWT token (use jwt.ms to decode)

### JWT validation fails in Functions
- Ensure Authorization header is being sent: `Bearer {token}`
- Verify tenant ID and client ID in Function configuration
- Check token expiration time

### User roles not appearing
- Verify app roles are defined in App Registration manifest
- Ensure users are assigned roles through Enterprise Application
- Check that roles claim is being included in token

## Azure DevOps Pipeline (Incremental Deployment)
- Pipeline: `Learning Path/06-Authentication/azure-pipelines.yml`
- Bicep: `Learning Path/06-Authentication/infra.bicep`
- Required variables: `azureSubscription`, `resourceGroupName`, `location`, `mainWebAppName`, `storageAccountName`, `cosmosAccountName`, `cosmosDatabaseName`, `functionAppName`, `azureAdTenantId`, `azureAdClientId`, `AzureAdClientSecret`
- Notes: The pipeline updates web app settings for Entra ID auth and keeps Storage/Cosmos/Functions settings aligned.
