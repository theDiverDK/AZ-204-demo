# Learning Path 7: Azure Key Vault & App Configuration

## Overview
In this learning path, you'll secure sensitive application secrets using Azure Key Vault and centralize configuration management with Azure App Configuration, including feature flags for controlled feature rollout.

## What You'll Build
1. **Azure Key Vault**: Secure storage for connection strings, client secrets, and API keys
2. **Managed Identity**: Secure authentication without storing credentials
3. **Azure App Configuration**: Centralized configuration management
4. **Feature Flags**: Control feature availability without redeployment

## Prerequisites
- Completed Learning Path 1-6
- Azure subscription with permissions to create Key Vault and App Configuration
- Application with Entra ID authentication configured

## Variables
Use base variables from `01-Init.md` (do not redefine):  
`location`, `resourceGroupName`, `random`, `appServicePlanName`, `webAppName`, `appRuntime`, `publishDir`, `zipPath`

Additional variables for this learning path:
```bash
keyVaultName="kv-conferencehub-$random"
appConfigName="appconfig-conferencehub-$random"
```

---

## Part 1: Create Azure Key Vault

### Step 1: Create Key Vault Resource

```powershell
# Create Key Vault
az keyvault create `
  --name $keyVaultName `
  --resource-group $resourceGroupName `
  --location $location `
  --enable-rbac-authorization true

# Get Key Vault URI
$keyVaultUri = az keyvault show `
  --name $keyVaultName `
  --resource-group $resourceGroupName `
  --query properties.vaultUri `
  --output tsv

Write-Host "Key Vault URI: $keyVaultUri"
```

**Bash**
```bash
# Create Key Vault
az keyvault create \
  --name "$keyVaultName" \
  --resource-group "$resourceGroupName" \
  --location "$location" \
  --enable-rbac-authorization true

# Get Key Vault URI
keyVaultUri=$(az keyvault show \
  --name "$keyVaultName" \
  --resource-group "$resourceGroupName" \
  --query properties.vaultUri \
  --output tsv)

echo "Key Vault URI: $keyVaultUri"
```

### Step 2: Enable Managed Identity for App Service

```powershell
# Enable system-assigned managed identity for Web App
az webapp identity assign `
  --name $webAppName `
  --resource-group $resourceGroupName

# Get the managed identity principal ID
$webAppPrincipalId = az webapp identity show `
  --name $webAppName `
  --resource-group $resourceGroupName `
  --query principalId `
  --output tsv

Write-Host "Web App Principal ID: $webAppPrincipalId"

# Enable managed identity for Function App
az functionapp identity assign `
  --name $functionAppName `
  --resource-group $resourceGroupName

# Get function app principal ID
$funcPrincipalId = az functionapp identity show `
  --name $functionAppName `
  --resource-group $resourceGroupName `
  --query principalId `
  --output tsv

Write-Host "Function App Principal ID: $funcPrincipalId"
```

**Bash**
```bash
# Enable system-assigned managed identity for Web App
az webapp identity assign \
  --name "$webAppName" \
  --resource-group "$resourceGroupName"

# Get the managed identity principal ID
webAppPrincipalId=$(az webapp identity show \
  --name "$webAppName" \
  --resource-group "$resourceGroupName" \
  --query principalId \
  --output tsv)

echo "Web App Principal ID: $webAppPrincipalId"

# Enable managed identity for Function App
az functionapp identity assign \
  --name "$functionAppName" \
  --resource-group "$resourceGroupName"

# Get function app principal ID
funcPrincipalId=$(az functionapp identity show \
  --name "$functionAppName" \
  --resource-group "$resourceGroupName" \
  --query principalId \
  --output tsv)

echo "Function App Principal ID: $funcPrincipalId"
```

### Step 3: Grant Access to Key Vault

```powershell
# Get your Key Vault resource ID
$keyVaultId = az keyvault show `
  --name $keyVaultName `
  --resource-group $resourceGroupName `
  --query id `
  --output tsv

# Assign "Key Vault Secrets Officer" role to yourself (for adding secrets)
$currentUserId = az ad signed-in-user show --query id -o tsv
az role assignment create `
  --role "Key Vault Secrets Officer" `
  --assignee $currentUserId `
  --scope $keyVaultId

# Assign "Key Vault Secrets User" role to Web App managed identity
az role assignment create `
  --role "Key Vault Secrets User" `
  --assignee $webAppPrincipalId `
  --scope $keyVaultId

# Assign "Key Vault Secrets User" role to Function App managed identity
az role assignment create `
  --role "Key Vault Secrets User" `
  --assignee $funcPrincipalId `
  --scope $keyVaultId

Write-Host "Waiting for role assignments to propagate (30 seconds)..."
Start-Sleep -Seconds 30
```

**Bash**
```bash
# Get your Key Vault resource ID
keyVaultId=$(az keyvault show \
  --name "$keyVaultName" \
  --resource-group "$resourceGroupName" \
  --query id \
  --output tsv)

# Assign "Key Vault Secrets Officer" role to yourself (for adding secrets)
currentUserId=$(az ad signed-in-user show --query id -o tsv)
az role assignment create \
  --role "Key Vault Secrets Officer" \
  --assignee "$currentUserId" \
  --scope "$keyVaultId"

# Assign "Key Vault Secrets User" role to Web App managed identity
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee "$webAppPrincipalId" \
  --scope "$keyVaultId"

# Assign "Key Vault Secrets User" role to Function App managed identity
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee "$funcPrincipalId" \
  --scope "$keyVaultId"

echo "Waiting for role assignments to propagate (30 seconds)..."
sleep 30
```
### Step 4: Add Secrets to Key Vault

```powershell
# Add Storage connection string
$storageConnectionString = az storage account show-connection-string `
  --name $storageAccountName `
  --resource-group $resourceGroupName `
  --output tsv

az keyvault secret set `
  --vault-name $keyVaultName `
  --name "AzureStorage--ConnectionString" `
  --value $storageConnectionString

# Add Entra ID Client Secret
az keyvault secret set `
  --vault-name $keyVaultName `
  --name "AzureAd--ClientSecret" `
  --value "YOUR_CLIENT_SECRET_FROM_LEARNING_PATH_6"

# Add Cosmos DB connection string (if using Cosmos DB from Learning Path 4)
az keyvault secret set `
  --vault-name $keyVaultName `
  --name "CosmosDb--ConnectionString" `
  --value "YOUR_COSMOS_CONNECTION_STRING"

Write-Host "Secrets added to Key Vault"
```

**Bash**
```bash
# Add Storage connection string
storageConnectionString=$(az storage account show-connection-string \
  --name "$storageAccountName" \
  --resource-group "$resourceGroupName" \
  --output tsv)

az keyvault secret set \
  --vault-name "$keyVaultName" \
  --name "AzureStorage--ConnectionString" \
  --value "$storageConnectionString"

# Add Entra ID Client Secret
az keyvault secret set \
  --vault-name "$keyVaultName" \
  --name "AzureAd--ClientSecret" \
  --value "YOUR_CLIENT_SECRET_FROM_LEARNING_PATH_6"

# Add Cosmos DB connection string (if using Cosmos DB from Learning Path 4)
az keyvault secret set \
  --vault-name "$keyVaultName" \
  --name "CosmosDb--ConnectionString" \
  --value "YOUR_COSMOS_CONNECTION_STRING"

echo "Secrets added to Key Vault"
```

---

## Part 2: Update Web Application to Use Key Vault

### Step 1: Add NuGet Packages

```powershell
cd ConferenceHub
dotnet add package Azure.Identity
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
dotnet add package Azure.Security.KeyVault.Secrets
```

**Bash**
```bash
cd ConferenceHub
dotnet add package Azure.Identity
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
dotnet add package Azure.Security.KeyVault.Secrets
```
### Step 2: Update Program.cs

Update `ConferenceHub/Program.cs` to load secrets from Key Vault:
```csharp
using ConferenceHub.Services;
using ConferenceHub.Models;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

var builder = WebApplication.CreateBuilder(args);

// Add Key Vault configuration
if (!builder.Environment.IsDevelopment())
{
    var keyVaultUri = new Uri(builder.Configuration["KeyVault:VaultUri"]!);
    builder.Configuration.AddAzureKeyVault(
        keyVaultUri,
        new DefaultAzureCredential());
}

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
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

builder.Services.AddSingleton<IDataService, DataService>();

// Configure Azure Functions settings
builder.Services.Configure<AzureFunctionsConfig>(
    builder.Configuration.GetSection("AzureFunctions"));

builder.Services.AddHttpClient();

// Configure Azure Storage services - now reading from Key Vault
var storageConnectionString = builder.Configuration["AzureStorage:ConnectionString"];
builder.Services.AddSingleton<IBlobStorageService>(sp => 
    new BlobStorageService(storageConnectionString!, sp.GetRequiredService<ILogger<BlobStorageService>>()));
builder.Services.AddSingleton<IAuditLogService>(sp => 
    new AuditLogService(storageConnectionString!, sp.GetRequiredService<ILogger<AuditLogService>>()));

// Add user profile service
builder.Services.AddScoped<IUserProfileService, UserProfileService>();

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

### Step 3: Update appsettings.json

Update `ConferenceHub/appsettings.json` to reference Key Vault:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "KeyVault": {
    "VaultUri": "https://$keyVaultName.vault.azure.net/"
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  },
  "AzureFunctions": {
    "SendConfirmationUrl": "https://$functionAppName.azurewebsites.net/api/SendConfirmation",
    "FunctionKey": ""
  }
}
```

Note: Connection strings and client secret are now loaded from Key Vault automatically.

---

## Part 3: Create Azure App Configuration

### Step 1: Create App Configuration Resource

```powershell
# Create App Configuration store
az appconfig create `
  --name $appConfigName `
  --resource-group $resourceGroupName `
  --location $location `
  --sku Standard

# Get App Configuration endpoint
$appConfigEndpoint = az appconfig show `
  --name $appConfigName `
  --resource-group $resourceGroupName `
  --query endpoint `
  --output tsv

Write-Host "App Configuration Endpoint: $appConfigEndpoint"
```

**Bash**
```bash
# Create App Configuration store
az appconfig create \
  --name $appConfigName \
  --resource-group $resourceGroupName \
  --location $location \
  --sku Standard

# Get App Configuration endpoint
appConfigEndpoint=$(az appconfig show \
  --name $appConfigName \
  --resource-group $resourceGroupName \
  --query endpoint \
  --output tsv)

echo App Configuration Endpoint: $appConfigEndpoint
```

### Step 2: Grant Access to App Configuration

```powershell
# Get App Configuration resource ID
$appConfigId = az appconfig show `
  --name $appConfigName `
  --resource-group $resourceGroupName `
  --query id `
  --output tsv

# Assign "App Configuration Data Owner" role to yourself
az role assignment create `
  --role "App Configuration Data Owner" `
  --assignee $currentUserId `
  --scope $appConfigId

# Assign "App Configuration Data Reader" role to Web App
az role assignment create `
  --role "App Configuration Data Reader" `
  --assignee $webAppPrincipalId `
  --scope $appConfigId

# Assign "App Configuration Data Reader" role to Function App
az role assignment create `
  --role "App Configuration Data Reader" `
  --assignee $funcPrincipalId `
  --scope $appConfigId

Write-Host "Waiting for role assignments to propagate (30 seconds)..."
Start-Sleep -Seconds 30
```

**Bash**
```bash
# Get App Configuration resource ID
appConfigId=$(az appconfig show \
  --name $appConfigName \
  --resource-group $resourceGroupName \
  --query id \
  --output tsv)

# Assign "App Configuration Data Owner" role to yourself
az role assignment create \
  --role "App Configuration Data Owner" \
  --assignee $currentUserId \
  --scope $appConfigId

# Assign "App Configuration Data Reader" role to Web App
az role assignment create \
  --role "App Configuration Data Reader" \
  --assignee $webAppPrincipalId \
  --scope $appConfigId

# Assign "App Configuration Data Reader" role to Function App
az role assignment create \
  --role "App Configuration Data Reader" \
  --assignee $funcPrincipalId \
  --scope $appConfigId

echo Waiting for role assignments to propagate (30 seconds)...
sleep 30
```

### Step 3: Add Configuration Values

```powershell
# Add application settings
az appconfig kv set `
  --name $appConfigName `
  --key "ConferenceHub:MaxSessionCapacity" `
  --value "100" `
  --yes

az appconfig kv set `
  --name $appConfigName `
  --key "ConferenceHub:RegistrationOpenDays" `
  --value "30" `
  --yes

az appconfig kv set `
  --name $appConfigName `
  --key "ConferenceHub:AllowWaitlist" `
  --value "true" `
  --yes

az appconfig kv set `
  --name $appConfigName `
  --key "Email:FromAddress" `
  --value "noreply@conferencehub.com" `
  --yes

az appconfig kv set `
  --name $appConfigName `
  --key "Email:FromName" `
  --value "ConferenceHub Notifications" `
  --yes

Write-Host "Configuration values added"
```

**Bash**
```bash
# Add application settings
az appconfig kv set \
  --name $appConfigName \
  --key "ConferenceHub:MaxSessionCapacity" \
  --value "100" \
  --yes

az appconfig kv set \
  --name $appConfigName \
  --key "ConferenceHub:RegistrationOpenDays" \
  --value "30" \
  --yes

az appconfig kv set \
  --name $appConfigName \
  --key "ConferenceHub:AllowWaitlist" \
  --value "true" \
  --yes

az appconfig kv set \
  --name $appConfigName \
  --key "Email:FromAddress" \
  --value "noreply@conferencehub.com" \
  --yes

az appconfig kv set \
  --name $appConfigName \
  --key "Email:FromName" \
  --value "ConferenceHub Notifications" \
  --yes

echo Configuration values added
```

---

## Part 4: Implement Feature Flags

### Step 1: Create Feature Flags in App Configuration

```powershell
# Create feature flag for slide upload
az appconfig feature set `
  --name $appConfigName `
  --feature "SlideUpload" `
  --yes `
  --description "Enable speaker slide upload functionality"

az appconfig feature enable `
  --name $appConfigName `
  --feature "SlideUpload" `
  --yes

# Create feature flag for waitlist
az appconfig feature set `
  --name $appConfigName `
  --feature "Waitlist" `
  --yes `
  --description "Enable waitlist when sessions are full"

az appconfig feature enable `
  --name $appConfigName `
  --feature "Waitlist" `
  --yes

# Create feature flag for session ratings
az appconfig feature set `
  --name $appConfigName `
  --feature "SessionRatings" `
  --yes `
  --description "Enable attendees to rate sessions"

az appconfig feature disable `
  --name $appConfigName `
  --feature "SessionRatings" `
  --yes

# Create feature flag for live Q&A
az appconfig feature set `
  --name $appConfigName `
  --feature "LiveQA" `
  --yes `
  --description "Enable live Q&A during sessions"

az appconfig feature disable `
  --name $appConfigName `
  --feature "LiveQA" `
  --yes

Write-Host "Feature flags created"
```

**Bash**
```bash
# Create feature flag for slide upload
az appconfig feature set \
  --name $appConfigName \
  --feature "SlideUpload" \
  --yes \
  --description "Enable speaker slide upload functionality"

az appconfig feature enable \
  --name $appConfigName \
  --feature "SlideUpload" \
  --yes

# Create feature flag for waitlist
az appconfig feature set \
  --name $appConfigName \
  --feature "Waitlist" \
  --yes \
  --description "Enable waitlist when sessions are full"

az appconfig feature enable \
  --name $appConfigName \
  --feature "Waitlist" \
  --yes

# Create feature flag for session ratings
az appconfig feature set \
  --name $appConfigName \
  --feature "SessionRatings" \
  --yes \
  --description "Enable attendees to rate sessions"

az appconfig feature disable \
  --name $appConfigName \
  --feature "SessionRatings" \
  --yes

# Create feature flag for live Q&A
az appconfig feature set \
  --name $appConfigName \
  --feature "LiveQA" \
  --yes \
  --description "Enable live Q&A during sessions"

az appconfig feature disable \
  --name $appConfigName \
  --feature "LiveQA" \
  --yes

echo Feature flags created
```

### Step 2: Add App Configuration to Web App

Add NuGet packages:
```powershell
cd ConferenceHub
dotnet add package Microsoft.Azure.AppConfiguration.AspNetCore
dotnet add package Microsoft.FeatureManagement.AspNetCore
```

**Bash**
```bash
cd ConferenceHub
dotnet add package Microsoft.Azure.AppConfiguration.AspNetCore
dotnet add package Microsoft.FeatureManagement.AspNetCore
```

### Step 3: Update Program.cs for App Configuration

Update `ConferenceHub/Program.cs`:
```csharp
using ConferenceHub.Services;
using ConferenceHub.Models;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);

// Add App Configuration
if (!builder.Environment.IsDevelopment())
{
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        options.Connect(new Uri(builder.Configuration["AppConfiguration:Endpoint"]!), new DefaultAzureCredential())
            .Select("ConferenceHub:*")
            .Select("Email:*")
            .ConfigureRefresh(refresh =>
            {
                refresh.Register("ConferenceHub:MaxSessionCapacity", refreshAll: true)
                    .SetCacheExpiration(TimeSpan.FromMinutes(5));
            })
            .UseFeatureFlags(featureFlagOptions =>
            {
                featureFlagOptions.CacheExpirationInterval = TimeSpan.FromMinutes(5);
            });
    });

    // Add Key Vault configuration
    var keyVaultUri = new Uri(builder.Configuration["KeyVault:VaultUri"]!);
    builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
}

// Add Feature Management
builder.Services.AddFeatureManagement();

// Add App Configuration refresh middleware
builder.Services.AddAzureAppConfiguration();

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
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

builder.Services.AddSingleton<IDataService, DataService>();

// Configure Azure Functions settings
builder.Services.Configure<AzureFunctionsConfig>(
    builder.Configuration.GetSection("AzureFunctions"));

builder.Services.AddHttpClient();

// Configure Azure Storage services
var storageConnectionString = builder.Configuration["AzureStorage:ConnectionString"];
builder.Services.AddSingleton<IBlobStorageService>(sp => 
    new BlobStorageService(storageConnectionString!, sp.GetRequiredService<ILogger<BlobStorageService>>()));
builder.Services.AddSingleton<IAuditLogService>(sp => 
    new AuditLogService(storageConnectionString!, sp.GetRequiredService<ILogger<AuditLogService>>()));

// Add user profile service
builder.Services.AddScoped<IUserProfileService, UserProfileService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Use App Configuration refresh middleware
app.UseAzureAppConfiguration();

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

### Step 4: Update appsettings.json

Add App Configuration endpoint:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "KeyVault": {
    "VaultUri": "https://$keyVaultName.vault.azure.net/"
  },
  "AppConfiguration": {
    "Endpoint": "https://$appConfigName.azconfig.io"
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "ClientId": "YOUR_CLIENT_ID",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  },
  "AzureFunctions": {
    "SendConfirmationUrl": "https://$functionAppName.azurewebsites.net/api/SendConfirmation",
    "FunctionKey": ""
  }
}
```

---

## Part 5: Use Feature Flags in Controllers and Views

### Step 1: Update OrganizerController

Update `Controllers/OrganizerController.cs` to use feature flags:
```csharp
using ConferenceHub.Models;
using ConferenceHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;

namespace ConferenceHub.Controllers
{
    [Authorize(Policy = "OrganizerOnly")]
    public class OrganizerController : Controller
    {
        private readonly IDataService _dataService;
        private readonly IBlobStorageService _blobStorageService;
        private readonly IAuditLogService _auditLogService;
        private readonly IFeatureManager _featureManager;
        private readonly ILogger<OrganizerController> _logger;

        public OrganizerController(
            IDataService dataService,
            IBlobStorageService blobStorageService,
            IAuditLogService auditLogService,
            IFeatureManager featureManager,
            ILogger<OrganizerController> logger)
        {
            _dataService = dataService;
            _blobStorageService = blobStorageService;
            _auditLogService = auditLogService;
            _featureManager = featureManager;
            _logger = logger;
        }

        // ... existing Index, Create, Edit, Delete, Registrations methods ...

        // GET: Organizer/UploadSlides/5
        [FeatureGate("SlideUpload")]
        public async Task<IActionResult> UploadSlides(int id)
        {
            var session = await _dataService.GetSessionByIdAsync(id);
            if (session == null)
            {
                return NotFound();
            }
            return View(session);
        }

        // POST: Organizer/UploadSlides/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [FeatureGate("SlideUpload")]
        public async Task<IActionResult> UploadSlides(int id, IFormFile slideFile)
        {
            // ... existing implementation ...
        }

        // ... existing AuditLogs method ...
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
using Microsoft.FeatureManagement;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace ConferenceHub.Controllers
{
    [Authorize]
    public class SessionsController : Controller
    {
        private readonly IDataService _dataService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuditLogService _auditLogService;
        private readonly IFeatureManager _featureManager;
        private readonly AzureFunctionsConfig _functionsConfig;
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(
            IDataService dataService,
            IHttpClientFactory httpClientFactory,
            IAuditLogService auditLogService,
            IFeatureManager featureManager,
            IOptions<AzureFunctionsConfig> functionsConfig,
            ILogger<SessionsController> logger)
        {
            _dataService = dataService;
            _httpClientFactory = httpClientFactory;
            _auditLogService = auditLogService;
            _featureManager = featureManager;
            _functionsConfig = functionsConfig.Value;
            _logger = logger;
        }

        // ... existing Index, Details methods ...

        // POST: Sessions/Register
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Register(int sessionId, string attendeeName, string attendeeEmail)
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? attendeeEmail;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? attendeeName;

            var session = await _dataService.GetSessionByIdAsync(sessionId);
            if (session == null)
            {
                return NotFound();
            }

            // Check if session is full
            if (session.CurrentRegistrations >= session.Capacity)
            {
                // Check if waitlist is enabled
                var waitlistEnabled = await _featureManager.IsEnabledAsync("Waitlist");
                
                if (waitlistEnabled)
                {
                    TempData["Info"] = "This session is full. You have been added to the waitlist.";
                    // TODO: Implement waitlist logic
                }
                else
                {
                    TempData["Error"] = "This session is at full capacity.";
                }
                
                return RedirectToAction(nameof(Details), new { id = sessionId });
            }

            var registration = new Registration
            {
                SessionId = sessionId,
                AttendeeName = userName,
                AttendeeEmail = userEmail
            };

            await _dataService.AddRegistrationAsync(registration);
            await _auditLogService.LogRegistrationAsync(sessionId, session.Title, userName, userEmail);
            await SendConfirmationEmailAsync(session, userName, userEmail);

            TempData["Success"] = "Successfully registered for the session!";
            
            return RedirectToAction(nameof(Details), new { id = sessionId });
        }

        // ... existing MyRegistrations and SendConfirmationEmailAsync methods ...
    }
}
```

### Step 3: Update Views with Feature Flags

Update `Views/Organizer/Index.cshtml` to conditionally show upload button:
```cshtml
@model List<ConferenceHub.Models.Session>
@inject Microsoft.FeatureManagement.IFeatureManager FeatureManager

@{
    ViewData["Title"] = "Organizer Dashboard";
    var slideUploadEnabled = await FeatureManager.IsEnabledAsync("SlideUpload");
}

<!-- ... existing header ... -->

<div class="table-responsive">
    <table class="table table-hover">
        <thead>
            <!-- ... existing headers ... -->
        </thead>
        <tbody>
            @foreach (var session in Model)
            {
                <tr>
                    <!-- ... existing columns ... -->
                    <td>
                        <div class="btn-group" role="group">
                            <a asp-action="Edit" asp-route-id="@session.Id" class="btn btn-sm btn-outline-primary">
                                <i class="bi bi-pencil"></i> Edit
                            </a>
                            @if (slideUploadEnabled)
                            {
                                <a asp-action="UploadSlides" asp-route-id="@session.Id" class="btn btn-sm btn-outline-info">
                                    <i class="bi bi-upload"></i> Slides
                                </a>
                            }
                            <a asp-action="Registrations" asp-route-id="@session.Id" class="btn btn-sm btn-outline-success">
                                <i class="bi bi-people"></i> Registrations
                            </a>
                            <a asp-action="Delete" asp-route-id="@session.Id" class="btn btn-sm btn-outline-danger">
                                <i class="bi bi-trash"></i> Delete
                            </a>
                        </div>
                    </td>
                </tr>
            }
        </tbody>
    </table>
</div>
```

Update `Views/Sessions/Details.cshtml`:
```cshtml
@model ConferenceHub.Models.Session
@inject Microsoft.FeatureManagement.IFeatureManager FeatureManager

@{
    ViewData["Title"] = Model.Title;
    var slideUploadEnabled = await FeatureManager.IsEnabledAsync("SlideUpload");
}

<!-- ... existing content ... -->

@if (slideUploadEnabled && !string.IsNullOrEmpty(Model.SlideUrl))
{
    <div class="mt-3">
        <h5>Presentation Slides</h5>
        <a href="@Model.SlideUrl" target="_blank" class="btn btn-outline-primary">
            <i class="bi bi-file-earmark-pdf"></i> View Slides
        </a>
        <small class="text-muted d-block mt-1">
            Uploaded: @Model.SlideUploadedAt?.ToString("MMM dd, yyyy")
        </small>
    </div>
}
```

---

## Part 6: Create Configuration Settings Model

Create `Models/ConferenceHubSettings.cs`:
```csharp
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
```

Register in `Program.cs`:
```csharp
builder.Services.Configure<ConferenceHubSettings>(
    builder.Configuration.GetSection("ConferenceHub"));
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email"));
```

Use in controllers via dependency injection:
```csharp
private readonly IOptions<ConferenceHubSettings> _settings;

public OrganizerController(
    IDataService dataService,
    IOptions<ConferenceHubSettings> settings,
    // ... other dependencies
{
    _settings = settings;
    // ...
}
```

---

## Part 7: Deploy to Azure

### Step 1: Configure Web App Settings

```powershell
# Add Key Vault and App Configuration endpoints
az webapp config appsettings set `
  --name $webAppName `
  --resource-group $resourceGroupName `
  --settings `
    KeyVault__VaultUri="https://$keyVaultName.vault.azure.net/" `
    AppConfiguration__Endpoint="https://$appConfigName.azconfig.io"

# Remove sensitive settings (now in Key Vault)
az webapp config appsettings delete `
  --name $webAppName `
  --resource-group $resourceGroupName `
  --setting-names AzureStorage__ConnectionString AzureAd__ClientSecret
```

**Bash**
```bash
# Add Key Vault and App Configuration endpoints
az webapp config appsettings set \
  --name $webAppName \
  --resource-group $resourceGroupName \
  --settings \
    KeyVault__VaultUri="https://$keyVaultName.vault.azure.net/" \
    AppConfiguration__Endpoint="https://$appConfigName.azconfig.io"

# Remove sensitive settings (now in Key Vault)
az webapp config appsettings delete \
  --name $webAppName \
  --resource-group $resourceGroupName \
  --setting-names AzureStorage__ConnectionString AzureAd__ClientSecret
```

### Step 2: Deploy Updated Application

```powershell
cd ConferenceHub
dotnet publish -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./app.zip -Force
az webapp deployment source config-zip `
  --resource-group $resourceGroupName `
  --name $webAppName `
  --src ./app.zip
```

**Bash**
```bash
cd ConferenceHub                                                                        
  dotnet publish -c Release -o ./publish   
  cd publish && zip -r ../app.zip . && cd ..

  az webapp deploy \
    --resource-group "$resourceGroupName" \
    --name "$webAppName" \
    --src-path ./app.zip \
    --type zip
```

### Step 3: Verify Deployment

```powershell
# Check Web App logs
az webapp log tail `
  --name $webAppName `
  --resource-group $resourceGroupName
```

**Bash**
```bash
# Check Web App logs
az webapp log tail \
  --name $webAppName \
  --resource-group $resourceGroupName
```

---

## Part 8: Test Feature Flags

### Test 1: Enable/Disable Slide Upload

```powershell
# Disable slide upload
az appconfig feature disable `
  --name $appConfigName `
  --feature "SlideUpload" `
  --yes

# Wait for cache to expire (5 minutes) or restart app
az webapp restart `
  --name $webAppName `
  --resource-group $resourceGroupName

# Verify: Upload Slides button should disappear from Organizer Dashboard

# Re-enable
az appconfig feature enable `
  --name $appConfigName `
  --feature "SlideUpload" `
  --yes
```

**Bash**
```bash
# Disable slide upload
az appconfig feature disable \
  --name $appConfigName \
  --feature "SlideUpload" \
  --yes

# Wait for cache to expire (5 minutes) or restart app
az webapp restart \
  --name $webAppName \
  --resource-group $resourceGroupName

# Verify: Upload Slides button should disappear from Organizer Dashboard

# Re-enable
az appconfig feature enable \
  --name $appConfigName \
  --feature "SlideUpload" \
  --yes
```

### Test 2: Toggle Waitlist Feature

```powershell
# Disable waitlist
az appconfig feature disable `
  --name $appConfigName `
  --feature "Waitlist" `
  --yes

# Try registering for a full session - should show "Session Full" error

# Enable waitlist
az appconfig feature enable `
  --name $appConfigName `
  --feature "Waitlist" `
  --yes

# Try registering for a full session - should show "Added to waitlist" message
```

**Bash**
```bash
# Disable waitlist
az appconfig feature disable \
  --name $appConfigName \
  --feature "Waitlist" \
  --yes

# Try registering for a full session - should show "Session Full" error

# Enable waitlist
az appconfig feature enable \
  --name $appConfigName \
  --feature "Waitlist" \
  --yes

# Try registering for a full session - should show "Added to waitlist" message
```

### Test 3: Configuration Refresh

```powershell
# Update max capacity
az appconfig kv set `
  --name $appConfigName `
  --key "ConferenceHub:MaxSessionCapacity" `
  --value "150" `
  --yes

# Wait 5 minutes or restart app
# Configuration should be automatically refreshed
```

**Bash**
```bash
# Update max capacity
az appconfig kv set \
  --name $appConfigName \
  --key "ConferenceHub:MaxSessionCapacity" \
  --value "150" \
  --yes

# Wait 5 minutes or restart app
# Configuration should be automatically refreshed
```

---

## Part 9: Update Azure Functions to Use Key Vault

### Step 1: Add Configuration to Function App

```powershell
# Configure Key Vault reference for Functions
az functionapp config appsettings set `
  --name $functionAppName `
  --resource-group $resourceGroupName `
  --settings `
    KeyVault__VaultUri="https://$keyVaultName.vault.azure.net/" `
    AppConfiguration__Endpoint="https://$appConfigName.azconfig.io"
```

**Bash**
```bash
# Configure Key Vault reference for Functions
az functionapp config appsettings set \
  --name $functionAppName \
  --resource-group $resourceGroupName \
  --settings \
    KeyVault__VaultUri="https://$keyVaultName.vault.azure.net/" \
    AppConfiguration__Endpoint="https://$appConfigName.azconfig.io"
```

### Step 2: Update Functions Project

Add packages to `ConferenceHubFunctions/ConferenceHubFunctions.csproj`:
```powershell
cd ../ConferenceHubFunctions
dotnet add package Azure.Identity
dotnet add package Microsoft.Extensions.Configuration.AzureAppConfiguration
```

**Bash**
```bash
cd ../ConferenceHubFunctions
dotnet add package Azure.Identity
dotnet add package Microsoft.Extensions.Configuration.AzureAppConfiguration
```

Update `ConferenceHubFunctions/Program.cs`:
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ConferenceHubFunctions.Authorization;
using Azure.Identity;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        var settings = config.Build();
        
        // Add App Configuration
        var appConfigEndpoint = settings["AppConfiguration:Endpoint"];
        if (!string.IsNullOrEmpty(appConfigEndpoint))
        {
            config.AddAzureAppConfiguration(options =>
            {
                options.Connect(new Uri(appConfigEndpoint), new DefaultAzureCredential())
                    .Select("Email:*")
                    .UseFeatureFlags();
            });
        }

        // Add Key Vault
        var keyVaultUri = settings["KeyVault:VaultUri"];
        if (!string.IsNullOrEmpty(keyVaultUri))
        {
            config.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
        }
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton<JwtValidator>();
    })
    .Build();

host.Run();
```

---

## Summary

You've successfully:
- ✅ Created Azure Key Vault for secure secret storage
- ✅ Enabled Managed Identity for Web App and Function App
- ✅ Stored connection strings and secrets in Key Vault
- ✅ Created Azure App Configuration for centralized settings
- ✅ Implemented Feature Flags (SlideUpload, Waitlist, SessionRatings, LiveQA)
- ✅ Configured automatic configuration refresh
- ✅ Removed hardcoded secrets from appsettings.json
- ✅ Used Feature Management to control feature availability

## Next Steps

In **Learning Path 8**, you'll:
- Deploy **Azure API Management** in front of your APIs
- Implement **Rate Limiting** and **Throttling**
- Add **Subscription Keys** for API access
- Create **API Products** for different user tiers
- Configure **Backend Policies** for transformation

---

## Troubleshooting

### Cannot access Key Vault
- Verify managed identity is enabled on App Service/Function App
- Check RBAC role assignments (Key Vault Secrets User)
- Wait 5-10 minutes for role propagation
- Verify Key Vault URI is correct in configuration

### App Configuration not loading
- Verify managed identity has App Configuration Data Reader role
- Check endpoint URL is correct
- Ensure DefaultAzureCredential is properly configured
- Review app logs for connection errors

### Feature flags not updating
- Check cache expiration time (default 5 minutes)
- Restart application to force reload
- Verify UseAzureAppConfiguration middleware is added
- Check feature flag is enabled in App Configuration portal

### Secrets not resolving
- Verify secret names match configuration keys (use `--` for `:`)
- Check Key Vault access policies if using RBAC
- Ensure secrets exist in Key Vault
- Review application logs for Key Vault access errors

## Azure DevOps Pipeline (Incremental Deployment)
- Pipeline: `Learning Path/07-KeyVault/azure-pipelines.yml`
- Bicep: `Learning Path/07-KeyVault/infra.bicep`
- Required variables: `azureSubscription`, `resourceGroupName`, `location`, `mainWebAppName`, `functionAppName`, `storageAccountName`, `cosmosAccountName`, `cosmosDatabaseName`, `keyVaultName`, `appConfigName`, `azureAdTenantId`, `azureAdClientId`, `AzureAdClientSecret`
- Notes: The pipeline provisions Key Vault and App Configuration, assigns RBAC roles to the web/function app identities, and sets `KeyVault__VaultUri` and `AppConfiguration__Endpoint`.
