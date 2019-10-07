#r "Microsoft.WindowsAzure.Storage"
using Microsoft.WindowsAzure.Storage.Table;
using System.Net;

//this function will get all the urls if no parameters are passed
public static async Task<HttpResponseMessage> Run(IQueryable<StateEntity> statusTable, HttpRequestMessage req, TraceWriter log)
{
    log.Info("C# HTTP trigger function processed a request.");

    //parse query parameter
    string name = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "urlName", true) == 0)
       .Value;


    return await req.CreateResponse(HttpStatusCode.OK, statusTable.Where(p => p.PartitionKey == "statuses" && p.Status != "OK").ToList());


}

public class StateEntity : TableEntity
{

    public string UrlName { get; set; }
    public string Url { get; set; }
    public string Status { get; set; }
    public string Description { get; set; }
    public DateTime Date { get; set; }
}
