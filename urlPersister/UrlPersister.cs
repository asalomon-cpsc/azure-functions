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
        [HttpTrigger(AuthorizationLevel.Function, "post", "delete", "put", "options")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        if (req.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            return new UrlPersisterOutput { HttpResponse = req.CreateResponse(HttpStatusCode.OK) };

        var urlList = new List<UrlManagementMessage>();

        if (req.Method.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            // DELETE passes urlName as a query param — no request body
            var urlName = req.Query["urlName"] ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(urlName))
            {
                urlList.Add(new UrlManagementMessage
                {
                    UrlName = urlName,
                    Url = string.Empty,
                    Date = DateTime.UtcNow,
                    Action = "DELETE"
                });
                _logger.LogInformation("{UrlName} - DELETE", urlName);
            }
        }
        else
        {
            // POST / PUT send a JSON array body
            List<JsonElement>? body = null;
            if (req.Body.Length > 0)
                body = await JsonSerializer.DeserializeAsync<List<JsonElement>>(req.Body);

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
                        Action = req.Method.ToUpperInvariant()
                    };

                    _logger.LogInformation("{UrlName} - {Action}", msg.UrlName, msg.Action);
                    urlList.Add(msg);
                }
            }
        }

        var response = req.CreateResponse(HttpStatusCode.Accepted);

        return new UrlPersisterOutput
        {
            QueueMessage = urlList,
            HttpResponse = response
        };
    }
}

public class UrlPersisterOutput
{
    [QueueOutput("url-management-queue", Connection = "AzureWebJobsStorage")]
    public List<UrlManagementMessage>? QueueMessage { get; set; }

    public HttpResponseData? HttpResponse { get; set; }
}
