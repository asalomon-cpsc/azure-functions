#r "Microsoft.WindowsAzure.Storage"
using System.Net;
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
public static async Task<HttpResponseMessage> Run(HttpRequestMessage req,ICollector<string> triggerMessage, TraceWriter log)
{
    log.Info("C# HTTP poller trigger function processed a request.");
    triggerMessage.Add("Start");

    return req.CreateResponse(HttpStatusCode.OK, "Polling request has been initiated");
}
