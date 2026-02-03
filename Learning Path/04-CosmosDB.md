# Learning Path 4: Azure Cosmos DB

## Overview
In this learning path, you'll migrate the ConferenceHub application from JSON file storage to Azure Cosmos DB, a globally distributed, multi-model database service that provides comprehensive SLAs for throughput, latency, availability, and consistency.

## What You'll Build
1. **Cosmos DB Account**: Create and configure Azure Cosmos DB
2. **Data Migration**: Move sessions and registrations from JSON/memory to Cosmos DB
3. **Advanced Queries**: Implement filtering, sorting, and pagination
4. **Update Functions**: Modify Azure Functions to work with Cosmos DB

## Prerequisites
- Completed Learning Paths 1-3
- Azure Cosmos DB Account (or create new)
- Microsoft.Azure.Cosmos NuGet package

## Variables
Use base variables from `01-Init.md` (do not redefine):  
`location`, `resourceGroupName`, `random`, `appServicePlanName`, `webAppName`, `appRuntime`, `publishDir`, `zipPath`

Additional variables for this learning path:
```bash
cosmosAccountName="cosmos-conferencehub-$random"
cosmosDatabaseName="ConferenceHubDB"
functionAppName="func-conferencehub-$random"
```

---

## Part 1: Create Azure Cosmos DB Resources

### Step 1: Create Cosmos DB Account

```powershell
# Create Cosmos DB account (SQL API)
az cosmosdb create `
  --name $cosmosAccountName `
  --resource-group $resourceGroupName `
  --kind GlobalDocumentDB `
  --locations regionName=$location failoverPriority=0 isZoneRedundant=False `
  --default-consistency-level Session `
  --enable-automatic-failover false

# Get connection string
az cosmosdb keys list `
  --name $cosmosAccountName `
  --resource-group $resourceGroupName `
  --type connection-strings `
  --query "connectionStrings[0].connectionString" `
  --output tsv
```
**Bash**
```bash
az cosmosdb create \
  --name "$cosmosAccountName" \
  --resource-group "$resourceGroupName" \
  --kind GlobalDocumentDB \
  --locations regionName="$location" failoverPriority=0 isZoneRedundant=False \
  --default-consistency-level Session \
  --enable-automatic-failover false

az cosmosdb keys list \
  --name "$cosmosAccountName" \
  --resource-group "$resourceGroupName" \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" \
  --output tsv
```

### Step 2: Create Database and Containers

```powershell
# Create database
az cosmosdb sql database create `
  --account-name $cosmosAccountName `
  --resource-group $resourceGroupName `
  --name $cosmosDatabaseName

# Create Sessions container (partition by /conferenceId or /track)
az cosmosdb sql container create `
  --account-name $cosmosAccountName `
  --resource-group $resourceGroupName `
  --database-name $cosmosDatabaseName `
  --name Sessions `
  --partition-key-path "/conferenceId" `
  --throughput 400

# Create Registrations container (partition by /sessionId)
az cosmosdb sql container create `
  --account-name $cosmosAccountName `
  --resource-group $resourceGroupName `
  --database-name $cosmosDatabaseName `
  --name Registrations `
  --partition-key-path "/sessionId" `
  --throughput 400
```
**Bash**
```bash
az cosmosdb sql database create \
  --account-name "$cosmosAccountName" \
  --resource-group "$resourceGroupName" \
  --name "$cosmosDatabaseName"

az cosmosdb sql container create \
  --account-name "$cosmosAccountName" \
  --resource-group "$resourceGroupName" \
  --database-name "$cosmosDatabaseName" \
  --name Sessions \
  --partition-key-path "/conferenceId" \
  --throughput 400

az cosmosdb sql container create \
  --account-name "$cosmosAccountName" \
  --resource-group "$resourceGroupName" \
  --database-name "$cosmosDatabaseName" \
  --name Registrations \
  --partition-key-path "/sessionId" \
  --throughput 400
```

---

## Part 2: Update Data Models

### Step 1: Add NuGet Package

```powershell
cd ConferenceHub
dotnet add package Microsoft.Azure.Cosmos
dotnet add package Newtonsoft.Json 
```
**Bash**
```bash
cd ConferenceHub
dotnet add package Microsoft.Azure.Cosmos
dotnet add package Newtonsoft.Json 

cd ../ConferenceHub.Functions
dotnet add package Microsoft.Azure.Cosmos
dotnet add package Newtonsoft.Json 
```

### Step 2: Update Session Model

cp ../../Learning\ Path/04-CosmosDB/ConferenceHub/Models/* .

Update `Models/Session.cs`:
```csharp
using Newtonsoft.Json;

namespace ConferenceHub.Models
{
    public class Session
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [JsonProperty("conferenceId")]
        public string ConferenceId { get; set; } = "az204-2025"; // Partition key
        
        [JsonProperty("sessionNumber")]
        public int SessionNumber { get; set; }
        
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;
        
        [JsonProperty("speaker")]
        public string Speaker { get; set; } = string.Empty;
        
        [JsonProperty("startTime")]
        public DateTime StartTime { get; set; }
        
        [JsonProperty("endTime")]
        public DateTime EndTime { get; set; }
        
        [JsonProperty("room")]
        public string Room { get; set; } = string.Empty;
        
        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonProperty("capacity")]
        public int Capacity { get; set; }
        
        [JsonProperty("currentRegistrations")]
        public int CurrentRegistrations { get; set; }
        
        [JsonProperty("slideUrl")]
        public string? SlideUrl { get; set; }
        
        [JsonProperty("slideUploadedAt")]
        public DateTime? SlideUploadedAt { get; set; }
        
        [JsonProperty("track")]
        public string Track { get; set; } = "General"; // e.g., "Cloud", "DevOps", "AI"
        
        [JsonProperty("level")]
        public string Level { get; set; } = "Intermediate"; // Beginner, Intermediate, Advanced
        
        [JsonProperty("registrationClosed")]
        public bool RegistrationClosed { get; set; } = false;
        
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [JsonProperty("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

### Step 3: Update Registration Model

Update `Models/Registration.cs`:
```csharp
using Newtonsoft.Json;

namespace ConferenceHub.Models
{
    public class Registration
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [JsonProperty("sessionId")]
        public string SessionId { get; set; } = string.Empty; // Partition key
        
        [JsonProperty("sessionTitle")]
        public string SessionTitle { get; set; } = string.Empty;
        
        [JsonProperty("attendeeName")]
        public string AttendeeName { get; set; } = string.Empty;
        
        [JsonProperty("attendeeEmail")]
        public string AttendeeEmail { get; set; } = string.Empty;
        
        [JsonProperty("registeredAt")]
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
        
        [JsonProperty("status")]
        public string Status { get; set; } = "Confirmed"; // Confirmed, Cancelled, Waitlist
        
        [JsonProperty("userId")]
        public string? UserId { get; set; } // For future auth integration
    }
}
```

---

## Part 3: Create Cosmos DB Service
cp ../../Learning\ Path/04-CosmosDB/ConferenceHub/Services/* .
### Step 1: Create Interface

Create `Services/ICosmosDbService.cs`:
```csharp
using ConferenceHub.Models;

namespace ConferenceHub.Services
{
    public interface ICosmosDbService
    {
        // Sessions
        Task<IEnumerable<Session>> GetSessionsAsync();
        Task<IEnumerable<Session>> GetSessionsByFilterAsync(string? track = null, string? level = null);
        Task<Session?> GetSessionByIdAsync(string id);
        Task<Session> AddSessionAsync(Session session);
        Task<Session> UpdateSessionAsync(Session session);
        Task DeleteSessionAsync(string id);
        
        // Registrations
        Task<IEnumerable<Registration>> GetRegistrationsAsync();
        Task<IEnumerable<Registration>> GetSessionRegistrationsAsync(string sessionId);
        Task<Registration?> GetRegistrationByIdAsync(string id);
        Task<Registration> AddRegistrationAsync(Registration registration);
        Task<Registration> UpdateRegistrationAsync(Registration registration);
        Task DeleteRegistrationAsync(string id, string sessionId);
        Task<int> GetRegistrationCountBySessionAsync(string sessionId);
    }
}
```

### Step 2: Implement Cosmos DB Service

Create `Services/CosmosDbService.cs`:
```csharp
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using ConferenceHub.Models;

namespace ConferenceHub.Services
{
    public class CosmosDbService : ICosmosDbService
    {
        private readonly Container _sessionsContainer;
        private readonly Container _registrationsContainer;
        private readonly ILogger<CosmosDbService> _logger;

        public CosmosDbService(
            CosmosClient cosmosClient,
            string databaseName,
            ILogger<CosmosDbService> logger)
        {
            _sessionsContainer = cosmosClient.GetContainer(databaseName, "Sessions");
            _registrationsContainer = cosmosClient.GetContainer(databaseName, "Registrations");
            _logger = logger;
        }

        #region Sessions

        public async Task<IEnumerable<Session>> GetSessionsAsync()
        {
            try
            {
                var query = _sessionsContainer.GetItemLinqQueryable<Session>()
                    .OrderBy(s => s.StartTime);

                var iterator = query.ToFeedIterator();
                var results = new List<Session>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sessions from Cosmos DB");
                throw;
            }
        }

        public async Task<IEnumerable<Session>> GetSessionsByFilterAsync(string? track = null, string? level = null)
        {
            try
            {
                var query = _sessionsContainer.GetItemLinqQueryable<Session>();

                if (!string.IsNullOrEmpty(track))
                {
                    query = query.Where(s => s.Track == track);
                }

                if (!string.IsNullOrEmpty(level))
                {
                    query = query.Where(s => s.Level == level);
                }

                query = query.OrderBy(s => s.StartTime);

                var iterator = query.ToFeedIterator();
                var results = new List<Session>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering sessions in Cosmos DB");
                throw;
            }
        }

        public async Task<Session?> GetSessionByIdAsync(string id)
        {
            try
            {
                var response = await _sessionsContainer.ReadItemAsync<Session>(
                    id,
                    new PartitionKey("az204-2025"));
                
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving session {SessionId}", id);
                throw;
            }
        }

        public async Task<Session> AddSessionAsync(Session session)
        {
            try
            {
                session.Id = Guid.NewGuid().ToString();
                session.ConferenceId = "az204-2025";
                session.CreatedAt = DateTime.UtcNow;
                session.UpdatedAt = DateTime.UtcNow;

                var response = await _sessionsContainer.CreateItemAsync(
                    session,
                    new PartitionKey(session.ConferenceId));

                _logger.LogInformation("Created session {SessionId}", session.Id);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating session in Cosmos DB");
                throw;
            }
        }

        public async Task<Session> UpdateSessionAsync(Session session)
        {
            try
            {
                session.UpdatedAt = DateTime.UtcNow;

                var response = await _sessionsContainer.ReplaceItemAsync(
                    session,
                    session.Id,
                    new PartitionKey(session.ConferenceId));

                _logger.LogInformation("Updated session {SessionId}", session.Id);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating session {SessionId}", session.Id);
                throw;
            }
        }

        public async Task DeleteSessionAsync(string id)
        {
            try
            {
                await _sessionsContainer.DeleteItemAsync<Session>(
                    id,
                    new PartitionKey("az204-2025"));

                _logger.LogInformation("Deleted session {SessionId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting session {SessionId}", id);
                throw;
            }
        }

        #endregion

        #region Registrations

        public async Task<IEnumerable<Registration>> GetRegistrationsAsync()
        {
            try
            {
                var query = _registrationsContainer.GetItemLinqQueryable<Registration>()
                    .OrderByDescending(r => r.RegisteredAt);

                var iterator = query.ToFeedIterator();
                var results = new List<Registration>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registrations from Cosmos DB");
                throw;
            }
        }

        public async Task<IEnumerable<Registration>> GetSessionRegistrationsAsync(string sessionId)
        {
            try
            {
                var query = _registrationsContainer.GetItemLinqQueryable<Registration>()
                    .Where(r => r.SessionId == sessionId)
                    .OrderByDescending(r => r.RegisteredAt);

                var iterator = query.ToFeedIterator();
                var results = new List<Registration>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registrations for session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<Registration?> GetRegistrationByIdAsync(string id)
        {
            try
            {
                // Need to query since we don't have the partition key
                var query = _registrationsContainer.GetItemLinqQueryable<Registration>()
                    .Where(r => r.Id == id);

                var iterator = query.ToFeedIterator();
                var results = await iterator.ReadNextAsync();

                return results.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registration {RegistrationId}", id);
                throw;
            }
        }

        public async Task<Registration> AddRegistrationAsync(Registration registration)
        {
            try
            {
                registration.Id = Guid.NewGuid().ToString();
                registration.RegisteredAt = DateTime.UtcNow;
                registration.Status = "Confirmed";

                var response = await _registrationsContainer.CreateItemAsync(
                    registration,
                    new PartitionKey(registration.SessionId));

                _logger.LogInformation("Created registration {RegistrationId} for session {SessionId}",
                    registration.Id, registration.SessionId);

                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating registration in Cosmos DB");
                throw;
            }
        }

        public async Task<Registration> UpdateRegistrationAsync(Registration registration)
        {
            try
            {
                var response = await _registrationsContainer.ReplaceItemAsync(
                    registration,
                    registration.Id,
                    new PartitionKey(registration.SessionId));

                _logger.LogInformation("Updated registration {RegistrationId}", registration.Id);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating registration {RegistrationId}", registration.Id);
                throw;
            }
        }

        public async Task DeleteRegistrationAsync(string id, string sessionId)
        {
            try
            {
                await _registrationsContainer.DeleteItemAsync<Registration>(
                    id,
                    new PartitionKey(sessionId));

                _logger.LogInformation("Deleted registration {RegistrationId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting registration {RegistrationId}", id);
                throw;
            }
        }

        public async Task<int> GetRegistrationCountBySessionAsync(string sessionId)
        {
            try
            {
                var query = _registrationsContainer.GetItemLinqQueryable<Registration>()
                    .Where(r => r.SessionId == sessionId && r.Status == "Confirmed")
                    .Count();

                var iterator = query.ToFeedIterator();
                var response = await iterator.ReadNextAsync();

                return response.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error counting registrations for session {SessionId}", sessionId);
                return 0;
            }
        }

        #endregion
    }
}
```

---

## Part 4: Update Configuration and DI

### Step 1: Update appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "CosmosDb": {
    "ConnectionString": "YOUR_COSMOS_CONNECTION_STRING",
    "DatabaseName": "ConferenceHubDB"
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

### Step 2: Update Program.cs

```csharp
using ConferenceHub.Services;
using ConferenceHub.Models;
using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure Cosmos DB
var cosmosConnectionString = builder.Configuration["CosmosDb:ConnectionString"];
var cosmosDatabaseName = builder.Configuration["CosmosDb:DatabaseName"];
builder.Services.AddSingleton(sp =>
{
    var cosmosClient = new CosmosClient(cosmosConnectionString);
    return cosmosClient;
});
builder.Services.AddSingleton<ICosmosDbService>(sp =>
{
    var cosmosClient = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<CosmosDbService>>();
    return new CosmosDbService(cosmosClient, cosmosDatabaseName!, logger);
});

// Keep the old DataService for backward compatibility during migration
// Remove this after full migration
// builder.Services.AddSingleton<IDataService, DataService>();

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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

---

## Part 5: Update Controllers

### Update SessionsController

Update `Controllers/SessionsController.cs`:
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
        private readonly ICosmosDbService _cosmosDbService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuditLogService _auditLogService;
        private readonly AzureFunctionsConfig _functionsConfig;
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(
            ICosmosDbService cosmosDbService,
            IHttpClientFactory httpClientFactory,
            IAuditLogService auditLogService,
            IOptions<AzureFunctionsConfig> functionsConfig,
            ILogger<SessionsController> logger)
        {
            _cosmosDbService = cosmosDbService;
            _httpClientFactory = httpClientFactory;
            _auditLogService = auditLogService;
            _functionsConfig = functionsConfig.Value;
            _logger = logger;
        }

        // GET: Sessions
        public async Task<IActionResult> Index(string? track, string? level)
        {
            IEnumerable<Session> sessions;
            
            if (!string.IsNullOrEmpty(track) || !string.IsNullOrEmpty(level))
            {
                sessions = await _cosmosDbService.GetSessionsByFilterAsync(track, level);
                ViewBag.SelectedTrack = track;
                ViewBag.SelectedLevel = level;
            }
            else
            {
                sessions = await _cosmosDbService.GetSessionsAsync();
            }

            // Get unique tracks and levels for filter dropdowns
            var allSessions = await _cosmosDbService.GetSessionsAsync();
            ViewBag.Tracks = allSessions.Select(s => s.Track).Distinct().OrderBy(t => t).ToList();
            ViewBag.Levels = allSessions.Select(s => s.Level).Distinct().OrderBy(l => l).ToList();

            return View(sessions);
        }

        // GET: Sessions/Details/5
        public async Task<IActionResult> Details(string id)
        {
            var session = await _cosmosDbService.GetSessionByIdAsync(id);
            if (session == null)
            {
                return NotFound();
            }

            // Get actual registration count from Cosmos DB
            session.CurrentRegistrations = await _cosmosDbService.GetRegistrationCountBySessionAsync(id);

            return View(session);
        }

        // POST: Sessions/Register
        [HttpPost]
        public async Task<IActionResult> Register(string sessionId, string attendeeName, string attendeeEmail)
        {
            var session = await _cosmosDbService.GetSessionByIdAsync(sessionId);
            if (session == null)
            {
                return NotFound();
            }

            // Check if registration is closed
            if (session.RegistrationClosed)
            {
                TempData["Error"] = "Registration for this session is closed.";
                return RedirectToAction(nameof(Details), new { id = sessionId });
            }

            // Get current registration count
            var currentCount = await _cosmosDbService.GetRegistrationCountBySessionAsync(sessionId);
            if (currentCount >= session.Capacity)
            {
                TempData["Error"] = "This session is at full capacity.";
                return RedirectToAction(nameof(Details), new { id = sessionId });
            }

            var registration = new Registration
            {
                SessionId = sessionId,
                SessionTitle = session.Title,
                AttendeeName = attendeeName,
                AttendeeEmail = attendeeEmail
            };

            await _cosmosDbService.AddRegistrationAsync(registration);

            // Log to audit table
            await _auditLogService.LogRegistrationAsync(
                int.Parse(session.SessionNumber.ToString()), 
                session.Title, 
                attendeeName, 
                attendeeEmail);

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

                var json = System.Text.Json.JsonSerializer.Serialize(registrationRequest);
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

---

## Part 6: Data Migration Script

Create a migration script to move existing data to Cosmos DB.

Create `Scripts/MigrateToCosmosDb.cs`:
```csharp
using Microsoft.Azure.Cosmos;
using ConferenceHub.Models;
using System.Text.Json;

public class DataMigration
{
    public static async Task MigrateSessionsAsync(string cosmosConnectionString, string databaseName)
    {
        var cosmosClient = new CosmosClient(cosmosConnectionString);
        var container = cosmosClient.GetContainer(databaseName, "Sessions");

        // Read existing sessions from JSON
        var jsonPath = "Data/sessions.json";
        var jsonContent = await File.ReadAllTextAsync(jsonPath);
        var oldSessions = JsonSerializer.Deserialize<List<OldSession>>(jsonContent,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (oldSessions == null) return;

        // Migrate each session
        foreach (var oldSession in oldSessions)
        {
            var newSession = new Session
            {
                Id = Guid.NewGuid().ToString(),
                ConferenceId = "az204-2025",
                SessionNumber = oldSession.Id,
                Title = oldSession.Title,
                Speaker = oldSession.Speaker,
                StartTime = oldSession.StartTime,
                EndTime = oldSession.EndTime,
                Room = oldSession.Room,
                Description = oldSession.Description,
                Capacity = oldSession.Capacity,
                CurrentRegistrations = oldSession.CurrentRegistrations,
                SlideUrl = oldSession.SlideUrl,
                Track = DetermineTrack(oldSession.Title),
                Level = "Intermediate",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await container.CreateItemAsync(newSession, new PartitionKey(newSession.ConferenceId));
            Console.WriteLine($"Migrated session: {newSession.Title}");
        }

        Console.WriteLine($"Successfully migrated {oldSessions.Count} sessions");
    }

    private static string DetermineTrack(string title)
    {
        if (title.Contains("Cloud") || title.Contains("Azure")) return "Cloud";
        if (title.Contains("Function") || title.Contains("Serverless")) return "Serverless";
        if (title.Contains("Container") || title.Contains("Docker")) return "DevOps";
        if (title.Contains("Security")) return "Security";
        if (title.Contains("Storage") || title.Contains("Database")) return "Data";
        return "General";
    }

    // Old session model for migration
    private class OldSession
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Speaker { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Room { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public int CurrentRegistrations { get; set; }
        public string? SlideUrl { get; set; }
    }
}
```

Run the migration:
```powershell
# Add to Program.cs temporarily or create a console app
await DataMigration.MigrateSessionsAsync(cosmosConnectionString, "$cosmosDatabaseName");
```

dotnet run

---

## Part 7: Update Azure Functions

### Update CloseRegistrations Function

cp ../Learning\ Path/04-CosmosDB/ConferenceHubFunctions/CloseRegistrations.cs .

Update `ConferenceHub.Functions/CloseRegistrations.cs`:
```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;

namespace ConferenceHub.Functions
{
    public class CloseRegistrations
    {
        private readonly ILogger _logger;
        private readonly CosmosClient _cosmosClient;

        public CloseRegistrations(ILoggerFactory loggerFactory, CosmosClient cosmosClient)
        {
            _logger = loggerFactory.CreateLogger<CloseRegistrations>();
            _cosmosClient = cosmosClient;
        }

        [Function("CloseRegistrations")]
        public async Task Run([TimerTrigger("0 0 2 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation("CloseRegistrations function triggered at: {Time}", DateTime.Now);

            try
            {
                var container = _cosmosClient.GetContainer("$cosmosDatabaseName", "Sessions");
                var cutoffTime = DateTime.UtcNow.AddHours(24);

                _logger.LogInformation("Checking for sessions starting before: {CutoffTime}", cutoffTime);

                // Query for sessions that need to be closed
                var query = new QueryDefinition(
                    "SELECT * FROM Sessions s WHERE s.startTime < @cutoffTime AND s.registrationClosed = false")
                    .WithParameter("@cutoffTime", cutoffTime);

                var iterator = container.GetItemQueryIterator<dynamic>(query);
                var sessionsToClose = new List<dynamic>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    sessionsToClose.AddRange(response);
                }

                // Update each session
                foreach (var session in sessionsToClose)
                {
                    session.registrationClosed = true;
                    session.updatedAt = DateTime.UtcNow;

                    await container.ReplaceItemAsync(
                        session,
                        session.id.ToString(),
                        new PartitionKey(session.conferenceId.ToString()));

                    _logger.LogInformation(
                        "Closed registration for session {SessionId}: {Title}",
                        session.id,
                        session.title);
                }

                _logger.LogInformation("Closed registration for {Count} sessions", sessionsToClose.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing registrations");
            }
        }
    }
}
```

Update `ConferenceHub.Functions/Program.cs` to add Cosmos DB:

cp ../Learning\ Path/04-CosmosDB/ConferenceHubFunctions/Program.cs .

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        
        // Add Cosmos DB client
        var cosmosConnectionString = Environment.GetEnvironmentVariable("CosmosDbConnectionString");
        services.AddSingleton(sp => new CosmosClient(cosmosConnectionString));
    })
    .Build();

host.Run();
```

---

## Part 8: Deploy to Azure

### Step 1: Update App Settings

```powershell
# Get Cosmos DB connection string
$cosmosConnectionString = az cosmosdb keys list `
  --name $cosmosAccountName `
  --resource-group $resourceGroupName `
  --type connection-strings `
  --query "connectionStrings[0].connectionString" `
  --output tsv

# Update Web App settings
az webapp config appsettings set `
  --name $webAppName `
  --resource-group $resourceGroupName `
  --settings CosmosDb__ConnectionString="$cosmosConnectionString" `
             CosmosDb__DatabaseName="$cosmosDatabaseName"

# Update Function App settings
az functionapp config appsettings set `
  --name $functionAppName `
  --resource-group $resourceGroupName `
  --settings CosmosDbConnectionString="$cosmosConnectionString"
```
**Bash**
```bash
cosmosConnectionString=$(az cosmosdb keys list \
  --name "$cosmosAccountName" \
  --resource-group "$resourceGroupName" \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" \
  --output tsv)

az webapp config appsettings set \
  --name "$webAppName" \
  --resource-group "$resourceGroupName" \
  --settings CosmosDb__ConnectionString="$cosmosConnectionString" \
             CosmosDb__DatabaseName="$cosmosDatabaseName"

az functionapp config appsettings set \
  --name "$functionAppName" \
  --resource-group "$resourceGroupName" \
  --settings CosmosDbConnectionString="$cosmosConnectionString"
```

### Step 2: Deploy Updated Applications

```powershell
# Deploy Web App (Linux, self-contained)
cd ConferenceHub
dotnet publish -c Release -r linux-x64 --self-contained true -o ./publish
Compress-Archive -Path ./publish/* -DestinationPath ./app.zip -Force
az webapp deploy `
  --resource-group $resourceGroupName `
  --name $webAppName `
  --src ./app.zip

# Deploy Functions
cd ../ConferenceHub.Functions
func azure functionapp publish $functionAppName
```
**Bash**
```bash
# Deploy Web App (Linux, self-contained)
cd ConferenceHub
dotnet publish -c Release -r linux-x64 --self-contained true -o ./publish
( cd publish && zip -r ../app.zip . )
az webapp deploy \
  --resource-group "$resourceGroupName" \
  --name "$webAppName" \
  --src-path ./app.zip --type zip

# Deploy Functions
cd ../ConferenceHub.Functions
func azure functionapp publish "$functionAppName"
```

---

## Part 9: Test the Application


### Test Registrations

1. Register for a session
2. Verify registration is saved to Cosmos DB
3. Check registration count updates
4. View registrations in Azure Portal

### Verify in Azure Portal

1. Navigate to Cosmos DB account
2. Browse "Sessions" container
3. Browse "Registrations" container
4. Run queries in Data Explorer:
```sql
SELECT * FROM Sessions s WHERE s.track = "Cloud"
SELECT * FROM Registrations r WHERE r.sessionId = "your-session-id"
```

---

## Summary

You've successfully:
- ✅ Created Azure Cosmos DB account and containers
- ✅ Migrated from JSON file storage to Cosmos DB
- ✅ Implemented advanced querying and filtering
- ✅ Updated Azure Functions to work with Cosmos DB
- ✅ Added partition key strategy for optimal performance

## Next Steps

In **Learning Path 5**, you'll:
- Containerize the application with **Docker**
- Create Dockerfile and docker-compose
- Deploy to **Azure Container Apps** or **App Service for Containers**
- Configure environment variables for containers

---

## Troubleshooting

### Connection issues
- Verify Cosmos DB connection string
- Check firewall rules in Cosmos DB
- Ensure network access is configured

### Query performance
- Verify partition key is used in queries
- Check Request Units (RU) consumption
- Consider adding indexes for frequently queried fields

### Migration issues
- Backup existing data before migration
- Test migration script with small dataset first
- Verify data types match between old and new models

## Azure DevOps Pipeline (Incremental Deployment)
- Pipeline: `Learning Path/04-CosmosDB/azure-pipelines.yml`
- Bicep: `Learning Path/04-CosmosDB/infra.bicep`
- Required variables: `azureSubscription`, `resourceGroupName`, `location`, `cosmosAccountName`, `cosmosDatabaseName`, `storageAccountName`, `functionAppName`, `mainWebAppName`
- Notes: The pipeline provisions Cosmos DB and updates web app settings for `CosmosDb__ConnectionString`/`CosmosDb__DatabaseName` plus Storage/Functions settings.
