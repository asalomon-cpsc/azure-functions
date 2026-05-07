using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class UrlQueuePersister
{
    private readonly TableServiceClient _tableService;
    private readonly ILogger<UrlQueuePersister> _logger;

    public UrlQueuePersister(TableServiceClient tableService, ILogger<UrlQueuePersister> logger)
    {
        _tableService = tableService;
        _logger = logger;
    }

    [Function("urlQueuePersister")]
    public async Task Run(
        [QueueTrigger("url-management-queue", Connection = "AzureWebJobsStorage")] UrlManagementMessage item)
    {
        var tableClient = _tableService.GetTableClient("urlTable");
        await tableClient.CreateIfNotExistsAsync();

        _logger.LogInformation("Url dequeued: {UrlName} - {Action}", item.UrlName, item.Action);

        var entity = new UrlTableEntity
        {
            PartitionKey = "urls",
            RowKey = item.UrlName,
            UrlName = item.UrlName,
            Url = item.Url,
            Action = item.Action,
            Date = item.Date,
            ETag = ETag.All
        };

        if (item.Action is "POST" or "PUT")
        {
            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
            _logger.LogInformation("Upserted {UrlName} to urlTable.", item.UrlName);

            // Write a PENDING placeholder to statusTable immediately so the URL appears
            // in readers right away. The next poll cycle will overwrite it with real data.
            var statusTable = _tableService.GetTableClient("statusTable");
            await statusTable.CreateIfNotExistsAsync();
            var placeholder = new StatusTableEntity
            {
                PartitionKey = "statuses",
                RowKey = item.UrlName,
                UrlName = item.UrlName,
                Url = item.Url,
                Status = "PENDING",
                Description = "Awaiting first poll.",
                Date = item.Date,
                ETag = ETag.All
            };
            await statusTable.UpsertEntityAsync(placeholder, TableUpdateMode.Replace);
            _logger.LogInformation("Wrote PENDING status for {UrlName} to statusTable.", item.UrlName);
        }
        else if (item.Action == "DELETE")
        {
            try
            {
                await tableClient.DeleteEntityAsync("urls", item.UrlName, ETag.All);
                _logger.LogInformation("Deleted {UrlName} from urlTable.", item.UrlName);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogWarning("Entity {UrlName} not found for delete in urlTable, skipping.", item.UrlName);
            }

            // Remove the current-state row from statusTable
            var statusTable = _tableService.GetTableClient("statusTable");
            try
            {
                await statusTable.DeleteEntityAsync("statuses", item.UrlName, ETag.All);
                _logger.LogInformation("Deleted {UrlName} from statusTable.", item.UrlName);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogWarning("Entity {UrlName} not found for delete in statusTable, skipping.", item.UrlName);
            }

            // Batch-delete all history rows for this URL from statusHistoryTable
            var historyTable = _tableService.GetTableClient("statusHistoryTable");
            var historyFilter = $"PartitionKey eq 'statuses' and RowKey ge '{item.UrlName}_' and RowKey lt '{item.UrlName}_~'";
            var batch = new List<TableTransactionAction>();
            int historyDeleted = 0;

            await foreach (var row in historyTable.QueryAsync<StatusTableEntity>(historyFilter))
            {
                batch.Add(new TableTransactionAction(TableTransactionActionType.Delete, row, ETag.All));
                if (batch.Count == 100)
                {
                    await historyTable.SubmitTransactionAsync(batch);
                    historyDeleted += batch.Count;
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                await historyTable.SubmitTransactionAsync(batch);
                historyDeleted += batch.Count;
            }
            _logger.LogInformation("Deleted {Count} history row(s) for {UrlName} from statusHistoryTable.", historyDeleted, item.UrlName);

            // Remove the stats row from statusStatsTable
            var statsTable = _tableService.GetTableClient("statusStatsTable");
            try
            {
                await statsTable.DeleteEntityAsync("stats", item.UrlName, ETag.All);
                _logger.LogInformation("Deleted {UrlName} from statusStatsTable.", item.UrlName);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogWarning("Entity {UrlName} not found for delete in statusStatsTable, skipping.", item.UrlName);
            }
        }
    }
}
