#r "Newtonsoft.Json"
#r "SendGrid"
using SendGrid.Helpers.Mail;
using Newtonsoft.Json;
using System.Net;
using System;
using System.Text;
using System.Net.Mail;

//this function runs the poller program and returns the site status/states
//the poller program will send states to the status-state-queue
//then the statusQueuePersister function who is subscribed to the status-state-queue will consume the the message
// in order to persist it in the storage table
public static async Task<List<State>> Run(HttpRequestMessage req, ICollector<State> historyValues, ICollector<State> statesValues, TraceWriter log, Mail message)
{
    log.Info("C# HTTP trigger function processed a request.");
    var urls = new Dictionary<string, string>();
    HttpResponseMessage response;
    var functionUrl = Environment.GetEnvironmentVariable("STATUS_URL_LIST_PROXY");
    log.Info($"func url {functionUrl}");
    StringBuilder text = new StringBuilder();
    using (var client = new HttpClient())
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.Timeout = TimeSpan.FromSeconds(10);

        response = await client.GetAsync(functionUrl);

        log.Info($"response code: {response.StatusCode.ToString()}");
        string content = await response.Content.ReadAsStringAsync();
        log.Info($"content is: {content}");
        List<StateEntity> contents = JsonConvert.DeserializeObject<List<StateEntity>>(content);
        
        foreach (var status in contents)
        {
            log.Info($"type is: {status.GetType().Name}");
            log.Info(status.UrlName);
            log.Info(status.Url);
            urls.Add(status.UrlName, status.Url);
        }


    }
    List<State> pollValue = await RunPoller(log, urls);
    
    foreach (var v in pollValue)
    {
       historyValues.Add(v);
       statesValues.Add(v);
       text.AppendLine($"Url : {v.Url}\n");
       text.AppendLine($"Poll Status : {v.Status}\n");
       text.AppendLine($"Status Description : {v.Description}\n");
       text.AppendLine("--------------");
    }
    if(text.Length>1){
      message =  SendEmail(text.ToString(),message);
    
    }

    return pollValue;

}

public static Mail SendEmail(string body,Mail message){

     message = new Mail
    {        
        Subject = "Azure news"          
    };

    var personalization = new Personalization();
    personalization.AddTo(new Email("asalomon@cpsc.gov"));   

    Content content = new Content
    {
        Type = "text/plain",
        Value = body
    };
    message.AddContent(content);
    message.AddPersonalization(personalization);
    return message;
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
            var pollingTask = await Poll(resource.Key, resource.Value);
            log.Info($"Poll Result for {cpscName}---{resource.Value}  is {pollingTask.Description} ");
            sList.Add(pollingTask);
        }
        catch (Exception ex)
        {
            log.Info(ex.Message);
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

public static async Task<State> Poll(string UrlName, string Url)
{
    var _state = new State();
    HttpResponseMessage response;
    using (var handler = new HttpClientHandler { UseDefaultCredentials = true, ClientCertificateOptions = ClientCertificateOption.Automatic })
    using (var client = new HttpClient(handler))
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", "azure_cpsc");
        client.Timeout = TimeSpan.FromSeconds(10);

        response = await client.GetAsync(Url, HttpCompletionOption.ResponseContentRead);

        if (response.StatusCode != HttpStatusCode.BadGateway && response.StatusCode != HttpStatusCode.RequestTimeout)
        {
            string content = await response.Content.ReadAsStringAsync();
            content = CreateStatusMessageForFalsePositives(content);
            _state = string.IsNullOrEmpty(content) ? new State()
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
            _state = new State()
            {
                Status = response.StatusCode.ToString(),
                Description = "Web Page Is Not Responding, Requests Are Timing Out",

                Url = Url
            };
        }
    }
    return _state;
}

public static string CreateStatusMessageForFalsePositives(string content)
{
    return content.Contains("Under Maintenance")
             ? "Website is reponding but with error pages, please check servers. app pools, web server"
             : string.Empty;
}
