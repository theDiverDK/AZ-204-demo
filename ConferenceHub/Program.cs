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
//builder.Services.AddSingleton<IDataService, DataService>();

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


//await DataMigration.MigrateSessionsAsync(cosmosConnectionString, "ConferenceHubDB");

app.Run();
