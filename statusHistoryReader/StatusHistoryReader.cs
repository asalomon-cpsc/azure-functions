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
        int days = int.TryParse(req.Query["days"], out var d) ? Math.Clamp(d, 1, 90) : 30;
        int pageSize = int.TryParse(req.Query["pageSize"], out var ps) ? Math.Clamp(ps, 1, 500) : 200;
        string? pageToken = string.IsNullOrEmpty(req.Query["pageToken"]) ? null : req.Query["pageToken"];

        var cutoff = DateTime.UtcNow.AddDays(-days);
        string filter;

        if (!string.IsNullOrWhiteSpace(urlName))
        {
            // RowKey range filter is O(result set) — uses the row index directly.
            // RowKey format: urlName_<ISO8601 UTC> — sorts lexicographically by time.
            filter = $"PartitionKey eq 'statuses' and RowKey ge '{urlName}_{cutoff:o}' and RowKey lt '{urlName}_~'";
        }
        else
        {
            // All-URLs scan: Timestamp filter is unindexed but acceptable for low-frequency admin views.
            filter = $"PartitionKey eq 'statuses' and Timestamp ge datetime'{cutoff:yyyy-MM-ddTHH:mm:ssZ}'";
        }

        var items = new List<StatusTableEntity>();
        string? nextPageToken = null;

        await foreach (var page in tableClient.QueryAsync<StatusTableEntity>(filter).AsPages(pageToken, pageSize))
        {
            items.AddRange(page.Values);
            nextPageToken = page.ContinuationToken;
            break; // return one page per request; client uses nextPageToken to load more
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { items, nextPageToken });
        return response;
    }
}
