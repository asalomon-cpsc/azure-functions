using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

/// <summary>
/// Reads the current statusTable snapshot before polling begins.
/// Returns a dictionary of UrlName → Status so the orchestrator can
/// detect state transitions (OK → down, down → OK) after fan-in.
/// Missing entries (first run, newly added URLs) default to "OK"
/// so that a first-time failure still triggers a "down" alert.
/// </summary>
public class GetPreviousStatesActivity
{
    private readonly TableServiceClient _tableService;
    private readonly ILogger<GetPreviousStatesActivity> _logger;

    public GetPreviousStatesActivity(TableServiceClient tableService, ILogger<GetPreviousStatesActivity> logger)
    {
        _tableService = tableService;
        _logger = logger;
    }

    [Function(nameof(GetPreviousStatesActivity))]
    public async Task<Dictionary<string, string>> Run([ActivityTrigger] string? input)
    {
        var tableClient = _tableService.GetTableClient("statusTable");
        await tableClient.CreateIfNotExistsAsync();

        var states = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await foreach (var entity in tableClient.QueryAsync<StatusTableEntity>(
            e => e.PartitionKey == "statuses"))
        {
            states[entity.UrlName] = entity.Status;
        }

        _logger.LogInformation("Loaded {Count} previous state(s) from statusTable.", states.Count);
        return states;
    }
}
