#r "Microsoft.WindowsAzure.Storage"
#r "SendGrid"
#r "Newtonsoft.Json"
using Microsoft.WindowsAzure;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Queue;
using SendGrid.Helpers.Mail;
using System;
using System.Text;
public static void Run(CloudQueueMessage myQueueItem, 
    DateTimeOffset expirationTime, 
    DateTimeOffset insertionTime, 
    DateTimeOffset nextVisibleTime,
    string queueTrigger,
    string id,
    string popReceipt,
    int dequeueCount, TraceWriter log, out Mail message)
{
    log.Info($"C# Queue trigger function processed: {myQueueItem}");
        List<StateEntity> states =JsonConvert.DeserializeObject<List<StateEntity>>(myQueueItem.AsString);
        message = new Mail();
    string dashboardUrl = "Location not loaded";
    string dashboard_url_env = Environment.GetEnvironmentVariable("dashboard_url");
    if(!string.IsNullOrEmpty(dashboard_url_env)){ 
        dashboardUrl = dashboard_url_env;
    }
    StringBuilder text = new StringBuilder();
       text.AppendLine($"<h3>The following resources had or have a status change: </h3>");
       foreach(var state in states){
        log.Info(state.UrlName);
        log.Info(state.Url);
        log.Info(state.Date.ToString());
        log.Info(state.Description);
        text.AppendLine($"<p>UrlName : <strong>{state.UrlName}</strong></p>");
        text.AppendLine($"<p>Url : {state.Url}</P>");
        text.AppendLine($"<p>Poll Status : {state.Status}</p>");
        text.AppendLine($"<p>Status Description : {state.Description}</p>");
        text.AppendLine("<hr/>");
        text.AppendLine("---------------");
        text.AppendLine($"<p>To view the dashboard of for all sites click here: <a href='{dashboardUrl}'>{dashboardUrl}</a><p>");
       }
      
     var personalization = new Personalization();
     Environment.GetEnvironmentVariable("EMAIL_RECIPIENTS")
                        .Split(';')
                        .ToList()
                        .ForEach(e=>
                        {
                            log.Info(e);
                         personalization.AddTo(new Email(e));   
                         
                        });

    Content content = new Content
    
    {
        Type = "text/html",
        Value = text.ToString()
    };
    message.AddContent(content);
    message.AddPersonalization(personalization);
   

         
}

public class StateEntity
{
    public StateEntity(){
        this.Date = DateTime.Now;
    }
    public StateEntity(string url,string urlName, string status, string description){
        
        this.UrlName = urlName;
        this.Url = url;
        this.Status = status;
        this.Description = description;
        this.Date = DateTime.Now;
    }
    public string UrlName {get;set;}
    public string Url {get;set;}
    public string Status {get;set;}
    public string Description {get;set;}
    public DateTime Date {get;set;}
}

