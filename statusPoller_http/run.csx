#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"
using Newtonsoft.Json;
using System.Net;
using System;
using System.Net.Mail;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;


public static void Run(
    CloudQueueMessage myQueueItem, 
    DateTimeOffset expirationTime, 
    DateTimeOffset insertionTime, 
    DateTimeOffset nextVisibleTime,
    string queueTrigger,
    string id,
    string popReceipt,
    int dequeueCount, 
    ICollector<State> historyValues, 
    ICollector<State> statesValues, 
    ICollector<string> errorValues, 
    TraceWriter log)
{
    log.Info("C# HTTP trigger function processed a request.");
    
    List<State> errors = new List<State>();
     StateEntity state =JsonConvert.DeserializeObject<StateEntity>(myQueueItem.AsString);
     state.Etag = "*";
    //List<State> pollValue = await RunPoller(log, urls);
    foreach (var v in RunPoller(log,state))
    {
       
        historyValues.Add(v);
        statesValues.Add(v);
         if(v.Status!="OK"){
            errors.Add(v);
            log.Info($"added {v.UrlName} with a status of {v.Status} to the errors queue");
        }
    }
    if(errors.Any()){
        errorValues.Add(JsonConvert.SerializeObject(errors));
    }

    //return pollValue;

}
public class StateEntity
{
    public string UrlName { get; set; }
    public string Url { get; set; }
    public DateTime Date { get; set; }
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTime TimeStamp { get; set; }
    public string Etag { get; set; }
}
static  IEnumerable<State> RunPoller(TraceWriter log, StateEntity url)
{
    string cpscName = "CPSC Web Site";
    log.Info($"Currently Polling:{cpscName}---{url.UrlName} ");
    State pollingTask = default(State);
    try{
        pollingTask =  Poll(url.UrlName, url.Url,log);
    }
    catch (System.AggregateException ex)
    {
                System.Text.StringBuilder e = new  System.Text.StringBuilder();
                ex.Flatten().InnerExceptions.ToList().ForEach(exception =>
                {
                   e.Append(ex.Message);

                });
            pollingTask = new State()
            {
                Status = "500",
                Description = e.ToString(),
                UrlName = url.UrlName,
                Url = url.Url
            };

     }
    
    log.Info($"Poll Result for {cpscName}---{url.UrlName}  is {pollingTask.Description} ");
    yield return pollingTask;

}

public struct State
{
    public string UrlName { get; set; }
    public string Url { get; set; }
    public string Status { get; set; }
    public string Description { get; set; }

}

public static State Poll(string UrlName, string Url,TraceWriter log)
{
    Task<HttpResponseMessage> response;
    using (var handler = new HttpClientHandler { ClientCertificateOptions = ClientCertificateOption.Automatic })
    using (var client = new HttpClient(handler))
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", "azure_cpsc");
        client.Timeout = TimeSpan.FromSeconds(10);
        response = client.GetAsync(Url,HttpCompletionOption.ResponseHeadersRead);
        
        if (response.Result.StatusCode != HttpStatusCode.BadGateway && response.Result.StatusCode != HttpStatusCode.RequestTimeout)
         {
            log.Info("poll result entered 1st condition state");
            log.Info($"result for {UrlName} is {response.Result.StatusCode}");
            if(response.Result.StatusCode == HttpStatusCode.GatewayTimeout){
                return  new State()
                {
                    Status = response.Result.StatusCode.ToString(),
                    Url = Url,
                    UrlName = UrlName,
                    Description = "More info here :  https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/504"
                };
            }
            string content = response.Result.ReasonPhrase;// response.Content.ReadAsStringAsync();
            content = CreateStatusMessageForFalsePositives(content);
            return string.IsNullOrEmpty(content) ?  new State()      
            {
                Status = response.Result.StatusCode.ToString(),
                Url = Url,
                UrlName = UrlName,
                Description = response.Result.ReasonPhrase
            }:
             new State()
            {
                Status = content,
                 Url = Url,
                 UrlName = UrlName
            };
        }
        
       else
        {
            log.Info("poll result entered 2nd condition state");
            return new State()
            {
                Status = response.Result.StatusCode.ToString(),
                Description = "Web Page Is Not Responding, Requests Are Timing Out",
                UrlName = UrlName,
                Url = Url
            };
       }
    }
    
}

public static string CreateStatusMessageForFalsePositives(string content)
{
    return content.Contains("Under Maintenance")
             ? "Website is reponding but with error pages, please check servers. app pools, web server"
             : string.Empty;
}
