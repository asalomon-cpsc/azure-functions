#r "Microsoft.WindowsAzure.Storage"
using System;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

public static Run(TimerInfo myTimer, Collector<string> triggerMessage, TraceWriter log)
{
    log.Info($"C# Timer trigger function executed at: {DateTime.Now}");
    triggerMessage.Add("Start");
    
}
