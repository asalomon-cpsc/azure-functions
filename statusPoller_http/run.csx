#r "Newtonsoft.Json"
using Newtonsoft.Json;
using System.Net;
using System;
using System.Net.Mail;

//this function runs the poller program and returns the site status/states
//the poller program will send states to the status-state-queue
//then the statusQueuePersister function who is subscribed to the status-state-queue will consume the the message
// in order to persist it in the storage table
public static async Task<List<State>> Run(HttpRequestMessage req, ICollector<State> historyValues, ICollector<State> statesValues, ICollector<string> errorValues, TraceWriter log)
{
    log.Info("C# HTTP trigger function processed a request.");
    var urls = new Dictionary<string, string>();
    List<State> errors = new List<State>();
    HttpResponseMessage response;
    var functionUrl = Environment.GetEnvironmentVariable("STATUS_URL_LIST_PROXY");
    log.Info($"func url {functionUrl}");
    using (var client = new HttpClient())
    {
        client.DefaultRequestHeaders.Accept.Clear();

        response = await client.GetAsync(functionUrl);

        log.Info($"response code: {response.StatusCode.ToString()}");
        string content = await response.Content.ReadAsStringAsync();
        log.Info($"content is: {content}");
        List<StateEntity> contents = JsonConvert.DeserializeObject<List<StateEntity>>(content);

        foreach (var status in contents)
        {
            urls.Add(status.UrlName, status.Url);
        }


    }
    List<State> pollValue = await RunPoller(log, urls);
    foreach (var v in pollValue)
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

    return pollValue;

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
static async Task<List<State>> RunPoller(TraceWriter log, IDictionary<string, string> urls)
{

    string cpscName = "CPSC Web Site";

    HashSet<State> sList = new HashSet<State>();

    foreach (var resource in urls)
    {
        log.Info($"Currently Polling:{cpscName}---{resource.Value} ");
        try
        {
            var pollingTask = await Poll(resource.Key, resource.Value,log);
            log.Info($"Poll Result for {cpscName}---{resource.Value}  is {pollingTask.Description} ");
            sList.Add(pollingTask);
        }
        catch (Exception ex)
        {
            log.Info(ex.Message);
            sList.Add(new State(){
                UrlName = resource.Key,
                Url = resource.Value,
                Status = "NotFound",
                Description = ex.Message
            });
        }
    }

    return sList.ToList();
}

public struct State
{
    public string UrlName { get; set; }
    public string Url { get; set; }
    public string Status { get; set; }
    public string Description { get; set; }

}

public static async Task<State> Poll(string UrlName, string Url,TraceWriter log)
{
    HttpResponseMessage response;
    using (var handler = new HttpClientHandler { ClientCertificateOptions = ClientCertificateOption.Automatic })
    using (var client = new HttpClient(handler))
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", "azure_cpsc");
        client.Timeout = TimeSpan.FromSeconds(60);

        response = await client.GetAsync(Url,HttpCompletionOption.ResponseContentRead);
        
        if (response.StatusCode != HttpStatusCode.BadGateway && response.StatusCode != HttpStatusCode.RequestTimeout)
        {
            log.Info("poll result entered 1st condition state");
            log.Info($"result for {UrlName} is {response.StatusCode}");
            if(response.StatusCode == HttpStatusCode.GatewayTimeout){
                return  new State()
            {
                Status = response.StatusCode.ToString(),
                Url = Url,
                UrlName = UrlName,
                Description = "More info here :  https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/504"
            };
            }
            string content = await response.Content.ReadAsStringAsync();
            content = CreateStatusMessageForFalsePositives(content);
            return string.IsNullOrEmpty(content) ? new State()
            {
                Status = response.StatusCode.ToString(),
                Url = Url,
                UrlName = UrlName,
                Description = response.ReasonPhrase
            } :
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
                Status = response.StatusCode.ToString(),
                Description = "Web Page Is Not Responding, Requests Are Timing Out",
                UrlName = UrlName,
                Url = Url
            };
       }
    }
    return  new State()
            {
                Status = "NA",
                Url = Url,
                UrlName = UrlName,
    
            };
}

public static string CreateStatusMessageForFalsePositives(string content)
{
    return content.Contains("Under Maintenance")
             ? "Website is reponding but with error pages, please check servers. app pools, web server"
             : string.Empty;
}
