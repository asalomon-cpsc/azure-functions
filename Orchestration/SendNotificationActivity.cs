using System.Text;
using Azure.Communication.Email;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

/// <summary>
/// Sends an ACS email alert for all failing URLs.
/// Replaces the old emailAlerter_csharp queue-triggered function.
/// Called by StatusPollerOrchestrator only when at least one URL is non-OK.
/// </summary>
public class SendNotificationActivity
{
    private readonly EmailClient _emailClient;
    private readonly ILogger<SendNotificationActivity> _logger;

    public SendNotificationActivity(EmailClient emailClient, ILogger<SendNotificationActivity> logger)
    {
        _emailClient = emailClient;
        _logger = logger;
    }

    [Function(nameof(SendNotificationActivity))]
    public async Task Run([ActivityTrigger] List<PollResult> failures)
    {
        if (failures is null || failures.Count == 0) return;

        var sender = Environment.GetEnvironmentVariable("EMAIL_SENDER")
            ?? throw new InvalidOperationException("EMAIL_SENDER app setting is not configured.");

        var subject = Environment.GetEnvironmentVariable("EMAIL_SUBJECT")
            ?? "Azure Site Status Notification";

        var recipientsEnv = Environment.GetEnvironmentVariable("EMAIL_RECIPIENTS")
            ?? throw new InvalidOperationException("EMAIL_RECIPIENTS app setting is not configured.");

        var dashboardUrl = Environment.GetEnvironmentVariable("dashboard_url") ?? "Location not loaded";

        var body = new StringBuilder();
        body.AppendLine("<h3>The following resources had or have a status change: </h3>");

        foreach (var state in failures)
        {
            _logger.LogInformation("{UrlName} | {Status} | {Description}",
                state.UrlName, state.Status, state.Description);

            body.AppendLine($"<p>UrlName : <strong>{state.UrlName}</strong></p>");
            body.AppendLine($"<p>Url : {state.Url}</p>");
            body.AppendLine($"<p>Poll Status : {state.Status}</p>");
            body.AppendLine($"<p>Status Description : {state.Description}</p>");
            body.AppendLine("<hr/>");
            body.AppendLine("---------------");
            body.AppendLine($"<p>To view the dashboard for all sites click here <strong>(DOES NOT WORK WITH IE BROWSER)</strong>: <a href='{dashboardUrl}'>{dashboardUrl}</a></p>");
        }

        var recipients = recipientsEnv
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => new EmailAddress(e))
            .ToList();

        var emailMessage = new EmailMessage(
            senderAddress: sender,
            content: new EmailContent(subject) { Html = body.ToString() },
            recipients: new EmailRecipients(recipients));

        var operation = await _emailClient.SendAsync(Azure.WaitUntil.Started, emailMessage);
        _logger.LogInformation("Email send operation started. OperationId: {OperationId}", operation.Id);
    }
}
