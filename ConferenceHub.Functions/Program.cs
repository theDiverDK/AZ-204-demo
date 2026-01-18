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