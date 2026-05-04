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

        var urlName = req.Query["urlName"];

        // RowKey in statusTable is now urlName (set by PollUrlActivity in the new architecture)
        string filter = string.IsNullOrWhiteSpace(urlName)
            ? "PartitionKey eq 'statuses'"
            : $"PartitionKey eq 'statuses' and RowKey eq '{urlName}'";

        var results = new List<StatusTableEntity>();
        await foreach (var entity in tableClient.QueryAsync<StatusTableEntity>(filter))
        {
            results.Add(entity);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(results);
        return response;
    }
}
