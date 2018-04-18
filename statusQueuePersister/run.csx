#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using System;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.Azure;

private const string PARTITION_KEY = "statuses";
public static void Run(CloudQueueMessage myQueueItem, 
    DateTimeOffset expirationTime, 
    DateTimeOffset insertionTime, 
    DateTimeOffset nextVisibleTime,
    string queueTrigger,
    string id,
    string popReceipt,
    int dequeueCount,
    CloudTable outTable,
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
        state.ETag = "*";
           TableOperation deleteOperation = TableOperation.Delete(state);
            state.RowKey = state.UrlName;
             log.Info("The url could not be retrieved.");
             TableOperation insertOperation = TableOperation.InsertOrReplace(state);
             log.Info("The url is being inserted...");
            
            try{
            TableResult insertResult = outTable.Execute(insertOperation);
             if (insertResult.Result != null)
                {
                log.Info("inserted or replaced successfully");
                }
            else
                {
                log.Info("Could not add url to table");

                }
            }
                catch(Exception ex){
                    log.Info(ex.Message);
                }

}          
        
public class StateEntity:TableEntity
{
    public StateEntity():base(){
        this.Date = DateTime.Now;
         this.PartitionKey = PARTITION_KEY;
    }
    public StateEntity(string url,string urlName, string status, string description, string action){
        this.PartitionKey = PARTITION_KEY;
        this.RowKey = $"{urlName}";
        this.UrlName = urlName;
        this.Url = url;
        this.Status = status;
        this.Action = action;
        this.Description = description;
        this.Date = DateTime.Now;
    }
    public string UrlName {get;set;}
    public string Url {get;set;}
    public string Status {get;set;}
    public string Description {get;set;}
    public string Action {get;set;}
    public DateTime Date {get;set;}
}

