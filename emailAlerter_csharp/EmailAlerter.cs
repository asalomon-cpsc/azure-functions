using System.Text;
using System.Text.Json;
using Azure.Communication.Email;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class EmailAlerter
{
    private readonly ILogger<EmailAlerter> _logger;
    private readonly EmailClient _emailClient;

    public EmailAlerter(ILogger<EmailAlerter> logger, EmailClient emailClient)
    {
        _logger = logger;
        _emailClient = emailClient;
    }

    [Function("emailAlerter_csharp")]
    public async Task Run(
        [QueueTrigger("status-notifications-queue", Connection = "AzureWebJobsStorage")] string message)
    {
        _logger.LogInformation("Email alerter triggered.");

        var states = JsonSerializer.Deserialize<List<StatusPollResult>>(message,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (states is null || states.Count == 0) return;

        var dashboardUrl = Environment.GetEnvironmentVariable("dashboard_url") ?? "Location not loaded";

        var sender = Environment.GetEnvironmentVariable("EMAIL_SENDER")
            ?? throw new InvalidOperationException("EMAIL_SENDER app setting is not configured.");

        var subject = Environment.GetEnvironmentVariable("EMAIL_SUBJECT")
            ?? "Azure Site Status Notification";

        var recipientsEnv = Environment.GetEnvironmentVariable("EMAIL_RECIPIENTS")
            ?? throw new InvalidOperationException("EMAIL_RECIPIENTS app setting is not configured.");

        var body = new StringBuilder();
        body.AppendLine("<h3>The following resources had or have a status change: </h3>");

        foreach (var state in states)
        {
            _logger.LogInformation("{UrlName} | {Url} | {Date} | {Description}",
                state.UrlName, state.Url, state.Date, state.Description);

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
