# Deployment Instructions

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local#install-the-azure-functions-core-tools)
- Azure CLI (`az login` authenticated)
- An existing Azure Function App (Consumption plan, .NET 8 Isolated Worker runtime)
- An Azure Communication Services resource with Email enabled

---

## 1. Azure Communication Services Setup

1. In the Azure portal, open your **Azure Communication Services** resource
2. Go to **Email** → **Domains** → **Add domain** → select **Azure-managed domain**
   - This provisions instantly with no DNS verification required
   - You will get an address like `donotreply@xxxxxxxx.azurecomm.net`
3. Copy that sender address — you will need it for `EMAIL_SENDER` below

### Grant Function App access to ACS (production)

```bash
# Get the function app's managed identity principal ID
principalId=$(az functionapp identity show \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --query principalId -o tsv)

# Get the ACS resource ID
acsId=$(az communication service show \
  --name <acs-resource-name> \
  --resource-group <resource-group> \
  --query id -o tsv)

# Assign the send role
az role assignment create \
  --assignee $principalId \
  --role "Contributor" \
  --scope $acsId
```

> For local development, use a connection string instead (see `ACS_CONNECTION_STRING` below).

---

## 2. Required App Settings

Set these in the Azure portal under **Function App → Configuration → Application settings**,  
or in `local.settings.json` for local development (this file is gitignored).

| Setting | Required | Description |
|---|---|---|
| `AzureWebJobsStorage` | Yes | Connection string for the Azure Storage account used for queues and tables |
| `FUNCTIONS_WORKER_RUNTIME` | Yes | Must be `dotnet-isolated` |
| `EMAIL_SENDER` | Yes | The ACS Azure-managed sender address, e.g. `donotreply@xxxxxxxx.azurecomm.net` |
| `EMAIL_SUBJECT` | Yes | Subject line for alert emails, e.g. `Azure Site Status Notification` |
| `EMAIL_RECIPIENTS` | Yes | Semicolon-separated list of recipient email addresses, e.g. `user@example.com;other@example.com` |
| `ACS_ENDPOINT` | Production | URI of your ACS resource, e.g. `https://your-acs.communication.azure.com` — used with managed identity |
| `ACS_CONNECTION_STRING` | Local dev | ACS connection string from the Azure portal — used as fallback when `ACS_ENDPOINT` is not set |
| `dashboard_url` | Yes | Full URL to your status dashboard — included as a link in alert emails |
| `STATUS_URL_LIST_PROXY` | Yes | Full URL of the `statusUrlListReader` function including the `code=` key, e.g. `https://<app>.azurewebsites.net/api/statusUrlListReader?code=<key>` |

### Settings removed from previous version

| Setting | Reason |
|---|---|
| `SENDGRID_API_KEY` | Replaced by Azure Communication Services |

---

## 3. Local Development

Copy the template and fill in values:

```bash
cp local.settings.json.template local.settings.json
# Edit local.settings.json with your actual values
```

Run locally:

```bash
func start
```

---

## 4. Deploy to Azure

```bash
dotnet build cpsc-functions.csproj --configuration Release

func azure functionapp publish <function-app-name> --dotnet-isolated
```

Or using the Azure CLI:

```bash
az functionapp deployment source config-zip \
  --name <function-app-name> \
  --resource-group <resource-group> \
  --src <path-to-zip>
```

---

## 5. Storage Tables and Queues

These are created automatically on first use. For reference:

| Resource | Type | Purpose |
|---|---|---|
| `urlTable` | Table | Stores the list of URLs to monitor |
| `statusTable` | Table | Stores current status per URL |
| `statusHistoryTable` | Table | Stores full polling history per URL |
| `url-management-queue` | Queue | URL add/update/delete requests |
| `status-initiator-queue` | Queue | Polling cycle trigger signal |
| `status-url-queue` | Queue | One message per URL to poll |
| `status-states-queue` | Queue | Current-state persistence handoff |
| `status-history-queue` | Queue | History persistence handoff |
| `status-notifications-queue` | Queue | Email alert trigger |

---

## 6. Verify Deployment

```bash
# Trigger a manual poll
curl -X POST "https://<app>.azurewebsites.net/api/httpPollerTrigger?code=<key>"

# Check current status of all monitored URLs
curl "https://<app>.azurewebsites.net/api/statusSiteStateReader?code=<key>"

# Check URL list
curl "https://<app>.azurewebsites.net/api/statusUrlListReader?code=<key>"
```
