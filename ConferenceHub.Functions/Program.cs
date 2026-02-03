using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using ConferenceHubFunctions.Authorization;
using Azure.Identity;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
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
