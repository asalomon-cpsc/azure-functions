using System.Text;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class StatusQueuePersister
{
    private readonly ILogger<StatusQueuePersister> _logger;

    public StatusQueuePersister(ILogger<StatusQueuePersister> logger) => _logger = logger;

    [Function("statusQueuePersister")]
    public async Task Run(
        [QueueTrigger("status-states-queue", Connection = "AzureWebJobsStorage")] StatusPollResult state,
        [TableInput("statusTable", Connection = "AzureWebJobsStorage")] TableClient tableClient)
    {
        _logger.LogInformation("Persisting current state for {UrlName}", state.UrlName);

        var entity = new StatusTableEntity
        {
            PartitionKey = "statuses",
            RowKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(state.Url)),
            UrlName = state.UrlName,
            Url = state.Url,
            Status = state.Status,
            Description = state.Description,
            Date = state.Date,
            ETag = ETag.All
        };

        try
        {
            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
            _logger.LogInformation("Upserted state for {UrlName}", state.UrlName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert state for {UrlName}, RowKey: {RowKey}",
                state.UrlName, entity.RowKey);
            throw;
        }
    }
}
