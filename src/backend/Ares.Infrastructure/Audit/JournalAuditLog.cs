using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ares.Infrastructure.Journal;

namespace Ares.Infrastructure.Audit;

/// <summary>
/// Security-relevant events that are NOT state transitions (authentication
/// failures, policy denials, approval decisions, stop/recover). They are
/// appended to the same hash-chained journal, so the operational log and the
/// security audit log share one tamper-evident backbone.
/// </summary>
public interface IAuditLog
{
    void Record(string action, string outcome, string resourceType, string resourceId,
        string? detail = null, string? subject = null, Guid? tenantId = null);

    IReadOnlyList<AuditRecord> Read(string actionPrefix = "", long afterSequence = 0, int limit = 200, Guid? tenantId = null);
}

public sealed record AuditRecord(
    long Sequence, string Action, string Outcome, string ResourceType, string ResourceId,
    string Subject, string? Detail, DateTimeOffset OccurredAt, string Hash, Guid? TenantId);

public sealed class JournalAuditLog(IStateJournal journal) : IAuditLog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Record(string action, string outcome, string resourceType, string resourceId,
        string? detail = null, string? subject = null, Guid? tenantId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        var payload = JsonSerializer.Serialize(new
        {
            action,
            outcome,
            resourceType,
            resourceId,
            subject = subject ?? JournalActor.Name,
            detail,
            tenantId
        }, Json);

        var stream = $"audit/{action}/{(string.IsNullOrEmpty(resourceId) ? "none" : resourceId)}";
        journal.Append(stream, action, payload, subject ?? JournalActor.Name);
    }

    public IReadOnlyList<AuditRecord> Read(string actionPrefix = "", long afterSequence = 0, int limit = 200, Guid? tenantId = null)
    {
        var prefix = string.IsNullOrEmpty(actionPrefix) ? "audit/" : $"audit/{actionPrefix}";
        var entries = journal.Read(prefix, afterSequence, limit);
        var results = new List<AuditRecord>(entries.Count);
        foreach (var entry in entries)
        {
            using var doc = JsonDocument.Parse(entry.Payload);
            var root = doc.RootElement;
            Guid? scope = null;
            if (root.TryGetProperty("tenantId", out var t) && t.ValueKind == JsonValueKind.String &&
                Guid.TryParse(t.GetString(), out var parsed))
                scope = parsed;

            if (tenantId is not null && scope != tenantId) continue;

            results.Add(new AuditRecord(
                entry.Sequence,
                root.TryGetProperty("action", out var a) ? a.GetString() ?? entry.EventType : entry.EventType,
                root.TryGetProperty("outcome", out var o) ? o.GetString() ?? "" : "",
                root.TryGetProperty("resourceType", out var rt) ? rt.GetString() ?? "" : "",
                root.TryGetProperty("resourceId", out var ri) ? ri.GetString() ?? "" : "",
                root.TryGetProperty("subject", out var s) ? s.GetString() ?? entry.Actor : entry.Actor,
                root.TryGetProperty("detail", out var d) ? d.GetString() : null,
                entry.OccurredAt,
                entry.Hash,
                scope));
        }
        return results;
    }
}

/// <summary>Deterministic, non-reversible resource fingerprint for logs.</summary>
public static class AuditFingerprint
{
    public static string Of(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];
}
