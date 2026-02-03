using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ConferenceHubFunctions.Authorization;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddSingleton<JwtValidator>();
    })
    .Build();

host.Run();
