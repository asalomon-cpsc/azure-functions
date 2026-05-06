using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

/// <summary>
/// Deletes statusHistoryTable rows older than HISTORY_RETENTION_DAYS (default 90).
/// Called by StatusPollerOrchestrator at the end of each poll cycle.
/// Uses batched TableTransactionAction deletes (max 100 per batch, same PartitionKey).
/// </summary>
public class PruneHistoryActivity
{
    private readonly TableServiceClient _tableService;
    private readonly ILogger<PruneHistoryActivity> _logger;

    public PruneHistoryActivity(TableServiceClient tableService, ILogger<PruneHistoryActivity> logger)
    {
        _tableService = tableService;
        _logger = logger;
    }

    [Function(nameof(PruneHistoryActivity))]
    public async Task Run([ActivityTrigger] List<UrlPollItem> urls)
    {
        int retentionDays = int.TryParse(
            Environment.GetEnvironmentVariable("HISTORY_RETENTION_DAYS"), out var d) ? d : 90;

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        _logger.LogInformation(
            "Pruning history rows older than {Cutoff} ({Days} days) for {UrlCount} URL(s).",
            cutoff, retentionDays, urls.Count);

        var historyTable = _tableService.GetTableClient("statusHistoryTable");
        await historyTable.CreateIfNotExistsAsync();

        int totalDeleted = 0;

        foreach (var url in urls)
        {
            // RowKey range: rows for this URL that are older than the cutoff.
            // Lower bound includes all rows starting with "urlName_".
            // Upper bound is the cutoff timestamp — anything before it gets deleted.
            var filter = $"PartitionKey eq 'statuses' " +
                         $"and RowKey ge '{url.UrlName}_' " +
                         $"and RowKey lt '{url.UrlName}_{cutoff:o}'";

            var batch = new List<TableTransactionAction>();

            await foreach (var entity in historyTable.QueryAsync<StatusTableEntity>(filter))
            {
                // ETag.All = unconditional delete (correct for bulk pruning)
                batch.Add(new TableTransactionAction(TableTransactionActionType.Delete, entity, ETag.All));

                if (batch.Count == 100)
                {
                    await historyTable.SubmitTransactionAsync(batch);
                    totalDeleted += batch.Count;
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await historyTable.SubmitTransactionAsync(batch);
                totalDeleted += batch.Count;
                batch.Clear();
            }
        }

        _logger.LogInformation("Prune complete. Deleted {Count} history row(s).", totalDeleted);
    }
}
