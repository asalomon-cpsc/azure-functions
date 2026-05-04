using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public static class StatusPollerOrchestrator
{
    [Function(nameof(StatusPollerOrchestrator))]
    public static async Task RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(StatusPollerOrchestrator));

        // Step 1: get URL list via activity (no direct I/O in orchestrator)
        var urls = await context.CallActivityAsync<List<UrlPollItem>>(
            nameof(GetUrlListActivity), input: string.Empty);

        if (urls is null || urls.Count == 0)
        {
            logger.LogInformation("No URLs configured — skipping poll cycle.");
            return;
        }

        logger.LogInformation("Fanning out to poll {Count} URL(s).", urls.Count);

        // Step 2: fan-out — poll all URLs in parallel, 3 retries each
        var retryOptions = new TaskOptions
        {
            Retry = new RetryPolicy(
                maxNumberOfAttempts: 3,
                firstRetryInterval: TimeSpan.FromSeconds(5))
        };

        var pollTasks = urls
            .Select(url => context.CallActivityAsync<PollResult>(
                nameof(PollUrlActivity), url, retryOptions))
            .ToList();

        var results = await Task.WhenAll(pollTasks);

        // Step 3: fan-in — notify only on failures
        var failures = results.Where(r => r.Status != "OK").ToList();
        logger.LogInformation(
            "Poll cycle complete. {FailCount} failure(s) out of {Total} URL(s).",
            failures.Count, results.Length);

        if (failures.Count > 0)
        {
            await context.CallActivityAsync(nameof(SendNotificationActivity), failures);
        }
    }
}
