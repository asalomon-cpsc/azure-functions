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
    //Collector<UrlEntity> statusTable,
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

         List<UrlEntity> urls =JsonConvert.DeserializeObject<List<UrlEntity>>(myQueueItem.AsString);
         TableResult result = null;
        foreach(var d in urls){
             UrlEntity url = new UrlEntity(d.Url,d.UrlName,d.Action);
            log.Info($"Url dequeued: {d.UrlName}");
           
 
                if (url.Action=="POST"|| url.Action=="PUT")
                    {
                       TableOperation postOrPut = TableOperation.InsertOrReplace(url);
                        TableResult postPutResult = outTable.Execute(postOrPut);
                    }
                if(url.Action=="DELETE")
                    {
                        url.ETag="*";//required for delete operations
                        TableOperation delete = TableOperation.Delete(url);
                        TableResult deleteResult =outTable.Execute(delete);
                    }



          }
         
}

public class UrlEntity:TableEntity
{
    public UrlEntity(string url,string urlName,string action){
        this.PartitionKey = "urls";
        this.RowKey = urlName;//;
        this.UrlName = urlName;
        this.Url = url;
        this.Action = action;
        this.Date = DateTime.Now;

    }
    public string UrlName {get;set;}
    public string Url {get;set;}
    public DateTime Date {get;set;}
    public string Action {get;set;}
}