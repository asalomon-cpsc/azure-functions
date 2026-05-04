using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class HttpPollerTrigger
{
    private readonly ILogger<HttpPollerTrigger> _logger;

    public HttpPollerTrigger(ILogger<HttpPollerTrigger> logger) => _logger = logger;

    [Function("httpPollerTrigger")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient durableClient)
    {
        _logger.LogInformation("HTTP poller trigger — starting poll orchestration.");

        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(StatusPollerOrchestrator));

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteStringAsync(
            $"Polling request initiated. OrchestrationInstanceId: {instanceId}");
        return response;
    }
}
