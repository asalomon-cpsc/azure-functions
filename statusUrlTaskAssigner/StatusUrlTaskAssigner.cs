using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class StatusUrlTaskAssigner
{
    private readonly ILogger<StatusUrlTaskAssigner> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public StatusUrlTaskAssigner(ILogger<StatusUrlTaskAssigner> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    [Function("statusUrlTaskAssigner")]
    [QueueOutput("status-url-queue", Connection = "AzureWebJobsStorage")]
    public async Task<UrlPollItem[]> Run(
        [QueueTrigger("status-initiator-queue", Connection = "AzureWebJobsStorage")] string message)
    {
        _logger.LogInformation("Status URL task assigner triggered.");

        if (string.IsNullOrEmpty(message))
            return [];

        var functionUrl = Environment.GetEnvironmentVariable("STATUS_URL_LIST_PROXY")
            ?? throw new InvalidOperationException("STATUS_URL_LIST_PROXY is not configured.");

        _logger.LogInformation("Calling URL list proxy: {Url}", functionUrl);

        using var client = _httpClientFactory.CreateClient("internal");
        var response = await client.GetAsync(functionUrl);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Response code: {StatusCode}", response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var urls = JsonSerializer.Deserialize<List<UrlTableEntity>>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (urls is null || urls.Count == 0)
            return [];

        var items = urls
            .Select(u =>
            {
                _logger.LogInformation("Queuing {UrlName} for polling", u.UrlName);
                return new UrlPollItem { UrlName = u.UrlName, Url = u.Url };
            })
            .ToArray();

        return items;
    }
}
