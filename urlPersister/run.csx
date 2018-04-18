#r "Newtonsoft.Json"
using Newtonsoft.Json;
using System.Net;
using System;
public static async Task<string> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info("C# HTTP trigger function processed a request.");
        List<UrlEntity> urlList=new List<UrlEntity>();
     // Get request body
        dynamic urls = await req.Content.ReadAsAsync<object>();
        foreach(var data in urls){
         var url = new UrlEntity();

        url.UrlName = data?.urlName;
        url.Url = data?.url;
        url.Date = DateTime.Now;
        url.Action = string.IsNullOrEmpty(url.Url)?"DELETE":req.Method.ToString();
        urlList.Add(url);
        log.Info(url.UrlName);
        log.Info(url.Action);
        }
       
    return JsonConvert.SerializeObject(urlList);
}


public struct UrlEntity
{
    public string Action {get;set;}
    public string UrlName {get;set;}
    public string Url {get;set;}
    public DateTime Date {get;set;}
}
