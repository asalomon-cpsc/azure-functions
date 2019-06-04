#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using System;
using System.Text;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.Azure;
using System.Net;
public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, ICollector<StateEntity> urlValues, TraceWriter log)
{
    log.Info("C# HTTP trigger function processed a request.");
    var urls = new Dictionary<string, string>();
    HttpResponseMessage response;
    var functionUrl = Environment.GetEnvironmentVariable("STATUS_URL_LIST_PROXY");
    log.Info($"func url {functionUrl}");
    List<StateEntity> contents = default(List<StateEntity>);
    using (var handler = new HttpClientHandler { ClientCertificateOptions = ClientCertificateOption.Automatic })
    using (var client = new HttpClient(handler))
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.Timeout = TimeSpan.FromSeconds(60);
        response = await client.GetAsync(functionUrl);
        log.Info($"response code: {response.StatusCode.ToString()}");
        string content = await response.Content.ReadAsStringAsync();
        log.Info($"content is: {content}");
        
        try
        {
            contents = JsonConvert.DeserializeObject<List<StateEntity>>(content);
        }
        catch (Exception ex)
        {

            log.Info($"an exception accured while deserializing state entity {ex.Message}");
        }
        foreach (var status in contents)
        {
             urlValues.Add(status);
            // if(!urls.ContainsKey(status.UrlName)){
            //     urls.Add(status.UrlName, status.Url);

            // }
        }


    }

     return  (contents.Count == 0 || contents == null)
        ? req.CreateResponse(HttpStatusCode.InternalServerError, "Unable to retreive URL list from database")
        : req.CreateResponse(HttpStatusCode.OK, $"Successfully assigned {contents.Count} urls for polling!");

}
public class UrlItem
{
    public string UrlName { get; set; }
    public string Url { get; set; }

}

public class State : UrlItem
{
    public string Status { get; set; }
    public string Description { get; set; }
}

public class StateEntity : UrlItem
{

    public DateTime Date { get; set; }
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTime TimeStamp { get; set; }
    public string Etag { get; set; }
}
