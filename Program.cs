using Azure.Communication.Email;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Named HTTP client for polling external sites
        services.AddHttpClient("poller", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("User-Agent", "azure_cpsc");
        });

        // Azure Table Storage client — used by Durable activities that write directly to tables
        var storageConnection = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? "UseDevelopmentStorage=true";
        services.AddAzureClients(clients => clients.AddTableServiceClient(storageConnection));

        // ACS Email: prefer managed identity (ACS_ENDPOINT) in production,
        // fall back to connection string (ACS_CONNECTION_STRING) for local dev.
        var acsEndpoint = Environment.GetEnvironmentVariable("ACS_ENDPOINT");
        var acsConnectionString = Environment.GetEnvironmentVariable("ACS_CONNECTION_STRING");

        if (!string.IsNullOrEmpty(acsEndpoint))
        {
            services.AddSingleton(new EmailClient(new Uri(acsEndpoint), new DefaultAzureCredential()));
        }
        else if (!string.IsNullOrEmpty(acsConnectionString))
        {
            services.AddSingleton(new EmailClient(acsConnectionString));
        }
    })
    .Build();

await host.RunAsync();
