# StatusPoller_http

This function is the entry point to the polling system. This function accepts an empty HTTP request then triggers the polling program.

# Sequence of Events

1. The program reads a list of url by calling the ***`statusUrlListReader`*** function.

2. Then for each url retrieved in ***`urlTable`*** , the poll is triggered for the url/resource.
3. Then the resultsets are aggregated and sent to two queues for additional processing.
4. Then the ***`statusQueuePersister`*** grabs the message in the ***`status-states-queue`*** queue to save it in the azure storage table named ***`statusesTable`*** . The ***`statusHistoryQueuePersiter`*** also grabs the message from the ***`status-history-queue`*** to save them in the azure storage table named ***`statusHistoryTable`*** .

The functions are able to pass data downstream seamlessly thanks to azure's bindings mechanism. each function has a bindings file in `Json` format.

# Bindings

The binding file is the main driver behind the portal integration services, the files can be generated inthe portal or manualy by understanding the [azure triggers and bindings concept](https://docs.microsoft.com/en-us/azure/azure-functions/functions-triggers-bindings):
![I'm an inline-style link with title](https://docs.microsoft.com/en-us/azure/azure-functions/media/functions-integrate-storage-queue-output-binding/function-add-queue-storage-output-binding.png)


```JSON
{
  "bindings": [
    {
      "authLevel": "function",
      "name": "req",
      "type": "httpTrigger",
      "direction": "in",
      "methods": [
        "get",
        "post"
      ]
    },
    {
      "type": "queue",
      "name": "statesValues",
      "queueName": "status-states-queue",
      "connection": "AzureWebJobsStorage",
      "direction": "out"
    },
    {
      "type": "queue",
      "name": "historyValues",
      "queueName": "status-history-queue",
      "connection": "AzureWebJobsStorage",
      "direction": "out"
    }
  ],
  "disabled": false
}
```