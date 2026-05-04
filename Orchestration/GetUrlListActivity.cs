using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

/// <summary>
/// Reads all URLs from urlTable and returns them as UrlPollItem list.
/// Called by StatusPollerOrchestrator as the first fan-out step.
/// Replaces the old statusUrlTaskAssigner HTTP-call-to-self pattern.
/// </summary>
public class GetUrlListActivity
{
    private readonly TableServiceClient _tableService;
    private readonly ILogger<GetUrlListActivity> _logger;

    public GetUrlListActivity(TableServiceClient tableService, ILogger<GetUrlListActivity> logger)
    {
        _tableService = tableService;
        _logger = logger;
    }

    [Function(nameof(GetUrlListActivity))]
    public async Task<List<UrlPollItem>> Run([ActivityTrigger] string? input)
    {
        _logger.LogInformation("Reading URL list from urlTable.");

        var tableClient = _tableService.GetTableClient("urlTable");
        var results = new List<UrlPollItem>();

        await foreach (var entity in tableClient.QueryAsync<UrlTableEntity>(
            e => e.PartitionKey == "urls"))
        {
            if (!string.IsNullOrEmpty(entity.Url))
                results.Add(new UrlPollItem { UrlName = entity.UrlName, Url = entity.Url });
        }

        _logger.LogInformation("Found {Count} URL(s) to poll.", results.Count);
        return results;
    }
}
