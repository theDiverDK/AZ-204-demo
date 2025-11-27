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
