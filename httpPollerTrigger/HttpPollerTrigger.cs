using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class HttpPollerTrigger
{
    private readonly ILogger<HttpPollerTrigger> _logger;

    public HttpPollerTrigger(ILogger<HttpPollerTrigger> logger) => _logger = logger;

    [Function("httpPollerTrigger")]
    public async Task<HttpPollerOutput> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP poller trigger function processed a request.");

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Polling request has been initiated");

        return new HttpPollerOutput { HttpResponse = response, QueueMessage = "Start" };
    }
}

public class HttpPollerOutput
{
    [QueueOutput("status-initiator-queue", Connection = "AzureWebJobsStorage")]
    public string? QueueMessage { get; set; }

    public HttpResponseData? HttpResponse { get; set; }
}
