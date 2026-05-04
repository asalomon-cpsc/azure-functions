# Site Status Notification

Provides simple HTTP-polling-based monitoring for public websites or HTTP services.
Each configured URL is polled on a schedule (or on demand). If one or more sites return a non-OK status, an email alert is sent with a summary of failures and a link to the status dashboard.

## Stack

| Data Store | Messaging | Runtime | Email Delivery |
|---|---|---|---|
| [Azure Storage Tables](https://azure.microsoft.com/services/storage/tables/) | [Azure Queue Storage](https://azure.microsoft.com/services/storage/queues/) | .NET 8 Isolated Worker (Azure Functions v4) | [Azure Communication Services Email](https://azure.microsoft.com/services/communication-services/) |

---

## Architecture

```mermaid
flowchart TD
    Timer([⏱ TimerTrigger\npollerTrigger\nevery 60 min])
    HTTP([🌐 HttpTrigger\nhttpPollerTrigger\nGET/POST])

    Timer -->|ScheduleNewOrchestrationInstanceAsync| Orch
    HTTP -->|ScheduleNewOrchestrationInstanceAsync| Orch

    subgraph Orchestration ["Durable Orchestration (StatusPollerOrchestrator)"]
        Orch[Orchestrator\nStatusPollerOrchestrator]
        GetUrls[Activity\nGetUrlListActivity\nreads urlTable]
        Poll1[Activity\nPollUrlActivity\nURL 1]
        Poll2[Activity\nPollUrlActivity\nURL 2]
        PollN[Activity\nPollUrlActivity\nURL N]
        Notify[Activity\nSendNotificationActivity\nACS Email]

        Orch -->|CallActivityAsync| GetUrls
        GetUrls -->|List of URLs| Orch
        Orch -->|fan-out parallel| Poll1
        Orch -->|fan-out parallel| Poll2
        Orch -->|fan-out parallel| PollN
        Poll1 -->|PollResult| Orch
        Poll2 -->|PollResult| Orch
        PollN -->|PollResult| Orch
        Orch -->|failures only| Notify
    end

    Poll1 -->|UpsertEntityAsync| statusTable[(statusTable\ncurrent state)]
    Poll1 -->|UpsertEntityAsync| historyTable[(statusHistoryTable\nhistory)]
    Poll2 -->|UpsertEntityAsync| statusTable
    Poll2 -->|UpsertEntityAsync| historyTable
    PollN -->|UpsertEntityAsync| statusTable
    PollN -->|UpsertEntityAsync| historyTable
    Notify -->|EmailClient.SendAsync| ACS([ACS Email])

    subgraph URL Management
        UrlHTTP([🌐 HttpTrigger\nurlPersister\nPOST/PUT/DELETE])
        UrlQueue[QueueTrigger\nurlQueuePersister]
        urlTable[(urlTable)]

        UrlHTTP -->|url-management-queue| UrlQueue
        UrlQueue -->|UpsertEntityAsync\nDeleteEntityAsync| urlTable
    end

    GetUrls -->|QueryAsync| urlTable

    subgraph Read Endpoints
        R1([🌐 statusUrlListReader\nGET])
        R2([🌐 statusSiteStateReader\nGET/POST/OPTIONS])
        R3([🌐 statusHistoryReader\nGET/POST])
    end

    R1 -->|QueryAsync| urlTable
    R2 -->|QueryAsync| statusTable
    R3 -->|QueryAsync| historyTable
```

---

## Function Reference

| Function | Trigger | Role |
|---|---|---|
| `pollerTrigger` | Timer (every 60 min) | Starts a poll orchestration instance |
| `httpPollerTrigger` | HTTP GET/POST | Manually starts a poll orchestration instance |
| `StatusPollerOrchestrator` | Durable Orchestrator | Fan-out/fan-in: fetches URLs, polls all in parallel, notifies on failure |
| `GetUrlListActivity` | Durable Activity | Reads `urlTable` directly — no internal HTTP call |
| `PollUrlActivity` | Durable Activity | Polls one URL; writes current state + history row; returns `PollResult` |
| `SendNotificationActivity` | Durable Activity | Sends ACS Email alert for all failing URLs |
| `urlPersister` | HTTP POST/PUT/DELETE | Accepts URL add/update/delete requests, enqueues to `url-management-queue` |
| `urlQueuePersister` | Queue (`url-management-queue`) | Persists URL changes to `urlTable` |
| `statusUrlListReader` | HTTP GET | Returns configured URL list from `urlTable` |
| `statusSiteStateReader` | HTTP GET/POST/OPTIONS | Returns current poll status from `statusTable` |
| `statusHistoryReader` | HTTP GET/POST | Returns poll history from `statusHistoryTable` |

---

## Required App Settings

| Setting | Description |
|---|---|
| `AzureWebJobsStorage` | Storage account connection string |
| `FUNCTIONS_WORKER_RUNTIME` | Must be `dotnet-isolated` |
| `EMAIL_SENDER` | ACS sender address (e.g. `donotreply@xxxxxxxx.azurecomm.net`) |
| `EMAIL_SUBJECT` | Email subject line |
| `EMAIL_RECIPIENTS` | Semicolon-separated recipient list |
| `ACS_ENDPOINT` | ACS resource URI — used with managed identity (production) |
| `ACS_CONNECTION_STRING` | ACS connection string — local dev fallback |
| `dashboard_url` | Status dashboard URL included in alert emails |

See `local.settings.json.template` for the full list. Copy it to `local.settings.json` (gitignored) and fill in values before running locally.

