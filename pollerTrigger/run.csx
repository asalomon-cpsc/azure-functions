using System;
using System.Net;

public static async Task Run(TimerInfo myTimer, TraceWriter log)
{
    log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

    using (var client = new HttpClient())
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.Timeout = TimeSpan.FromSeconds(10);

        var response = await client.GetAsync(Environment.GetEnvironmentVariable("POLLER_TRIGGER_URL"));

        log.Info($"response code: {response.StatusCode.ToString()}");
        
    }
}
