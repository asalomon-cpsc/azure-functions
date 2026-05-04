using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CpscFunctions;

public class UrlPersister
{
    private readonly ILogger<UrlPersister> _logger;

    public UrlPersister(ILogger<UrlPersister> logger) => _logger = logger;

    [Function("urlPersister")]
    public async Task<UrlPersisterOutput> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", "delete", "put")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var body = await JsonSerializer.DeserializeAsync<List<JsonElement>>(req.Body);
        var urlList = new List<UrlManagementMessage>();

        if (body is not null)
        {
            foreach (var item in body)
            {
                var urlName = item.TryGetProperty("urlName", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                var url = item.TryGetProperty("url", out var u) ? u.GetString() ?? string.Empty : string.Empty;

                var msg = new UrlManagementMessage
                {
                    UrlName = urlName,
                    Url = url,
                    Date = DateTime.UtcNow,
                    Action = string.IsNullOrEmpty(url) ? "DELETE" : req.Method.ToUpperInvariant()
                };

                _logger.LogInformation("{UrlName} - {Action}", msg.UrlName, msg.Action);
                urlList.Add(msg);
            }
        }

        var response = req.CreateResponse(HttpStatusCode.Accepted);

        return new UrlPersisterOutput
        {
            QueueMessage = JsonSerializer.Serialize(urlList),
            HttpResponse = response
        };
    }
}

public class UrlPersisterOutput
{
    [QueueOutput("url-management-queue", Connection = "AzureWebJobsStorage")]
    public string? QueueMessage { get; set; }

    public HttpResponseData? HttpResponse { get; set; }
}
