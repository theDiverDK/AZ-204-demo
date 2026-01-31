# Learning Path 11: Application Insights & Monitoring

## Overview
In this final learning path, you'll implement comprehensive monitoring and observability using Azure Application Insights, including custom telemetry, distributed tracing, dashboards, and alerting.

## What You'll Build
1. **Application Insights Integration**: Monitor web app and functions
2. **Custom Telemetry**: Track business events and metrics
3. **Distributed Tracing**: Track requests across services
4. **Live Metrics**: Real-time monitoring dashboard
5. **Alerts & Actions**: Automated alerting and notifications
6. **Workbooks**: Custom dashboards for insights

## Prerequisites
- Completed Learning Path 1-10
- Deployed Azure resources (Web App, Functions, Storage, Service Bus, etc.)

## Variables
Use base variables from `01-Init.md` (do not redefine):  
`location`, `resourceGroupName`, `random`, `appServicePlanName`, `webAppName`, `appRuntime`, `publishDir`, `zipPath`

Additional variables for this learning path:
```bash
logAnalyticsWorkspaceName="law-conferencehub-$random"
appInsightsName="appinsights-conferencehub-$random"
functionAppName="func-conferencehub-$random"
```

---

## Part 1: Create Application Insights Resource

### Step 1: Create Application Insights

```powershell
# Create Log Analytics Workspace (required for Application Insights)
az monitor log-analytics workspace create `
  --resource-group $resourceGroupNameName `
  --workspace-name $logAnalyticsWorkspaceName `
  --location $location

# Get workspace ID
$workspaceId = az monitor log-analytics workspace show `
  --resource-group $resourceGroupNameName `
  --workspace-name $logAnalyticsWorkspaceName `
  --query id `
  --output tsv

# Create Application Insights
az monitor app-insights component create `
  --app $appInsightsName `
  --location $location `
  --resource-group $resourceGroupNameName `
  --workspace $workspaceId

# Get instrumentation key and connection string
$instrumentationKey = az monitor app-insights component show `
  --app $appInsightsName `
  --resource-group $resourceGroupNameName `
  --query instrumentationKey `
  --output tsv

$connectionString = az monitor app-insights component show `
  --app $appInsightsName `
  --resource-group $resourceGroupNameName `
  --query connectionString `
  --output tsv

Write-Host "Instrumentation Key: $instrumentationKey"
Write-Host "Connection String: $connectionString"

# Store in Key Vault
az keyvault secret set `
  --vault-name $keyVaultName `
  --name "ApplicationInsights--ConnectionString" `
  --value $connectionString
```

### Step 2: Enable Application Insights for Web App

```powershell
# Enable Application Insights on Web App
az webapp config appsettings set `
  --name conferencehub-demo-az204reinke `
  --resource-group $resourceGroupNameName `
  --settings `
    APPLICATIONINSIGHTS_CONNECTION_STRING="$connectionString" `
    ApplicationInsightsAgent_EXTENSION_VERSION="~3"

# Enable always on (for continuous monitoring)
az webapp config set `
  --name conferencehub-demo-az204reinke `
  --resource-group $resourceGroupNameName `
  --always-on true
```

### Step 3: Enable Application Insights for Functions

```powershell
# Enable Application Insights for Function App
az functionapp config appsettings set `
  --name $functionAppName `
  --resource-group $resourceGroupNameName `
  --settings `
    APPLICATIONINSIGHTS_CONNECTION_STRING="$connectionString"
```

---

## Part 2: Add Application Insights to Web Application

### Step 1: Add NuGet Packages

```powershell
cd ConferenceHub
dotnet add package Microsoft.ApplicationInsights.AspNetCore
dotnet add package Microsoft.ApplicationInsights.WorkerService
```

### Step 2: Update Program.cs

Update `ConferenceHub/Program.cs`:
```csharp
using ConferenceHub.Services;
using ConferenceHub.Models;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Azure.Identity;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.FeatureManagement;
using Microsoft.ApplicationInsights.Extensibility;

var builder = WebApplication.CreateBuilder(args);

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = true;
    options.EnableQuickPulseMetricStream = true;
});

// Configure telemetry
builder.Services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();

// ... existing configuration (App Configuration, Key Vault, etc.) ...

// Add services to the container with authorization
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

// ... rest of configuration ...

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

### Step 3: Create Custom Telemetry Initializer

Create `ConferenceHub/Telemetry/CustomTelemetryInitializer.cs`:
```csharp
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
```

Register HttpContextAccessor in `Program.cs`:
```csharp
builder.Services.AddHttpContextAccessor();
```

---

## Part 3: Add Custom Telemetry Tracking

### Step 1: Create Telemetry Service

Create `ConferenceHub/Services/ITelemetryService.cs`:
```csharp
namespace ConferenceHub.Services
{
    public interface ITelemetryService
    {
        void TrackSessionView(int sessionId, string sessionTitle, string userId);
        void TrackRegistration(int sessionId, string sessionTitle, string userId, bool success);
        void TrackSlideDownload(int sessionId, string sessionTitle, string userId);
        void TrackFeedbackSubmission(int sessionId, int rating, string userId);
        void TrackSearchQuery(string query, int resultCount, string userId);
    }
}
```

Create `ConferenceHub/Services/TelemetryService.cs`:
```csharp
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace ConferenceHub.Services
{
    public class TelemetryService : ITelemetryService
    {
        private readonly TelemetryClient _telemetryClient;

        public TelemetryService(TelemetryClient telemetryClient)
        {
            _telemetryClient = telemetryClient;
        }

        public void TrackSessionView(int sessionId, string sessionTitle, string userId)
        {
            var properties = new Dictionary<string, string>
            {
                { "SessionId", sessionId.ToString() },
                { "SessionTitle", sessionTitle },
                { "UserId", userId }
            };

            var metrics = new Dictionary<string, double>
            {
                { "ViewCount", 1 }
            };

            _telemetryClient.TrackEvent("SessionViewed", properties, metrics);
        }

        public void TrackRegistration(int sessionId, string sessionTitle, string userId, bool success)
        {
            var properties = new Dictionary<string, string>
            {
                { "SessionId", sessionId.ToString() },
                { "SessionTitle", sessionTitle },
                { "UserId", userId },
                { "Success", success.ToString() }
            };

            var metrics = new Dictionary<string, double>
            {
                { "RegistrationCount", success ? 1 : 0 },
                { "FailedRegistrationCount", success ? 0 : 1 }
            };

            _telemetryClient.TrackEvent("RegistrationAttempt", properties, metrics);

            // Track metric for monitoring
            _telemetryClient.GetMetric("Registrations.Total").TrackValue(success ? 1 : 0);
            
            if (!success)
            {
                _telemetryClient.TrackTrace($"Registration failed for session {sessionId}", SeverityLevel.Warning);
            }
        }

        public void TrackSlideDownload(int sessionId, string sessionTitle, string userId)
        {
            var properties = new Dictionary<string, string>
            {
                { "SessionId", sessionId.ToString() },
                { "SessionTitle", sessionTitle },
                { "UserId", userId }
            };

            var metrics = new Dictionary<string, double>
            {
                { "DownloadCount", 1 }
            };

            _telemetryClient.TrackEvent("SlideDownloaded", properties, metrics);
        }

        public void TrackFeedbackSubmission(int sessionId, int rating, string userId)
        {
            var properties = new Dictionary<string, string>
            {
                { "SessionId", sessionId.ToString() },
                { "UserId", userId }
            };

            var metrics = new Dictionary<string, double>
            {
                { "Rating", rating },
                { "FeedbackCount", 1 }
            };

            _telemetryClient.TrackEvent("FeedbackSubmitted", properties, metrics);
            _telemetryClient.GetMetric("Session.AverageRating", "SessionId").TrackValue(rating, sessionId.ToString());
        }

        public void TrackSearchQuery(string query, int resultCount, string userId)
        {
            var properties = new Dictionary<string, string>
            {
                { "Query", query },
                { "UserId", userId },
                { "HasResults", (resultCount > 0).ToString() }
            };

            var metrics = new Dictionary<string, double>
            {
                { "ResultCount", resultCount }
            };

            _telemetryClient.TrackEvent("SearchPerformed", properties, metrics);
        }
    }
}
```

Register service in `Program.cs`:
```csharp
builder.Services.AddSingleton<ITelemetryService, TelemetryService>();
```

### Step 2: Update Controllers with Custom Telemetry

Update `Controllers/SessionsController.cs`:
```csharp
using Microsoft.ApplicationInsights;

private readonly ITelemetryService _telemetryService;
private readonly TelemetryClient _telemetryClient;

public SessionsController(
    IDataService dataService,
    IHttpClientFactory httpClientFactory,
    IAuditLogService auditLogService,
    IServiceBusService serviceBusService,
    ITelemetryService telemetryService,
    TelemetryClient telemetryClient,
    // ... other dependencies
{
    _telemetryService = telemetryService;
    _telemetryClient = telemetryClient;
    // ...
}

[AllowAnonymous]
public async Task<IActionResult> Index()
{
    using var operation = _telemetryClient.StartOperation<RequestTelemetry>("GetAllSessions");
    
    try
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var sessions = await _dataService.GetSessionsAsync();
        stopwatch.Stop();

        // Track custom metric
        _telemetryClient.GetMetric("SessionList.LoadTime").TrackValue(stopwatch.ElapsedMilliseconds);
        _telemetryClient.GetMetric("SessionList.Count").TrackValue(sessions.Count);

        return View(sessions);
    }
    catch (Exception ex)
    {
        operation.Telemetry.Success = false;
        _telemetryClient.TrackException(ex);
        throw;
    }
}

[AllowAnonymous]
public async Task<IActionResult> Details(int id)
{
    var session = await _dataService.GetSessionByIdAsync(id);
    if (session == null)
    {
        return NotFound();
    }

    // Track session view
    var userId = User.Identity?.Name ?? "Anonymous";
    _telemetryService.TrackSessionView(id, session.Title, userId);

    return View(session);
}

[HttpPost]
[Authorize]
public async Task<IActionResult> Register(int sessionId, string attendeeName, string attendeeEmail)
{
    using var operation = _telemetryClient.StartOperation<RequestTelemetry>("RegisterForSession");
    operation.Telemetry.Properties["SessionId"] = sessionId.ToString();
    
    var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? attendeeEmail;
    var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? attendeeName;

    var session = await _dataService.GetSessionByIdAsync(sessionId);
    if (session == null)
    {
        return NotFound();
    }

    bool success = false;
    
    try
    {
        if (session.CurrentRegistrations >= session.Capacity)
        {
            _telemetryService.TrackRegistration(sessionId, session.Title, userEmail, false);
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
        await _auditLogService.LogRegistrationAsync(sessionId, session.Title, userName, userEmail);

        // Send to Service Bus
        var registrationMessage = new RegistrationMessage
        {
            RegistrationId = registration.Id,
            SessionId = sessionId,
            SessionTitle = session.Title,
            AttendeeName = userName,
            AttendeeEmail = userEmail,
            SessionStartTime = session.StartTime,
            Room = session.Room
        };

        await _serviceBusService.SendRegistrationMessageAsync(registrationMessage);

        success = true;
        _telemetryService.TrackRegistration(sessionId, session.Title, userEmail, true);
        
        operation.Telemetry.Success = true;
        TempData["Success"] = "Successfully registered!";
        
        return RedirectToAction(nameof(Details), new { id = sessionId });
    }
    catch (Exception ex)
    {
        operation.Telemetry.Success = false;
        _telemetryClient.TrackException(ex);
        _telemetryService.TrackRegistration(sessionId, session.Title, userEmail, false);
        throw;
    }
}
```

Update `Controllers/FeedbackController.cs`:
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Submit(SessionFeedback feedback)
{
    try
    {
        feedback.AttendeeEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "unknown@email.com";
        feedback.AttendeeName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        feedback.SubmittedAt = DateTime.UtcNow;

        var session = await _dataService.GetSessionByIdAsync(feedback.SessionId);
        if (session == null)
        {
            return NotFound();
        }
        feedback.SessionTitle = session.Title;

        await _eventHubService.SendFeedbackAsync(feedback);

        // Track feedback in Application Insights
        _telemetryService.TrackFeedbackSubmission(feedback.SessionId, feedback.Rating, feedback.AttendeeEmail);

        TempData["Success"] = "Thank you for your feedback!";
        return RedirectToAction("Details", "Sessions", new { id = feedback.SessionId });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error submitting feedback");
        _telemetryClient.TrackException(ex);
        TempData["Error"] = "Error submitting feedback. Please try again.";
        return RedirectToAction("Submit", new { sessionId = feedback.SessionId });
    }
}
```

---

## Part 4: Add Application Insights to Azure Functions

### Step 1: Update Function Configuration

The Functions runtime automatically integrates with Application Insights. Enhance it with custom tracking.

Update `ConferenceHubFunctions/ProcessRegistrationQueue.cs`:
```csharp
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

public class ProcessRegistrationQueue
{
    private readonly ILogger<ProcessRegistrationQueue> _logger;
    private readonly TelemetryClient _telemetryClient;

    public ProcessRegistrationQueue(
        ILogger<ProcessRegistrationQueue> logger,
        TelemetryClient telemetryClient)
    {
        _logger = logger;
        _telemetryClient = telemetryClient;
    }

    [Function("ProcessRegistrationQueue")]
    public async Task Run(
        [ServiceBusTrigger("registration-queue", Connection = "ServiceBusConnectionString")] ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        using var operation = _telemetryClient.StartOperation<RequestTelemetry>("ProcessRegistration");
        operation.Telemetry.Properties["MessageId"] = message.MessageId;
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Processing registration message: {MessageId}", message.MessageId);

            var messageBody = Encoding.UTF8.GetString(message.Body);
            var registration = JsonSerializer.Deserialize<RegistrationMessage>(messageBody);

            if (registration != null)
            {
                operation.Telemetry.Properties["SessionId"] = registration.SessionId.ToString();
                operation.Telemetry.Properties["AttendeeEmail"] = registration.AttendeeEmail;
                
                await ProcessRegistrationAsync(registration);
                await messageActions.CompleteMessageAsync(message);

                stopwatch.Stop();
                _telemetryClient.GetMetric("RegistrationProcessing.Duration").TrackValue(stopwatch.ElapsedMilliseconds);
                _telemetryClient.GetMetric("RegistrationProcessing.Success").TrackValue(1);

                operation.Telemetry.Success = true;
                _logger.LogInformation("Registration processed successfully: {Email}", registration.AttendeeEmail);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            operation.Telemetry.Success = false;
            _telemetryClient.TrackException(ex);
            _telemetryClient.GetMetric("RegistrationProcessing.Failure").TrackValue(1);
            
            _logger.LogError(ex, "Error processing registration message");
            
            if (message.DeliveryCount >= 3)
            {
                await messageActions.DeadLetterMessageAsync(message, "MaxDeliveryCountExceeded", ex.Message);
            }
            else
            {
                await messageActions.AbandonMessageAsync(message);
            }
        }
    }

    // ... rest of the class ...
}
```

### Step 2: Configure Functions Startup

Update `ConferenceHubFunctions/Program.cs`:
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ConferenceHubFunctions.Authorization;
using Azure.Identity;
using Microsoft.ApplicationInsights.Extensibility;

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
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<JwtValidator>();
        
        // Configure Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

host.Run();
```

---

## Part 5: Create Custom Dashboards and Workbooks

### Step 1: Create KQL Queries for Monitoring

Common queries for Application Insights:

**1. Session Views Over Time**
```kusto
customEvents
| where name == "SessionViewed"
| summarize ViewCount = count() by bin(timestamp, 1h), SessionId = tostring(customDimensions.SessionId)
| order by timestamp desc
```

**2. Registration Success Rate**
```kusto
customEvents
| where name == "RegistrationAttempt"
| summarize 
    TotalAttempts = count(),
    SuccessCount = countif(customDimensions.Success == "True"),
    FailureCount = countif(customDimensions.Success == "False")
    by bin(timestamp, 1h)
| extend SuccessRate = (SuccessCount * 100.0) / TotalAttempts
| order by timestamp desc
```

**3. Average Session Ratings**
```kusto
customEvents
| where name == "FeedbackSubmitted"
| extend Rating = todouble(customMeasurements.Rating)
| summarize 
    AvgRating = avg(Rating),
    FeedbackCount = count()
    by SessionId = tostring(customDimensions.SessionId)
| order by AvgRating desc
```

**4. Application Performance**
```kusto
requests
| summarize 
    RequestCount = count(),
    AvgDuration = avg(duration),
    P95Duration = percentile(duration, 95),
    FailureCount = countif(success == false)
    by bin(timestamp, 5m), operation_Name
| order by timestamp desc
```

**5. Exception Analysis**
```kusto
exceptions
| summarize ExceptionCount = count() by type, outerMessage, operation_Name
| order by ExceptionCount desc
```

### Step 2: Create Dashboard

```powershell
# Create dashboard using Azure Portal
# Navigate to: Application Insights → Dashboards → New Dashboard
# Add the following tiles:
# 1. Failed requests
# 2. Server response time
# 3. Server requests
# 4. Availability
# 5. Custom metric: Session Views
# 6. Custom metric: Registration Success Rate
```

### Step 3: Create Workbook

Create workbook in Azure Portal:
1. Navigate to Application Insights → Workbooks → New
2. Add sections for:
   - **Overview**: Request counts, response times, failures
   - **User Activity**: Session views, registrations, feedback
   - **Performance**: Operation durations, dependencies
   - **Errors**: Exception analysis, failed requests
   - **Custom Metrics**: Business-specific KPIs

---

## Part 6: Configure Alerts

### Step 1: Create Alert for High Error Rate

```powershell
# Create action group for notifications
az monitor action-group create `
  --name ag-conferencehub-alerts `
  --resource-group $resourceGroupNameName `
  --short-name CHAlerts `
  --email-receiver admin soren@reinke.dk

# Create alert rule for high error rate
az monitor metrics alert create `
  --name alert-high-error-rate `
  --resource-group $resourceGroupNameName `
  --scopes "/subscriptions/YOUR_SUB_ID/resourceGroups/$resourceGroupNameName/providers/microsoft.insights/components/$appInsightsName" `
  --condition "count requests/failed > 10" `
  --window-size 5m `
  --evaluation-frequency 1m `
  --action ag-conferencehub-alerts `
  --description "Alert when error rate exceeds threshold"
```

### Step 2: Create Alert for Slow Response Time

```powershell
# Alert for response time > 3 seconds
az monitor metrics alert create `
  --name alert-slow-response `
  --resource-group $resourceGroupNameName `
  --scopes "/subscriptions/YOUR_SUB_ID/resourceGroups/$resourceGroupNameName/providers/microsoft.insights/components/$appInsightsName" `
  --condition "avg requests/duration > 3000" `
  --window-size 5m `
  --evaluation-frequency 1m `
  --action ag-conferencehub-alerts `
  --description "Alert when response time is too slow"
```

### Step 3: Create Custom Metric Alert

```powershell
# Alert for low registration success rate
az monitor scheduled-query create `
  --name alert-low-registration-rate `
  --resource-group $resourceGroupNameName `
  --scopes "/subscriptions/YOUR_SUB_ID/resourceGroups/$resourceGroupNameName/providers/microsoft.insights/components/$appInsightsName" `
  --condition "count > 5" `
  --condition-query "customEvents | where name == 'RegistrationAttempt' and customDimensions.Success == 'False' | summarize FailureCount = count()" `
  --window-duration PT5M `
  --evaluation-frequency PT1M `
  --action-groups ag-conferencehub-alerts `
  --description "Alert when registration failures exceed threshold"
```

---

## Part 7: Enable Availability Testing

### Step 1: Create URL Ping Test

```powershell
# Create availability test
az monitor app-insights web-test create `
  --resource-group $resourceGroupNameName `
  --name "Homepage Availability" `
  --location $location `
  --web-test-kind ping `
  --frequency 300 `
  --timeout 30 `
  --enabled true `
  --defined-web-test-name "Homepage Test" `
  --synthetic-monitor-id "homepage-test" `
  --tags "hidden-link:/subscriptions/YOUR_SUB_ID/resourceGroups/$resourceGroupNameName/providers/microsoft.insights/components/$appInsightsName=Resource" `
  --location-web-test-ids "us-il-ch1-azr" "us-va-ash-azr" "emea-nl-ams-azr" `
  --web-test-request-url "https://conferencehub-demo-az204reinke.azurewebsites.net"
```

### Step 2: Monitor Availability

View availability in Azure Portal:
- Navigate to Application Insights → Availability
- View test results and geographic distribution
- Set up alerts for availability failures

---

## Part 8: Deploy and Test

### Step 1: Deploy Updated Applications

```powershell
# Deploy Web App
cd ConferenceHub
dotnet publish -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./app.zip -Force
az webapp deployment source config-zip `
  --resource-group $resourceGroupNameName `
  --name conferencehub-demo-az204reinke `
  --src ./app.zip

# Deploy Functions
cd ../ConferenceHubFunctions
func azure functionapp publish $functionAppName
```

### Step 2: Generate Test Traffic

```powershell
# Generate traffic to create telemetry
for ($i = 1; $i -le 100; $i++) {
    Invoke-WebRequest -Uri "https://conferencehub-demo-az204reinke.azurewebsites.net" -UseBasicParsing
    Start-Sleep -Milliseconds 500
}
```

### Step 3: View Live Metrics

1. Navigate to Application Insights in Azure Portal
2. Click "Live Metrics" in left menu
3. Watch real-time requests, failures, and performance
4. Monitor custom metrics and events

### Step 4: Explore Application Map

1. Navigate to Application Insights → Application Map
2. View dependencies between components:
   - Web App
   - Azure Functions
   - Storage
   - Service Bus
   - Cosmos DB
3. Identify bottlenecks and failures

---

## Part 9: Query and Analyze Telemetry

### Useful Kusto Queries

**Top 10 Most Viewed Sessions**
```kusto
customEvents
| where name == "SessionViewed"
| summarize ViewCount = count() by SessionTitle = tostring(customDimensions.SessionTitle)
| top 10 by ViewCount desc
```

**Registration Funnel Analysis**
```kusto
union
    (customEvents | where name == "SessionViewed" | extend Stage = "View", SessionId = tostring(customDimensions.SessionId)),
    (customEvents | where name == "RegistrationAttempt" | extend Stage = "Register", SessionId = tostring(customDimensions.SessionId))
| summarize Count = count() by Stage, SessionId
| evaluate pivot(Stage)
```

**User Journey Analysis**
```kusto
customEvents
| where user_AuthenticatedId != ""
| order by timestamp asc
| project timestamp, user_AuthenticatedId, name, customDimensions
| take 100
```

**Performance by Operation**
```kusto
requests
| summarize 
    RequestCount = count(),
    AvgDuration = avg(duration),
    P50 = percentile(duration, 50),
    P95 = percentile(duration, 95),
    P99 = percentile(duration, 99)
    by operation_Name
| order by AvgDuration desc
```

---

## Summary

You've successfully completed all 11 learning paths and built a comprehensive ConferenceHub application with:

### ✅ Learning Path 1: Core Web Application
- ASP.NET Core MVC with sessions and registrations
- Organizer dashboard

### ✅ Learning Path 2: Azure Functions
- HTTP and Timer triggers
- Email confirmations

### ✅ Learning Path 3: Azure Storage
- Blob Storage for slides
- Table Storage for audit logs

### ✅ Learning Path 4: Cosmos DB
- NoSQL data persistence
- Advanced querying

### ✅ Learning Path 5: Docker & Containers
- Containerization with Docker
- Azure Container Apps deployment

### ✅ Learning Path 6: Authentication
- Microsoft Entra ID integration
- Role-based authorization

### ✅ Learning Path 7: Key Vault & App Configuration
- Secure secret management
- Feature flags

### ✅ Learning Path 8: API Management
- API gateway with APIM
- Rate limiting and policies

### ✅ Learning Path 9: Event Grid & Event Hub
- Event-driven architecture
- Real-time feedback streaming

### ✅ Learning Path 10: Service Bus & Queues
- Reliable messaging
- Dead letter queue handling

### ✅ Learning Path 11: Application Insights
- Comprehensive monitoring
- Custom telemetry and dashboards
- Alerts and availability testing

---

## Next Steps & Advanced Topics

Consider exploring:
- **Azure DevOps**: CI/CD pipelines with automated testing
- **Azure Cache for Redis**: Distributed caching
- **Azure SignalR Service**: Real-time web functionality
- **Azure Cognitive Services**: AI-powered features
- **Azure Load Balancer**: High availability and scaling
- **Terraform/Bicep**: Infrastructure as Code

---

## Troubleshooting

### Telemetry not appearing
- Verify connection string is correct
- Check sampling settings (may be filtering data)
- Wait 2-5 minutes for data to appear
- Review application logs for SDK errors

### Missing custom events
- Verify TelemetryClient.TrackEvent() is called
- Check telemetry initializer is registered
- Ensure application is flushing telemetry on shutdown

### Alerts not firing
- Verify alert rules are enabled
- Check action group is properly configured
- Review alert condition thresholds
- Check metric data is being collected

### Dashboard not updating
- Verify time range in dashboard
- Check data source connections
- Refresh browser cache
- Verify KQL queries are valid

---

## Congratulations! 🎉

You've built a production-ready, cloud-native application using the full spectrum of Azure services covered in the AZ-204 certification exam. This application demonstrates:

- ✅ Compute solutions (App Service, Functions, Containers)
- ✅ Storage solutions (Blob, Queue, Table, Cosmos DB)
- ✅ Authentication and authorization (Entra ID)
- ✅ Secure solutions (Key Vault, Managed Identity)
- ✅ API Management and integration
- ✅ Event-based solutions (Event Grid, Event Hub, Service Bus)
- ✅ Monitoring and optimization (Application Insights)

**You're now ready for the AZ-204 exam and real-world Azure development!**

## Azure DevOps Pipeline (Incremental Deployment)
- Pipeline: `Learning Path/11-AppInsights/azure-pipelines.yml`
- Bicep: `Learning Path/11-AppInsights/infra.bicep`
- Required variables: `azureSubscription`, `resourceGroupName`, `location`, `mainWebAppName`, `functionAppName`, `storageAccountName`, `cosmosAccountName`, `cosmosDatabaseName`, `keyVaultUri`, `appConfigEndpoint`, `azureAdTenantId`, `azureAdClientId`, `AzureAdClientSecret`, `apiManagementGatewayUrl`, `ApiManagementSubscriptionKey`, `eventHubNamespaceName`, `eventHubName`, `serviceBusNamespaceName`
- Notes: The pipeline provisions Application Insights and sets `APPLICATIONINSIGHTS_CONNECTION_STRING` for the web and function apps.
