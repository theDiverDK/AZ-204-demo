using ConferenceHub.Services;
using ConferenceHub.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);
var appConfigEnabled = false;

// Add App Configuration
if (!builder.Environment.IsDevelopment() &&
    !string.IsNullOrWhiteSpace(builder.Configuration["AppConfiguration:Endpoint"]))
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
    appConfigEnabled = true;

    // Add Key Vault configuration
    if (!string.IsNullOrWhiteSpace(builder.Configuration["KeyVault:VaultUri"]))
    {
        var keyVaultUri = new Uri(builder.Configuration["KeyVault:VaultUri"]!);
        builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
    }
}
var eventHubConnectionString = builder.Configuration["EventHub:ConnectionString"];
var eventHubName = "session-feedback";
builder.Services.AddSingleton<IEventHubService>(sp => 
    new EventHubService(
        eventHubConnectionString!, 
        eventHubName, 
        sp.GetRequiredService<ILogger<EventHubService>>()));
        
// Add Feature Management
builder.Services.AddFeatureManagement();

// Add App Configuration refresh middleware
if (appConfigEnabled)
{
    builder.Services.AddAzureAppConfiguration();
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

// Configure Cosmos DB (used by learning paths >= 4)
var cosmosConnectionString =
    Environment.GetEnvironmentVariable("CosmosDb__ConnectionString")
    ?? builder.Configuration["CosmosDb:ConnectionString"];
var cosmosDatabaseName =
    Environment.GetEnvironmentVariable("CosmosDb__DatabaseName")
    ?? builder.Configuration["CosmosDb:DatabaseName"];

// Defensive cleanup in case values come quoted from App Configuration/Key Vault references.
cosmosConnectionString = cosmosConnectionString?.Trim();
if (!string.IsNullOrEmpty(cosmosConnectionString) &&
    cosmosConnectionString.StartsWith("\"") &&
    cosmosConnectionString.EndsWith("\""))
{
    cosmosConnectionString = cosmosConnectionString[1..^1];
}

if (!string.IsNullOrWhiteSpace(cosmosConnectionString) &&
    !string.IsNullOrWhiteSpace(cosmosDatabaseName))
{
    try
    {
        var cosmosClient = new CosmosClient(cosmosConnectionString);
        builder.Services.AddSingleton(cosmosClient);
        builder.Services.AddSingleton<ICosmosDbService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<CosmosDbService>>();
            return new CosmosDbService(cosmosClient, cosmosDatabaseName, logger);
        });
        builder.Services.AddSingleton<IDataService, CosmosDataService>();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Cosmos disabled due to invalid configuration: {ex.Message}");
        builder.Services.AddSingleton<IDataService, DataService>();
    }
}
else
{
    // Fallback for early learning paths/local scenarios without Cosmos.
    builder.Services.AddSingleton<IDataService, DataService>();
}

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

// Configure settings from App Configuration
builder.Services.Configure<ConferenceHubSettings>(
    builder.Configuration.GetSection("ConferenceHub"));
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Use App Configuration refresh middleware
if (appConfigEnabled)
{
    app.UseAzureAppConfiguration();
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
