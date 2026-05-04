using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class PollerTrigger
{
    private readonly ILogger<PollerTrigger> _logger;

    public PollerTrigger(ILogger<PollerTrigger> logger) => _logger = logger;

    [Function("pollerTrigger")]
    [QueueOutput("status-initiator-queue", Connection = "AzureWebJobsStorage")]
    public string Run([TimerTrigger("0 */60 * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("C# Timer trigger function executed at: {Time}", DateTime.Now);
        return "Start";
    }
}
