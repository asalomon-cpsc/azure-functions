using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

/// <summary>
/// Returns pre-aggregated uptime stats from statusStatsTable.
/// One row per monitored URL — avoids full statusHistoryTable scans in the UI.
/// Supports optional ?urlName= for single-site lookup.
/// </summary>
public class StatusStatsReader
{
    private readonly TableServiceClient _tableService;
    private readonly ILogger<StatusStatsReader> _logger;

    public StatusStatsReader(TableServiceClient tableService, ILogger<StatusStatsReader> logger)
    {
        _tableService = tableService;
        _logger = logger;
    }

    [Function("statusStatsReader")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "options")] HttpRequestData req)
    {
        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            return req.CreateResponse(HttpStatusCode.OK);

        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var tableClient = _tableService.GetTableClient("statusStatsTable");
        await tableClient.CreateIfNotExistsAsync();

        var urlName = req.Query["urlName"];
        string filter = string.IsNullOrWhiteSpace(urlName)
            ? "PartitionKey eq 'stats'"
            : $"PartitionKey eq 'stats' and RowKey eq '{urlName}'";

        var results = new List<StatusStatsEntity>();
        await foreach (var entity in tableClient.QueryAsync<StatusStatsEntity>(filter))
        {
            results.Add(entity);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(results);
        return response;
    }
}
