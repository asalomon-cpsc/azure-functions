using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class StatusUrlListReader
{
    private readonly ILogger<StatusUrlListReader> _logger;

    public StatusUrlListReader(ILogger<StatusUrlListReader> logger) => _logger = logger;

    [Function("statusUrlListReader")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req,
        [TableInput("urlTable", Connection = "AzureWebJobsStorage")] TableClient tableClient)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var urlName = req.Query["urlName"];
        var results = new List<UrlTableEntity>();

        if (string.IsNullOrWhiteSpace(urlName))
        {
            await foreach (var entity in tableClient.QueryAsync<UrlTableEntity>(
                e => e.PartitionKey == "urls"))
            {
                results.Add(entity);
            }
        }
        else
        {
            await foreach (var entity in tableClient.QueryAsync<UrlTableEntity>(
                e => e.PartitionKey == "urls" && e.RowKey == urlName))
            {
                results.Add(entity);
            }
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(results);
        return response;
    }
}
