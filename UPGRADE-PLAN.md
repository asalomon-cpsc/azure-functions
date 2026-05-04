# Azure Functions Modernization Plan

## Current State

- 12 C# script (`.csx`) functions, Azure Functions v2-era host, extension bundle `[1.*, 2.0.0)`
- Old `Microsoft.WindowsAzure.Storage` SDK patterns (`CloudTable`, `ICollector`)
- SendGrid output binding for email alerts — **SendGrid free trial exhausted**
- Hardcoded sender email `asalomon@azure.com` in `emailAlerter_csharp/function.json`
- All other secrets already use environment variables (`AzureWebJobsStorage`, `SENDGRID_API_KEY`, `EMAIL_RECIPIENTS`, `dashboard_url`, `STATUS_URL_LIST_PROXY`)
- 5 HTTP-exposed functions: `httpPollerTrigger`, `statusUrlListReader`, `urlPersister`, `statusSiteStateReader`, `statusHistoryReader`

## Target State

- .NET 8 Isolated Worker (Azure Functions v4)
- Azure Communication Services Email (managed identity, 100 emails/day free)
- All secrets in app settings / Key Vault — zero hardcoded values in source
- Optional: 5 HTTP endpoints fronted by Azure API Management

---

## Phase 1 — Project Scaffold

1. Create `cpsc-functions.csproj` at workspace root
   - `OutputType=Exe`, `TargetFramework=net8.0`, `AzureFunctionsVersion=v4`
2. Create `Program.cs` with `HostBuilder` + `ConfigureFunctionsWorkerDefaults`
3. Update root `host.json`
   - Bump extension bundle from `[1.*, 2.0.0)` → `[4.*, 5.0.0)`
   - Delete empty nested `statusUrlListReader/host.json` and `urlPersister/host.json`
4. Create `local.settings.json.template` listing all required app settings without values
5. Add `local.settings.json` to `.gitignore`

### NuGet Packages

| Package | Min Version |
|---|---|
| `Microsoft.Azure.Functions.Worker` | 1.23.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http` | 3.2.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Timer` | 4.3.1 |
| `Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues` | 5.5.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Tables` | 1.3.1 |
| `Azure.Communication.Email` | 1.0.1 |
| `Azure.Identity` | 1.12.0 |
| `Microsoft.Extensions.Azure` | 1.7.4 |

---

## Phase 2 — Convert All 12 Functions

Each `run.csx` + `function.json` pair is replaced by a single `.cs` class file using attributes. Batches within the same group can be done in parallel.

### SDK Changes Applied to Every Function

| Old (v2 script) | New (v4 Isolated Worker) |
|---|---|
| `HttpRequest` / `ILogger` | `HttpRequestData` / `FunctionContext` |
| `ICollector<T>` | `IAsyncCollector<T>` or output binding return type |
| `CloudTable` | `TableClient` (`Azure.Data.Tables`) |
| `TableQuery<T>` | `TableClient.QueryAsync<T>(filter)` |
| `Newtonsoft.Json` | `System.Text.Json` |
| `function.json` binding attributes | C# `[QueueTrigger]`, `[TableInput]`, etc. attributes |

### Conversion Batches

**Batch A — Initiators** *(parallel)*
- `pollerTrigger` — TimerTrigger, queue output
- `httpPollerTrigger` — HttpTrigger, queue output

**Batch B — URL Management** *(parallel)*
- `urlPersister` — HttpTrigger (POST/PUT/DELETE), queue output
- `urlQueuePersister` — QueueTrigger, TableClient write
- `statusUrlListReader` — HttpTrigger (GET), TableClient read

**Batch C — Status Pipeline** *(do in order)*
1. `statusUrlTaskAssigner` — QueueTrigger, HTTP call, queue output
2. `statusPoller_http` — QueueTrigger, HTTP poll, 3x queue output
3. `statusQueuePersister` — QueueTrigger, TableClient write
4. `statusHistoryQueuePersister` — QueueTrigger, TableClient write

**Batch D — Readers** *(parallel)*
- `statusSiteStateReader` — HttpTrigger (GET/POST/OPTIONS), TableClient read
- `statusHistoryReader` — HttpTrigger (GET/POST), TableClient read
- **Bug fix included**: `statusHistoryReader` currently queries `RowKey == urlName` but the writer stores row keys as `urlName + timestamp` — single-record lookups never match. Fix the filter to use `RowKey ge urlName` during the rewrite.

**Batch E — Email** *(after Phase 3 & 4)*
- `emailAlerter_csharp` — QueueTrigger, ACS EmailClient send

---

## Phase 3 — Secrets & Hardcoded Values Cleanup

| Location | Issue | Fix |
|---|---|---|
| `emailAlerter_csharp/function.json` line 16 | `asalomon@azure.com` hardcoded sender | Move to `EMAIL_SENDER` app setting |
| `emailAlerter_csharp/function.json` line 17 | `Azure Site Status Notification` hardcoded subject | Move to `EMAIL_SUBJECT` app setting |
| `emailAlerter_csharp/function.json` | Entire `sendGrid` output binding block | Remove — replaced by ACS SDK call in Phase 4 |
| `statusPoller_http/run.csx` line 123 | MDN HTTP 504 URL hardcoded | Not a secret; keep as a named `const string` |

All other secrets (`AzureWebJobsStorage`, `SENDGRID_API_KEY`, `EMAIL_RECIPIENTS`, `dashboard_url`, `STATUS_URL_LIST_PROXY`) are already environment-variable-backed — no changes needed.

After Phase 4 is validated, remove the `SENDGRID_API_KEY` app setting from the function app.

---

## Phase 4 — SendGrid → Azure Communication Services Email

### Code Changes (`emailAlerter_csharp`)

1. Remove `sendGrid` output binding from `function.json`
2. Register `EmailClient` in `Program.cs`:
   ```csharp
   // Managed identity (production)
   builder.Services.AddSingleton(new EmailClient(
       new Uri(Environment.GetEnvironmentVariable("ACS_ENDPOINT")!),
       new DefaultAzureCredential()));
   // Or connection string (local dev fallback via ACS_CONNECTION_STRING app setting)
   ```
3. Inject `EmailClient` into the function and replace the SendGrid binding parameter
4. Send via:
   ```csharp
   await emailClient.SendAsync(
       WaitUntil.Completed,
       new EmailMessage(
           senderAddress: Environment.GetEnvironmentVariable("EMAIL_SENDER"),
           content: new EmailContent(Environment.GetEnvironmentVariable("EMAIL_SUBJECT"))
               { Html = htmlBody },
           recipients: new EmailRecipients(recipientList)));
   ```

### Azure Prerequisites

1. Provision an Azure Communication Services resource
2. Add and verify a sender email domain (or use an Azure-managed domain)
3. Assign the `Communication and Email Send` role to the function app's managed identity
4. Add `ACS_ENDPOINT` app setting (production) or `ACS_CONNECTION_STRING` (local dev) to function app configuration

---

## Phase 5 — API Management *(optional)*

Front these 5 HTTP endpoints with APIM:

| Function | Method(s) | Product |
|---|---|---|
| `httpPollerTrigger` | POST | Admin |
| `statusUrlListReader` | GET | Status Dashboard |
| `urlPersister` | POST, PUT, DELETE | Admin |
| `statusSiteStateReader` | GET | Status Dashboard |
| `statusHistoryReader` | GET | Status Dashboard |

Change all 5 from `AuthorizationLevel.Function` → `AuthorizationLevel.Anonymous` — APIM subscription key replaces function keys.

### Scenario A — New APIM Instance

- Bicep template for **APIM Consumption tier** (no idle cost, pay-per-call)
- Named Value: function app base URL
- Two Products:
  - **Status Dashboard** (read-only) — rate limit 100 calls/60s
  - **Admin** (write) — rate limit 20 calls/60s, restricted subscription
- Policies per product: require subscription key, rate limiting, CORS headers
- Optional: OpenAPI spec auto-generated from function routes and imported into APIM

### Scenario B — Existing APIM Instance

- Add the function app as a new Backend in APIM
- Define the 5 HTTP routes manually or import an OpenAPI spec
- Apply the same Product and policy structure as Scenario A

---

## Verification Checklist

- [ ] `dotnet build` passes with 0 errors
- [ ] `func start` launches all 12 functions locally with `local.settings.json` populated
- [ ] HTTP reader endpoints return expected JSON
- [ ] Queue-triggered functions process test messages end-to-end
- [ ] Email arrives via ACS to a test recipient
- [ ] `git grep -r "asalomon\|sendgrid\|SENDGRID"` returns no matches in committed files
- [ ] *(APIM)* Call with subscription key → 200; call without key → 401

---

## App Settings Reference

| Setting | Used By | Notes |
|---|---|---|
| `AzureWebJobsStorage` | All queue/table bindings | Connection string for storage account |
| `EMAIL_SENDER` | `emailAlerter_csharp` | Verified ACS sender address |
| `EMAIL_SUBJECT` | `emailAlerter_csharp` | Email subject line |
| `EMAIL_RECIPIENTS` | `emailAlerter_csharp` | Comma-separated recipient list |
| `ACS_ENDPOINT` | `emailAlerter_csharp` | ACS resource endpoint URI (production) |
| `ACS_CONNECTION_STRING` | `emailAlerter_csharp` | ACS connection string (local dev only) |
| `dashboard_url` | `emailAlerter_csharp` | Link included in alert email body |
| `STATUS_URL_LIST_PROXY` | `statusUrlTaskAssigner` | URL of the `statusUrlListReader` HTTP endpoint |
