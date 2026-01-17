# Learning Path 2: Azure Functions

## Overview
In this learning path, you'll enhance the ConferenceHub application by adding serverless Azure Functions to handle background tasks and offload certain actions from the main web application.

## What You'll Build
1. **HTTP-triggered Function**: Send confirmation emails when users register for sessions
2. **Timer-triggered Function**: Automatically close registration for sessions starting within 24 hours
3. **Web App Integration**: Update the web app to call the Azure Function instead of handling email logic directly

## Prerequisites
- Completed Learning Path 1 (Web App deployed to Azure App Service)
- Azure CLI installed and logged in
- .NET 10 SDK installed
- VS Code with Azure Functions extension

---

## Part 1: Create Azure Functions Project

### Step 1: Create Functions App Locally

1. **Create a new folder for the Functions project**:
```powershell
cd "c:\Users\Admin\AZ-204 demo"
mkdir ConferenceHub.Functions
cd ConferenceHub.Functions
```

2. **Initialize the Functions project**:
```powershell
func init --worker-runtime dotnet-isolated --target-framework net10.0
```

3. **Open the project in VS Code**:
```powershell
code .
```

### Step 2: Create HTTP-Triggered Function (Send Confirmation)

1. **Create the HTTP function**:
```powershell
func new --name SendConfirmation --template "HTTP trigger" --authlevel "function"
```

2. **Create a Models folder and add the Registration DTO**:

Create `Models/RegistrationRequest.cs`:
```csharp
namespace ConferenceHub.Functions.Models
{
    public class RegistrationRequest
    {
        public int SessionId { get; set; }
        public string SessionTitle { get; set; } = string.Empty;
        public string AttendeeName { get; set; } = string.Empty;
        public string AttendeeEmail { get; set; } = string.Empty;
        public DateTime SessionStartTime { get; set; }
        public string Room { get; set; } = string.Empty;
    }
}
```

3. **Update the SendConfirmation function**:

Replace the contents of `SendConfirmation.cs`:
```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ConferenceHub.Functions.Models;

namespace ConferenceHub.Functions
{
    public class SendConfirmation
    {
        private readonly ILogger<SendConfirmation> _logger;

        public SendConfirmation(ILogger<SendConfirmation> logger)
        {
            _logger = logger;
        }

        [Function("SendConfirmation")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("SendConfirmation function triggered");

            try
            {
                // Read the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var registration = JsonSerializer.Deserialize<RegistrationRequest>(requestBody, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (registration == null)
                {
                    return new BadRequestObjectResult("Invalid registration data");
                }

                // Simulate sending email (will be replaced with actual email service later)
                _logger.LogInformation(
                    "Sending confirmation email to {Email} for session '{SessionTitle}'",
                    registration.AttendeeEmail,
                    registration.SessionTitle);

                _logger.LogInformation(
                    "Email Details - Attendee: {Name}, Session: {Title}, Time: {Time}, Room: {Room}",
                    registration.AttendeeName,
                    registration.SessionTitle,
                    registration.SessionStartTime,
                    registration.Room);

                // Simulate email content
                var emailContent = new
                {
                    To = registration.AttendeeEmail,
                    Subject = $"Registration Confirmed: {registration.SessionTitle}",
                    Body = $@"
                        Dear {registration.AttendeeName},
                        
                        Your registration for the following session has been confirmed:
                        
                        Session: {registration.SessionTitle}
                        Date & Time: {registration.SessionStartTime:MMMM dd, yyyy 'at' h:mm tt}
                        Room: {registration.Room}
                        
                        We look forward to seeing you at the conference!
                        
                        Best regards,
                        ConferenceHub Team
                    "
                };

                _logger.LogInformation("Email content: {@EmailContent}", emailContent);

                return new OkObjectResult(new
                {
                    success = true,
                    message = "Confirmation email sent successfully",
                    recipient = registration.AttendeeEmail
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing confirmation email");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
```

### Step 3: Create Timer-Triggered Function (Close Registration)

1. **Create the Timer function**:
```powershell
func new --name CloseRegistrations --template "Timer trigger"
```

2. **Create Session model for the function**:

Create `Models/Session.cs`:
```csharp
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
```

3. **Update the CloseRegistrations function**:

Replace the contents of `CloseRegistrations.cs`:
```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ConferenceHub.Functions
{
    public class CloseRegistrations
    {
        private readonly ILogger _logger;

        public CloseRegistrations(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CloseRegistrations>();
        }

        // Runs every night at 2 AM (CRON: 0 0 2 * * *)
        // For testing, use "0 */5 * * * *" to run every 5 minutes
        [Function("CloseRegistrations")]
        public async Task Run([TimerTrigger("0 0 2 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation("CloseRegistrations function triggered at: {Time}", DateTime.Now);

            try
            {
                // Calculate the cutoff time (24 hours from now)
                var cutoffTime = DateTime.Now.AddHours(24);
                _logger.LogInformation("Checking for sessions starting before: {CutoffTime}", cutoffTime);

                // TODO: In a future learning path, this will:
                // 1. Connect to Azure Table Storage or Cosmos DB
                // 2. Query for sessions where StartTime < cutoffTime AND RegistrationClosed = false
                // 3. Update those sessions to set RegistrationClosed = true

                // For now, simulate the logic
                _logger.LogInformation("Simulating closing registrations for upcoming sessions");
                
                // Simulate finding sessions
                var sessionsToClose = new[]
                {
                    new { Id = 1, Title = "Sample Session 1", StartTime = DateTime.Now.AddHours(20) },
                    new { Id = 2, Title = "Sample Session 2", StartTime = DateTime.Now.AddHours(22) }
                };

                foreach (var session in sessionsToClose)
                {
                    _logger.LogInformation(
                        "Closing registration for session {SessionId}: {Title} (starts at {StartTime})",
                        session.Id,
                        session.Title,
                        session.StartTime);
                }

                _logger.LogInformation("Closed registration for {Count} sessions", sessionsToClose.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing registrations");
            }

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation("Next timer schedule at: {NextRun}", myTimer.ScheduleStatus.Next);
            }
        }
    }
}
```

### Step 4: Test Locally

1. **Start the Functions runtime**:
```powershell
func start
```

2. **Test the HTTP function** (in a new terminal):
```powershell
$body = @{
    sessionId = 1
    sessionTitle = "Building Cloud-Native Applications"
    attendeeName = "Test User"
    attendeeEmail = "test@example.com"
    sessionStartTime = "2025-12-01T09:00:00"
    room = "Main Hall A"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:7071/api/SendConfirmation" -Method Post -Body $body -ContentType "application/json"
```

3. **Observe the timer function** - it will run based on the CRON schedule (or manually trigger it in the Azure portal later)

---

## Part 2: Deploy Azure Functions to Azure

### Step 1: Create Azure Resources

1. **Create a Storage Account** (required for Functions):
```powershell
az storage account create `
  --name stconferencehubfunc `
  --resource-group rg-conferencehub `
  --location eastus `
  --sku Standard_LRS
```

2. **Create a Function App**:
```powershell
az functionapp create `
  --name func-conferencehub-[yourname] `
  --resource-group rg-conferencehub `
  --storage-account stconferencehubfunc `
  --consumption-plan-location eastus `
  --runtime dotnet-isolated `
  --runtime-version 8 `
  --functions-version 4 `
  --os-type Windows
```
*Replace [yourname] with your unique identifier*

### Step 2: Deploy the Functions

1. **Publish the Functions app**:
```powershell
func azure functionapp publish func-conferencehub-[yourname]
```

2. **Get the function URL and key**:
```powershell
# Get the function key
az functionapp function keys list `
  --name func-conferencehub-[yourname] `
  --resource-group rg-conferencehub `
  --function-name SendConfirmation

# The function URL will be:
# https://func-conferencehub-[yourname].azurewebsites.net/api/SendConfirmation?code=[function-key]
```

---

## Part 3: Update Web App to Call Azure Function

### Step 1: Add Configuration

1. **Add Function URL to appsettings.json**:

Open `ConferenceHub/appsettings.json` and add:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AzureFunctions": {
    "SendConfirmationUrl": "https://func-conferencehub-[yourname].azurewebsites.net/api/SendConfirmation",
    "FunctionKey": "[your-function-key]"
  }
}
```

2. **Create appsettings.Development.json** for local testing:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AzureFunctions": {
    "SendConfirmationUrl": "http://localhost:7071/api/SendConfirmation",
    "FunctionKey": ""
  }
}
```

### Step 2: Create Function Configuration Model

Create `ConferenceHub/Models/AzureFunctionsConfig.cs`:
```csharp
namespace ConferenceHub.Models
{
    public class AzureFunctionsConfig
    {
        public string SendConfirmationUrl { get; set; } = string.Empty;
        public string FunctionKey { get; set; } = string.Empty;
    }
}
```

### Step 3: Register HttpClient and Configuration

Update `ConferenceHub/Program.cs`:
```csharp
using ConferenceHub.Services;
using ConferenceHub.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IDataService, DataService>();

// Configure Azure Functions settings
builder.Services.Configure<AzureFunctionsConfig>(
    builder.Configuration.GetSection("AzureFunctions"));

// Add HttpClient for calling Azure Functions
builder.Services.AddHttpClient();

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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

### Step 4: Update SessionsController

Update `ConferenceHub/Controllers/SessionsController.cs`:
```csharp
using ConferenceHub.Models;
using ConferenceHub.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace ConferenceHub.Controllers
{
    public class SessionsController : Controller
    {
        private readonly IDataService _dataService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AzureFunctionsConfig _functionsConfig;
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(
            IDataService dataService,
            IHttpClientFactory httpClientFactory,
            IOptions<AzureFunctionsConfig> functionsConfig,
            ILogger<SessionsController> logger)
        {
            _dataService = dataService;
            _httpClientFactory = httpClientFactory;
            _functionsConfig = functionsConfig.Value;
            _logger = logger;
        }

        // GET: Sessions
        public async Task<IActionResult> Index()
        {
            var sessions = await _dataService.GetSessionsAsync();
            return View(sessions);
        }

        // GET: Sessions/Details/5
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
        public async Task<IActionResult> Register(int sessionId, string attendeeName, string attendeeEmail)
        {
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
                AttendeeName = attendeeName,
                AttendeeEmail = attendeeEmail
            };

            await _dataService.AddRegistrationAsync(registration);

            // Call Azure Function to send confirmation email
            await SendConfirmationEmailAsync(session, attendeeName, attendeeEmail);

            TempData["Success"] = "Successfully registered for the session!";
            
            return RedirectToAction(nameof(Details), new { id = sessionId });
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

                // Build the URL with function key if provided
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
                // Don't fail the registration if email fails
                _logger.LogError(ex, "Error calling SendConfirmation function");
            }
        }
    }
}
```

---

## Part 4: Configure App Service Settings

### Update Azure Web App Configuration

Add the Function URL to the Web App's application settings:
```powershell
az webapp config appsettings set `
  --name conferencehub-demo-[yourname] `
  --resource-group rg-conferencehub `
  --settings AzureFunctions__SendConfirmationUrl="https://func-conferencehub-[yourname].azurewebsites.net/api/SendConfirmation" `
             AzureFunctions__FunctionKey="[your-function-key]"
```

---

## Part 5: Test End-to-End

### Step 1: Test Locally

1. **Start the Functions app** (in one terminal):
```powershell
cd ConferenceHub.Functions
func start
```

2. **Start the Web app** (in another terminal):
```powershell
cd ConferenceHub
dotnet run
```

3. **Test registration**:
   - Navigate to http://localhost:5000/sessions
   - Register for a session
   - Check the Functions terminal for confirmation email logs

### Step 2: Test in Azure

1. **Deploy updated Web App**:
```powershell
cd ConferenceHub
dotnet publish -c Release -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./app.zip -Force
az webapp deployment source config-zip `
  --resource-group rg-conferencehub `
  --name conferencehub-demo-[yourname] `
  --src ./app.zip
```

2. **Test the live application**:
   - Navigate to your Azure Web App URL
   - Register for a session
   - Check Application Insights or Function logs

3. **View Function logs**:
```powershell
az functionapp log tail `
  --name func-conferencehub-[yourname] `
  --resource-group rg-conferencehub
```

---

## Part 6: Monitor and Troubleshoot

### View Function Execution History

```powershell
# View recent function executions
az functionapp function show `
  --name func-conferencehub-[yourname] `
  --resource-group rg-conferencehub `
  --function-name SendConfirmation
```

### Check Application Insights

In the Azure Portal:
1. Navigate to your Function App
2. Go to "Application Insights"
3. View "Live Metrics" to see real-time function executions
4. View "Logs" to query execution history

---

## Summary

You've successfully:
- ✅ Created an Azure Functions project with HTTP and Timer triggers
- ✅ Deployed Functions to Azure
- ✅ Updated the web app to call the Azure Function
- ✅ Configured the timer to run nightly for closing registrations
- ✅ Tested the integration end-to-end

## Next Steps

In **Learning Path 3**, you'll:
- Replace in-memory registration storage with **Azure Cosmos DB**
- Update the Timer function to actually query and update the database
- Implement actual email sending using **Azure Communication Services** or **SendGrid**

---

## Troubleshooting

### Function not triggering
- Check CRON expression syntax
- Verify Function App is running in Azure Portal
- Check Application Insights for errors

### HTTP function returns 401
- Verify the function key is correct
- Check authorization level in function code

### Web app can't reach function
- Verify the Function URL in app settings
- Check network/firewall rules
- Test the function URL directly with Postman or curl

### Timer running too frequently
- Verify CRON expression: `0 0 2 * * *` = 2 AM daily
- For testing, use `0 */5 * * * *` = every 5 minutes

## Azure DevOps Pipeline (Incremental Deployment)
- Pipeline: `Learning Path/02-Functions/azure-pipelines.yml`
- Bicep: `Learning Path/02-Functions/infra.bicep`
- Required variables: `azureSubscription`, `resourceGroupName`, `location`, `functionStorageAccountName`, `functionAppName`, `functionPlanName`, `mainWebAppName`
- Notes: The pipeline provisions the Function App and updates the web app settings with `AzureFunctions__SendConfirmationUrl` and `AzureFunctions__FunctionKey`.
