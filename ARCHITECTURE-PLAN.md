# Better Architecture Plan — Cost-Neutral Redesign

## Cost Baseline (Current)

| Resource | Current Usage | Monthly Cost |
|---|---|---|
| Azure Functions Consumption | ~hourly timer + small fan-out | ~$0 (within 1M exec free/month) |
| Azure Storage (queues + tables) | 6 queues, 3 tables | ~$0 (pennies at this scale) |
| SendGrid | Free tier (exhausted, unusable) | $0 |
| **Total** | | **~$0** |

## Cost After Redesign

| Resource | New Usage | Monthly Cost |
|---|---|---|
| Azure Functions Consumption | Same consumption plan, ~same execution count | ~$0 |
| Azure Storage | 1 explicit queue + 3 tables + Durable internal storage | ~$0 |
| ACS Email | 100 emails/day free tier | $0 |
| **Total** | | **~$0** |

> **Durable Functions overhead at this scale:** For hourly polling of 50 URLs, Durable adds ~1,200 extra executions/day (~37K/month). The free tier is 1 million/month. No cost impact.

---

## Current Architecture Problems

### 1. Circular HTTP Dependency
`statusUrlTaskAssigner` makes an HTTP call back to `statusUrlListReader` (another function in the same app) via the `STATUS_URL_LIST_PROXY` app setting just to read a storage table. This means:
- One internal function failure cascades into a full polling outage
- Adds an unnecessary network round-trip inside the same process boundary
- Requires `STATUS_URL_LIST_PROXY` to be kept in sync with the function app URL

### 2. Six-Queue Pipeline for Fan-Out/Fan-In
The current polling pipeline uses 6 explicit Storage Queues and 8 queue-processing functions to do what is a standard fan-out/fan-in pattern:

```
[Timer] → status-initiator-queue
  → statusUrlTaskAssigner (HTTP call to get URLs) → status-url-queue (one message per URL)
    → statusPoller_http (polls URL)
      → status-states-queue   → statusQueuePersister
      → status-history-queue  → statusHistoryQueuePersister
      → status-notifications-queue → emailAlerter_csharp
```

Each queue is an independent failure domain with no visibility into partial failures across the chain.

### 3. Socket Exhaustion Risk
`statusPoller_http` creates `new HttpClient(new HttpClientHandler())` inside a `using` block on every invocation. When polling many URLs in rapid succession, this exhausts the local socket pool (TIME_WAIT accumulation).

### 4. Silent Pipeline Failures
If any queue-processing function fails repeatedly, messages land in Azure Storage poison queues with no alerting. The polling cycle silently drops data.

### 5. Two Data Bugs
- `statusHistoryReader` queries `RowKey == urlName` but the writer stores row keys as `urlName + timestamp` — per-URL history queries always return empty.
- `statusSiteStateReader` parses the `urlName` query parameter but never uses it, always returning the full state list.

---

## Proposed Architecture

Replace the entire polling pipeline with **Azure Durable Functions** (fan-out/fan-in pattern). The HTTP management and read endpoints are unchanged.

### New Pipeline

```
[Timer/HTTP]
  └─► StatusPollerOrchestrator (Durable Orchestrator)
        │
        ├─ reads urlTable directly (no HTTP hop, no STATUS_URL_LIST_PROXY)
        │
        ├─► PollUrlActivity (one per URL, all run in parallel)
        │     ├─ polls URL via IHttpClientFactory (shared connection pool)
        │     ├─ writes current state → statusTable
        │     └─ writes history row  → statusHistoryTable
        │
        └─► SendNotificationActivity (only if any URL returned non-OK)
              └─ sends ACS Email with failure summary
```

### Function Inventory

| Function | Status | Notes |
|---|---|---|
| `pollerTrigger` | **Modified** | `TimerTrigger` → calls `DurableClient.StartNewAsync` instead of writing to queue |
| `httpPollerTrigger` | **Modified** | `HttpTrigger` → calls `DurableClient.StartNewAsync` instead of writing to queue |
| `StatusPollerOrchestrator` | **New** | Durable Orchestrator; reads `urlTable` directly, fans out activities |
| `PollUrlActivity` | **New** | Durable Activity; polls one URL, writes state + history |
| `SendNotificationActivity` | **New** | Durable Activity; sends ACS Email via injected `EmailClient` |
| `statusUrlListReader` | **Unchanged** | HTTP read of `urlTable` |
| `statusSiteStateReader` | **Bug fix** | Fix: apply `urlName` filter when provided |
| `statusHistoryReader` | **Bug fix** | Fix: query `RowKey ge urlName` (prefix scan) instead of exact match |
| `urlPersister` | **Unchanged** | HTTP write to `url-management-queue` |
| `urlQueuePersister` | **Unchanged** | Consumes `url-management-queue`, writes to `urlTable` |

### Functions Eliminated (10 → 9, net -3 after new additions)

| Eliminated | Replaced By |
|---|---|
| `statusUrlTaskAssigner` | `StatusPollerOrchestrator` (reads table directly) |
| `statusPoller_http` | `PollUrlActivity` |
| `statusQueuePersister` | `PollUrlActivity` (writes table inline) |
| `statusHistoryQueuePersister` | `PollUrlActivity` (writes table inline) |
| `emailAlerter_csharp` | `SendNotificationActivity` |

### Queues Eliminated (6 → 1)

| Eliminated Queue | Why It Existed |
|---|---|
| `status-initiator-queue` | Trigger signal between timer and task assigner — gone; orchestrator starts directly |
| `status-url-queue` | Fan-out of one message per URL — gone; Durable activities handle this internally |
| `status-states-queue` | Current state persistence handoff — gone; activity writes inline |
| `status-history-queue` | History persistence handoff — gone; activity writes inline |
| `status-notifications-queue` | Email trigger — gone; orchestrator calls notification activity directly |

**Only queue retained:** `url-management-queue` (URL add/update/delete pipeline — unchanged)

---

## Implementation Plan

### Phase 1 — Project Scaffold
*(Identical to the upgrade plan — .NET 8 Isolated Worker project structure)*

Add one additional NuGet package:
```
Microsoft.Azure.Functions.Worker.Extensions.DurableTask 1.3.0+
```

Register `IHttpClientFactory` and `EmailClient` in `Program.cs`:
```csharp
builder.Services.AddHttpClient("poller", c => {
    c.Timeout = TimeSpan.FromSeconds(60);
    c.DefaultRequestHeaders.Add("User-Agent", "azure_cpsc");
});
builder.Services.AddSingleton(new EmailClient(
    new Uri(Environment.GetEnvironmentVariable("ACS_ENDPOINT")!),
    new DefaultAzureCredential()));
```

Remove `STATUS_URL_LIST_PROXY` from app settings — it is no longer needed.

### Phase 2 — Durable Orchestration

**`StatusPollerOrchestrator.cs`** (Orchestrator)
```csharp
[Function(nameof(StatusPollerOrchestrator))]
public static async Task RunOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    // 1. Read URL list directly from urlTable
    var urls = await context.CallActivityAsync<List<UrlEntity>>(
        nameof(GetUrlListActivity), null);

    // 2. Fan-out: poll all URLs in parallel
    var tasks = urls.Select(url =>
        context.CallActivityAsync<PollResult>(nameof(PollUrlActivity), url,
            new TaskOptions { Retry = new RetryPolicy(3, TimeSpan.FromSeconds(10)) }));
    var results = await Task.WhenAll(tasks);

    // 3. Fan-in: notify only on failures
    var failures = results.Where(r => r.Status != "OK").ToList();
    if (failures.Any())
        await context.CallActivityAsync(nameof(SendNotificationActivity), failures);
}
```

**`PollUrlActivity.cs`** (Activity)
- Receives a `UrlEntity` input
- Uses injected `IHttpClientFactory` (named client `"poller"`)
- Writes current state row to `statusTable` via `TableClient.UpsertEntityAsync`
- Writes history row to `statusHistoryTable` with `RowKey = urlName + "_" + timestamp`
- Returns `PollResult { UrlName, Status, Description }`

**`SendNotificationActivity.cs`** (Activity)
- Receives `List<PollResult>` from orchestrator
- Builds same HTML email body as the existing `emailAlerter_csharp`
- Sends via `EmailClient.SendAsync()` using `EMAIL_SENDER`, `EMAIL_RECIPIENTS`, `EMAIL_SUBJECT`, `dashboard_url` app settings

**`pollerTrigger.cs`** and **`httpPollerTrigger.cs`** (modified)
```csharp
await durableClient.ScheduleNewOrchestrationInstanceAsync(
    nameof(StatusPollerOrchestrator));
```

### Phase 3 — HTTP Endpoints (Bugs Fixed)

**`statusHistoryReader.cs`** — fix the RowKey query:
```csharp
// Before (broken): filter = $"PartitionKey eq 'statuses' and RowKey eq '{urlName}'"
// After (correct): prefix scan
var filter = TableClient.CreateQueryFilter(
    $"PartitionKey eq 'statuses' and RowKey ge '{urlName}_' and RowKey lt '{urlName}_~'");
```

**`statusSiteStateReader.cs`** — apply the filter when `urlName` is provided:
```csharp
var urlName = req.Query["urlName"];
var filter = string.IsNullOrEmpty(urlName)
    ? $"PartitionKey eq 'statuses'"
    : $"PartitionKey eq 'statuses' and RowKey eq '{Convert.ToBase64String(Encoding.UTF8.GetBytes(urlName))}'";
```

### Phase 4 — Secrets Cleanup
*(Same as upgrade plan — move hardcoded sender/subject to app settings)*

### Phase 5 — APIM *(optional, same two scenarios as upgrade plan)*

---

## App Settings — Final Reference

| Setting | Used By | Notes |
|---|---|---|
| `AzureWebJobsStorage` | All bindings + Durable | Storage account connection |
| `EMAIL_SENDER` | `SendNotificationActivity` | Verified ACS sender address |
| `EMAIL_SUBJECT` | `SendNotificationActivity` | Email subject line |
| `EMAIL_RECIPIENTS` | `SendNotificationActivity` | Semicolon-separated recipient list |
| `ACS_ENDPOINT` | `SendNotificationActivity` | ACS resource endpoint URI (production) |
| `ACS_CONNECTION_STRING` | `SendNotificationActivity` | ACS connection string (local dev only) |
| `dashboard_url` | `SendNotificationActivity` | Link included in alert email body |
| ~~`STATUS_URL_LIST_PROXY`~~ | ~~`statusUrlTaskAssigner`~~ | **Removed** — no longer needed |
| ~~`SENDGRID_API_KEY`~~ | ~~`emailAlerter_csharp`~~ | **Removed** after ACS validated |

---

## Verification Checklist

- [ ] `dotnet build` passes with 0 errors
- [ ] `func start` launches all 9 functions locally
- [ ] Trigger `httpPollerTrigger` → Durable orchestration instance starts (visible in Durable dashboard or via `GET /runtime/webhooks/durabletask/instances`)
- [ ] All URLs are polled in parallel (check logs for concurrent activity execution)
- [ ] State rows appear in `statusTable` after a poll cycle
- [ ] History rows appear in `statusHistoryTable` with correct `urlName_timestamp` row keys
- [ ] `statusHistoryReader?urlName=x` returns only rows for that URL
- [ ] `statusSiteStateReader?urlName=x` returns only the state for that URL
- [ ] Deliberate non-OK URL triggers email via ACS
- [ ] `git grep -r "asalomon\|sendgrid\|SENDGRID\|STATUS_URL_LIST_PROXY"` returns no matches
- [ ] *(APIM)* Subscription key required; call without key returns 401
