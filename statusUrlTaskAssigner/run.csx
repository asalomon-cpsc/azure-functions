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
public static async Task Run(CloudQueueMessage myQueueItem, 
    DateTimeOffset expirationTime, 
    DateTimeOffset insertionTime, 
    DateTimeOffset nextVisibleTime,
    string queueTrigger,
    string id,
    string popReceipt,
    int dequeueCount,
    ICollector<StateEntity> urlValues, TraceWriter log)
{
        log.Info("C# HTTP trigger function processed a request.");
        if(myQueueItem == null || string.IsNullOrEmpty(myQueueItem.AsString)){
            return;
        }
        
        var functionUrl = Environment.GetEnvironmentVariable("STATUS_URL_LIST_PROXY");
        HttpResponseMessage response;
        log.Info($"Function url {functionUrl}");
        List<StateEntity> contents = default(List<StateEntity>);
        using (var handler = new HttpClientHandler { ClientCertificateOptions = ClientCertificateOption.Automatic })
        using (var client = new HttpClient(handler))
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.Timeout = TimeSpan.FromSeconds(60);
            response = await client.GetAsync(functionUrl);
            log.Info($"Response code: {response.StatusCode.ToString()}");
            string content = await response.Content.ReadAsStringAsync();
            log.Info($"content is: {content}");
            
            try
            {
                contents = JsonConvert.DeserializeObject<List<StateEntity>>(content);
            }
            catch (Exception ex)
            {

                log.Info($"An exception accured while deserializing state entity {ex.Message}");
            }
            foreach (var status in contents)
            {
                log.Info($"Adding  {status.UrlName} to queue");
                urlValues.Add(status);
                
            }
        }

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
