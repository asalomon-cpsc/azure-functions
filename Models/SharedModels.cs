using Azure;
using Azure.Data.Tables;

namespace CpscFunctions;

/// <summary>
/// Returned by PollUrlActivity and aggregated by the orchestrator
/// to decide whether to send a notification.
/// </summary>
public class PollResult
{
    public string UrlName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
}

// ---------------------------------------------------------------------------
// Queue message models (plain POCOs — no ITableEntity, clean JSON serialization)
// ---------------------------------------------------------------------------

/// <summary>Flows on status-url-queue: one message per URL to poll.</summary>
public class UrlPollItem
{
    public string UrlName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Flows on url-management-queue: URL add/update/delete requests
/// from urlPersister → urlQueuePersister.
/// </summary>
public class UrlManagementMessage
{
    public string Action { get; set; } = string.Empty;
    public string UrlName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Flows on status-states-queue and status-history-queue from statusPoller_http,
/// and on status-notifications-queue as a JSON array.
/// </summary>
public class StatusPollResult
{
    public string UrlName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
}

// ---------------------------------------------------------------------------
// Table entity models (implement ITableEntity for Azure.Data.Tables SDK)
// ---------------------------------------------------------------------------

/// <summary>
/// Row in statusTable (current state) and statusHistoryTable (history).
/// RowKey conventions:
///   statusTable:        base64(Url)
///   statusHistoryTable: urlName_timestamp
/// </summary>
public class StatusTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "statuses";
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; } = ETag.All;

    public string UrlName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
}

/// <summary>Row in urlTable. RowKey = UrlName.</summary>
public class UrlTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "urls";
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; } = ETag.All;

    public string UrlName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
}
