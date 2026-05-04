using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class StatusHistoryReader
{
    private readonly ILogger<StatusHistoryReader> _logger;

    public StatusHistoryReader(ILogger<StatusHistoryReader> logger) => _logger = logger;

    [Function("statusHistoryReader")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
        [TableInput("statusHistoryTable", Connection = "AzureWebJobsStorage")] TableClient tableClient)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var urlName = req.Query["urlName"];
        var results = new List<StatusTableEntity>();

        // Bug fix from original: the history writer stores RowKey as "urlName_timestamp",
        // so an exact RowKey == urlName match never found anything.
        // Fixed to use a prefix scan: RowKey starts with "urlName_".
        string filter = string.IsNullOrWhiteSpace(urlName)
            ? "PartitionKey eq 'statuses'"
            : $"PartitionKey eq 'statuses' and RowKey ge '{urlName}_' and RowKey lt '{urlName}_~'";

        await foreach (var entity in tableClient.QueryAsync<StatusTableEntity>(filter))
        {
            results.Add(entity);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(results);
        return response;
    }
}
