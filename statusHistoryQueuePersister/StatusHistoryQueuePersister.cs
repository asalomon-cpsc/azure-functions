using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class StatusHistoryQueuePersister
{
    private readonly ILogger<StatusHistoryQueuePersister> _logger;

    public StatusHistoryQueuePersister(ILogger<StatusHistoryQueuePersister> logger) => _logger = logger;

    [Function("statusHistoryQueuePersister")]
    public async Task Run(
        [QueueTrigger("status-history-queue", Connection = "AzureWebJobsStorage")] StatusPollResult state,
        [TableInput("statusHistoryTable", Connection = "AzureWebJobsStorage")] TableClient tableClient)
    {
        _logger.LogInformation("Persisting history for {UrlName}", state.UrlName);

        var entity = new StatusTableEntity
        {
            PartitionKey = "statuses",
            RowKey = $"{state.UrlName}_{DateTime.UtcNow:o}",
            UrlName = state.UrlName,
            Url = state.Url,
            Status = state.Status,
            Description = state.Description,
            Date = DateTime.UtcNow,
            ETag = ETag.All
        };

        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
        _logger.LogInformation("Inserted history row {RowKey}", entity.RowKey);
    }
}
