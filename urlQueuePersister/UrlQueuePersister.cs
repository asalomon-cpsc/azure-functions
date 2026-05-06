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
                _logger.LogWarning("Entity {UrlName} not found for delete, skipping.", item.UrlName);
            }
        }
    }
}
