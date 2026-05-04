using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class StatusSiteStateReader
{
    private readonly ILogger<StatusSiteStateReader> _logger;

    public StatusSiteStateReader(ILogger<StatusSiteStateReader> logger) => _logger = logger;

    [Function("statusSiteStateReader")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", "options")] HttpRequestData req,
        [TableInput("statusTable", Connection = "AzureWebJobsStorage")] TableClient tableClient)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var results = new List<StatusTableEntity>();
        await foreach (var entity in tableClient.QueryAsync<StatusTableEntity>(
            e => e.PartitionKey == "statuses"))
        {
            results.Add(entity);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(results);
        return response;
    }
}
