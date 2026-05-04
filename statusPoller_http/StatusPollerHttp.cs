using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class StatusPollerHttp
{
    private const string GatewayTimeoutDescription =
        "More info here: https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/504";

    private readonly ILogger<StatusPollerHttp> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public StatusPollerHttp(ILogger<StatusPollerHttp> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    [Function("statusPoller_http")]
    public async Task<StatusPollerOutput> Run(
        [QueueTrigger("status-url-queue", Connection = "AzureWebJobsStorage")] UrlPollItem item)
    {
        _logger.LogInformation("Polling: {UrlName}", item.UrlName);

        var result = await PollAsync(item.UrlName, item.Url);

        string? notificationMessage = null;
        if (result.Status != "OK")
        {
            _logger.LogInformation("Added {UrlName} with status {Status} to notifications queue",
                item.UrlName, result.Status);
            notificationMessage = JsonSerializer.Serialize(new List<StatusPollResult> { result });
        }

        return new StatusPollerOutput
        {
            StateMessage = result,
            HistoryMessage = result,
            NotificationMessage = notificationMessage
        };
    }

    private async Task<StatusPollResult> PollAsync(string urlName, string url)
    {
        HttpResponseMessage response;
        try
        {
            using var client = _httpClientFactory.CreateClient("poller");
            response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception polling {UrlName}", urlName);
            return new StatusPollResult
            {
                UrlName = urlName,
                Url = url,
                Status = "000",
                Description = $"- {ex.Message} - This may be a transient error, a temporary error that is likely to disappear soon. You can view the dashboard and re-initiate the polling by clicking on the refresh button to be sure.",
                Date = DateTime.UtcNow
            };
        }

        _logger.LogInformation("Poll result for {UrlName}: {StatusCode}", urlName, response.StatusCode);

        if (response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.RequestTimeout)
        {
            return new StatusPollResult
            {
                UrlName = urlName,
                Url = url,
                Status = response.StatusCode.ToString(),
                Description = "Web Page Is Not Responding, Requests Are Timing Out",
                Date = DateTime.UtcNow
            };
        }

        if (response.StatusCode == HttpStatusCode.GatewayTimeout)
        {
            return new StatusPollResult
            {
                UrlName = urlName,
                Url = url,
                Status = response.StatusCode.ToString(),
                Description = GatewayTimeoutDescription,
                Date = DateTime.UtcNow
            };
        }

        var reasonPhrase = response.ReasonPhrase ?? string.Empty;
        var status = CreateStatusMessageForFalsePositives(reasonPhrase);

        return new StatusPollResult
        {
            UrlName = urlName,
            Url = url,
            Status = string.IsNullOrEmpty(status) ? response.StatusCode.ToString() : status,
            Description = string.IsNullOrEmpty(status) ? reasonPhrase : string.Empty,
            Date = DateTime.UtcNow
        };
    }

    private static string CreateStatusMessageForFalsePositives(string content) =>
        content.Contains("Under Maintenance")
            ? "Website is reponding but with error pages, please check servers. app pools, web server"
            : string.Empty;
}

public class StatusPollerOutput
{
    [QueueOutput("status-states-queue", Connection = "AzureWebJobsStorage")]
    public StatusPollResult? StateMessage { get; set; }

    [QueueOutput("status-history-queue", Connection = "AzureWebJobsStorage")]
    public StatusPollResult? HistoryMessage { get; set; }

    /// <summary>
    /// JSON-serialized List&lt;StatusPollResult&gt;. Set only when status is not OK.
    /// The emailAlerter function deserializes this as a list.
    /// </summary>
    [QueueOutput("status-notifications-queue", Connection = "AzureWebJobsStorage")]
    public string? NotificationMessage { get; set; }
}
