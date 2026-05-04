using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class UrlQueuePersister
{
    private readonly ILogger<UrlQueuePersister> _logger;

    public UrlQueuePersister(ILogger<UrlQueuePersister> logger) => _logger = logger;

    [Function("urlQueuePersister")]
    public async Task Run(
        [QueueTrigger("url-management-queue", Connection = "AzureWebJobsStorage")] string message,
        [TableInput("urlTable", Connection = "AzureWebJobsStorage")] TableClient tableClient)
    {
        var urls = JsonSerializer.Deserialize<List<UrlManagementMessage>>(message,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (urls is null) return;

        foreach (var item in urls)
        {
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
            }
            else if (item.Action == "DELETE")
            {
                try
                {
                    await tableClient.DeleteEntityAsync("urls", item.UrlName, ETag.All);
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    _logger.LogWarning("Entity {UrlName} not found for delete, skipping.", item.UrlName);
                }
            }
        }
    }
}
