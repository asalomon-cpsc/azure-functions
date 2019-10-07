#r "Microsoft.WindowsAzure.Storage"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

public static void Run(TimerInfo myTimer, ICollector<string> triggerMessage, TraceWriter log)
{
    log.Info($"C# Timer trigger function executed at: {DateTime.Now}");
    triggerMessage.Add("Start");
    
}
