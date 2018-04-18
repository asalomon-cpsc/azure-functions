#r "Microsoft.WindowsAzure.Storage"
using Microsoft.WindowsAzure.Storage.Table;
using System.Net;

//this function will get all the urls if no parameters are passed
public static async Task<HttpResponseMessage> Run(IQueryable<UrlEntity> urlTable,HttpRequestMessage req, TraceWriter log)
{
    log.Info("C# HTTP trigger function processed a request.");
    
    // parse query parameter
    string name = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "urlName", true) == 0)
        .Value;
     log.Info("Name is" + name);

    return string.IsNullOrWhiteSpace(name)
        ? req.CreateResponse(HttpStatusCode.OK, urlTable.Where(p => p.PartitionKey == "urls").ToList())
        : req.CreateResponse(HttpStatusCode.OK, urlTable.Where(p => p.PartitionKey == "urls" && p.RowKey==name).ToList().FirstOrDefault());
}

public class UrlEntity:TableEntity
{
   
    public string UrlName {get;set;}
    public string Url {get;set;}
    public DateTime Date {get;set;}
}
