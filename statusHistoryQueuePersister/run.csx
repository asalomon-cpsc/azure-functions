#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using System;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;

public static void Run(CloudQueueMessage myQueueItem, 
    DateTimeOffset expirationTime, 
    DateTimeOffset insertionTime, 
    DateTimeOffset nextVisibleTime,
    string queueTrigger,
    string id,
    string popReceipt,
    int dequeueCount,
    ICollector<StateEntity> statusHistoryTable,
    TraceWriter log)
{
    log.Info($"C# Queue trigger function processed: {myQueueItem.AsString}\n" +
        $"queueTrigger={queueTrigger}\n" +
        $"expirationTime={expirationTime}\n" +
        $"insertionTime={insertionTime}\n" +
        $"nextVisibleTime={nextVisibleTime}\n" +
        $"id={id}\n" +
        $"popReceipt={popReceipt}\n" + 
        $"dequeueCount={dequeueCount}");

        StateEntity state =JsonConvert.DeserializeObject<StateEntity>(myQueueItem.AsString);
        log.Info(state.PartitionKey);
        log.Info(state.RowKey);
        log.Info(state.UrlName);
        log.Info(state.Url);
        log.Info(state.Date.ToString());
        log.Info(state.Description);
        statusHistoryTable.Add(new StateEntity(state.Url,state.UrlName, 
                                    state.Status,
                                    state.Description));
}

public class StateEntity:TableEntity
{
    public StateEntity():base(){
        this.PartitionKey = "statuses";
        this.RowKey = Guid.NewGuid().ToString();
    }
    public StateEntity(string url,string urlName, string status, string description){
        this.PartitionKey = "statuses";
        this.RowKey = $"{urlName}{DateTime.Now.ToString("o")}";
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