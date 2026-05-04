using System.Net;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

/// <summary>
/// Polls a single URL, writes current state to statusTable,
/// writes a history row to statusHistoryTable, and returns a PollResult.
/// Replaces statusPoller_http + statusQueuePersister + statusHistoryQueuePersister.
/// </summary>
public class PollUrlActivity
{
    private const string GatewayTimeoutDescription =
        "More info here: https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/504";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TableServiceClient _tableService;
    private readonly ILogger<PollUrlActivity> _logger;

    public PollUrlActivity(
        IHttpClientFactory httpClientFactory,
        TableServiceClient tableService,
        ILogger<PollUrlActivity> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tableService = tableService;
        _logger = logger;
    }

    [Function(nameof(PollUrlActivity))]
    public async Task<PollResult> Run([ActivityTrigger] UrlPollItem item)
    {
        _logger.LogInformation("Polling: {UrlName} — {Url}", item.UrlName, item.Url);

        var result = await PollAsync(item.UrlName, item.Url);

        var now = DateTime.UtcNow;

        // Write current state (RowKey = urlName — one row per monitored URL)
        var statusTable = _tableService.GetTableClient("statusTable");
        var stateEntity = new StatusTableEntity
        {
            PartitionKey = "statuses",
            RowKey = item.UrlName,
            UrlName = item.UrlName,
            Url = item.Url,
            Status = result.Status,
            Description = result.Description,
            Date = now,
            ETag = ETag.All
        };

        try
        {
            await statusTable.UpsertEntityAsync(stateEntity, TableUpdateMode.Replace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert state for {UrlName}", item.UrlName);
            throw; // let Durable retry
        }

        // Write history row (RowKey = urlName_timestamp — one row per poll)
        var historyTable = _tableService.GetTableClient("statusHistoryTable");
        var historyEntity = new StatusTableEntity
        {
            PartitionKey = "statuses",
            RowKey = $"{item.UrlName}_{now:o}",
            UrlName = item.UrlName,
            Url = item.Url,
            Status = result.Status,
            Description = result.Description,
            Date = now,
            ETag = ETag.All
        };

        try
        {
            await historyTable.UpsertEntityAsync(historyEntity, TableUpdateMode.Replace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert history for {UrlName}", item.UrlName);
            throw;
        }

        _logger.LogInformation("Poll result for {UrlName}: {Status}", item.UrlName, result.Status);
        return result;
    }

    private async Task<PollResult> PollAsync(string urlName, string url)
    {
        HttpResponseMessage response;
        try
        {
            using var client = _httpClientFactory.CreateClient("poller");
            response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception polling {UrlName}", urlName);
            return new PollResult
            {
                UrlName = urlName,
                Url = url,
                Status = "000",
                Description = $"- {ex.Message} - This may be a transient error, a temporary error that is likely to disappear soon. You can view the dashboard and re-initiate the polling by clicking on the refresh button to be sure.",
                Date = DateTime.UtcNow
            };
        }

        _logger.LogInformation("HTTP {StatusCode} for {UrlName}", response.StatusCode, urlName);

        if (response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.RequestTimeout)
        {
            return new PollResult
            {
                UrlName = urlName,
                Url = url,
                Status = response.StatusCode.ToString(),
                Description = "Web Page Is Not Responding, Requests Are Timing Out",
                Date = DateTime.UtcNow
            };
        }

        if (response.StatusCode == HttpStatusCode.GatewayTimeout)
        {
            return new PollResult
            {
                UrlName = urlName,
                Url = url,
                Status = response.StatusCode.ToString(),
                Description = GatewayTimeoutDescription,
                Date = DateTime.UtcNow
            };
        }

        var reasonPhrase = response.ReasonPhrase ?? string.Empty;
        var status = CreateStatusMessageForFalsePositives(reasonPhrase);

        return new PollResult
        {
            UrlName = urlName,
            Url = url,
            Status = string.IsNullOrEmpty(status) ? response.StatusCode.ToString() : status,
            Description = string.IsNullOrEmpty(status) ? reasonPhrase : string.Empty,
            Date = DateTime.UtcNow
        };
    }

    private static string CreateStatusMessageForFalsePositives(string content) =>
        content.Contains("Under Maintenance")
            ? "Website is reponding but with error pages, please check servers. app pools, web server"
            : string.Empty;
}
