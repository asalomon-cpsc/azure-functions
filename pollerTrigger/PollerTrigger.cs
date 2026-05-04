using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class PollerTrigger
{
    private readonly ILogger<PollerTrigger> _logger;

    public PollerTrigger(ILogger<PollerTrigger> logger) => _logger = logger;

    [Function("pollerTrigger")]
    public async Task Run(
        [TimerTrigger("0 */60 * * * *")] TimerInfo myTimer,
        [DurableClient] DurableTaskClient durableClient)
    {
        _logger.LogInformation("Timer fired at: {Time} — starting poll orchestration.", DateTime.UtcNow);
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(StatusPollerOrchestrator));
        _logger.LogInformation("Started orchestration {InstanceId}.", instanceId);
    }
}
