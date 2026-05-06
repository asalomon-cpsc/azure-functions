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

        // Step 2: capture previous states before polling so we can detect transitions
        var previousStates = await context.CallActivityAsync<Dictionary<string, string>>(
            nameof(GetPreviousStatesActivity), input: string.Empty);

        logger.LogInformation("Fanning out to poll {Count} URL(s).", urls.Count);

        // Step 3: fan-out — poll all URLs in parallel, 3 retries each
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

        // Step 4: fan-in — detect state transitions only
        var newlyDown = new List<PollResult>();
        var newlyUp = new List<PollResult>();

        foreach (var result in results)
        {
            var wasOk = !previousStates.TryGetValue(result.UrlName, out var prev) || prev == "OK";
            var isOk = result.Status == "OK";

            if (!wasOk && isOk)
                newlyUp.Add(result);
            else if (wasOk && !isOk)
                newlyDown.Add(result);
            // no change → no notification
        }

        logger.LogInformation(
            "Poll cycle complete. {DownCount} newly down, {UpCount} newly recovered, out of {Total} URL(s).",
            newlyDown.Count, newlyUp.Count, results.Length);

        // Step 5: send notifications and prune concurrently
        var pruneTask = context.CallActivityAsync(nameof(PruneHistoryActivity), urls);

        if (newlyDown.Count > 0)
        {
            await context.CallActivityAsync(nameof(SendNotificationActivity),
                new NotificationRequest { Type = NotificationType.Down, Items = newlyDown });
        }

        if (newlyUp.Count > 0)
        {
            await context.CallActivityAsync(nameof(SendNotificationActivity),
                new NotificationRequest { Type = NotificationType.Recovery, Items = newlyUp });
        }

        await pruneTask;
    }
}
