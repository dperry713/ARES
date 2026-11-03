using Ares.Domain;
using Ares.Infrastructure.Journal;

namespace Ares.Approvals.Services;

public interface IApprovalService
{
    /// <summary>Request approval for one exact operation (MEC §10.3 binding).</summary>
    Task<Approval> RequestAsync(Guid executionId, Guid engagementId, Guid tenantId,
        string target, string moduleId, string moduleVersion, string operationHash,
        string requestedBy, TimeSpan ttl, int maxUses = 1);

    /// <summary>Human approves a pending request. AI-generated approval is prohibited.</summary>
    Task<Approval> ApproveAsync(Guid approvalId, string approverIdentity);

    Task<Approval> RejectAsync(Guid approvalId, string approverIdentity, string reason);

    Task<Approval> GetAsync(Guid approvalId);

    Task<IReadOnlyList<Approval>> ListPendingAsync(Guid tenantId, int skip = 0, int take = 50);

    /// <summary>
    /// Validate a presented approval against the exact operation about to execute.
    /// Any material change (engagement, tenant, target, module id/version, op hash),
    /// expiry, over-use, or cross-execution reuse throws. Fail closed. When
    /// <paramref name="expectedApprovalId"/> is supplied, a different approval is
    /// rejected BEFORE its use budget is consumed, so a forged id cannot burn the
    /// real approval.
    /// </summary>
    Task<Approval> ValidateForExecutionAsync(Guid executionId, Guid engagementId, Guid tenantId,
        string target, string moduleId, string moduleVersion, string operationHash,
        DateTimeOffset now, Guid? expectedApprovalId = null);
}

/// <summary>
/// Durable approval store. Two properties are load-bearing:
/// <list type="bullet">
/// <item>Binding: an approval authorizes exactly one operation, once, inside a
/// bounded window. Any material change invalidates it.</item>
/// <item>Separation of duties: the human who approves can never be the subject
/// that requested. A single credential cannot both request and release a
/// security test.</item>
/// </list>
/// </summary>
public class ApprovalService : IApprovalService
{
    private readonly Dictionary<Guid, Approval> _approvals;
    private readonly object _lock = new();
    private readonly IStateJournal? _journal;

    public ApprovalService(IStateJournal? journal = null)
    {
        _journal = journal;
        _approvals = JournalReplay.LatestById<Approval>(journal, "approval/");
    }

    public Task<Approval> RequestAsync(Guid executionId, Guid engagementId, Guid tenantId,
        string target, string moduleId, string moduleVersion, string operationHash,
        string requestedBy, TimeSpan ttl, int maxUses = 1)
    {
        if (executionId == Guid.Empty) throw new ArgumentException("Execution id required.", nameof(executionId));
        if (engagementId == Guid.Empty) throw new ArgumentException("Engagement id required.", nameof(engagementId));
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant id required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("Target required.", nameof(target));
        if (string.IsNullOrWhiteSpace(moduleId)) throw new ArgumentException("Module id required.", nameof(moduleId));
        if (string.IsNullOrWhiteSpace(moduleVersion)) throw new ArgumentException("Module version required.", nameof(moduleVersion));
        if (string.IsNullOrWhiteSpace(operationHash)) throw new ArgumentException("Operation hash required.", nameof(operationHash));
        if (string.IsNullOrWhiteSpace(requestedBy)) throw new ArgumentException("Requester required.", nameof(requestedBy));
        if (ttl <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be positive; use a short window.");
        if (ttl > TimeSpan.FromHours(24)) throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must not exceed 24 hours.");
        if (maxUses < 1) throw new ArgumentOutOfRangeException(nameof(maxUses), "MaxUses must be >= 1.");

        var approval = new Approval
        {
            Id = Guid.NewGuid(),
            ExecutionId = executionId,
            EngagementId = engagementId,
            TenantId = tenantId,
            Target = target.Trim(),
            ModuleId = moduleId,
            ModuleVersion = moduleVersion,
            OperationHash = operationHash,
            RequestedBy = requestedBy,
            ApprovalScope = target,
            ApprovedAt = DateTime.MinValue,
            ExpiresAt = DateTime.UtcNow.Add(ttl),
            Decision = "Pending",
            Reason = string.Empty,
            MaxUses = maxUses,
            Uses = 0
        };

        lock (_lock)
        {
            Persist(approval);
            return Task.FromResult(approval);
        }
    }

    public Task<Approval> ApproveAsync(Guid approvalId, string approverIdentity)
    {
        if (string.IsNullOrWhiteSpace(approverIdentity))
            throw new ArgumentException("A human approver identity is required.", nameof(approverIdentity));

        lock (_lock)
        {
            var rec = Require(approvalId);
            if (rec.Decision != "Pending")
                throw new InvalidOperationException($"Approval {approvalId} is '{rec.Decision}', not Pending.");
            if (string.Equals(rec.RequestedBy, approverIdentity, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Separation of duties: the approver must not be the requestor of the same operation (MEC §10.3).");
            if (DateTime.UtcNow > rec.ExpiresAt)
            {
                Persist(rec with { Decision = "Expired" });
                throw new InvalidOperationException($"Approval {approvalId} has expired (fail closed).");
            }

            var approved = rec with { Decision = "Approved", ApprovedBy = approverIdentity, ApprovedAt = DateTime.UtcNow };
            Persist(approved);
            return Task.FromResult(approved);
        }
    }

    public Task<Approval> RejectAsync(Guid approvalId, string approverIdentity, string reason)
    {
        if (string.IsNullOrWhiteSpace(approverIdentity))
            throw new ArgumentException("A human approver identity is required.", nameof(approverIdentity));

        lock (_lock)
        {
            var rec = Require(approvalId);
            if (rec.Decision != "Pending")
                throw new InvalidOperationException($"Approval {approvalId} is '{rec.Decision}', not Pending.");
            var rejected = rec with { Decision = "Rejected", ApprovedBy = approverIdentity, Reason = reason ?? string.Empty };
            Persist(rejected);
            return Task.FromResult(rejected);
        }
    }

    public Task<Approval> GetAsync(Guid approvalId)
    {
        lock (_lock) return Task.FromResult(Require(approvalId));
    }

    public Task<IReadOnlyList<Approval>> ListPendingAsync(Guid tenantId, int skip = 0, int take = 50)
    {
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(take), "Page size must be 1..500.");
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<Approval>>(_approvals.Values
                .Where(a => a.TenantId == tenantId && a.Decision == "Pending")
                .OrderBy(a => a.ExpiresAt)
                .Skip(skip).Take(take).ToList());
        }
    }

    public Task<Approval> ValidateForExecutionAsync(Guid executionId, Guid engagementId, Guid tenantId,
        string target, string moduleId, string moduleVersion, string operationHash,
        DateTimeOffset now, Guid? expectedApprovalId = null)
    {
        lock (_lock)
        {
            Approval? match = null;
            foreach (var rec in _approvals.Values)
            {
                if (rec.ExecutionId == executionId && rec.Decision == "Approved")
                {
                    match = rec;
                    break;
                }
            }

            if (match is null)
                throw new InvalidOperationException($"No approved approval bound to execution {executionId} (fail closed).");
            // Identity check happens before any state mutation: presenting the
            // wrong approval id must not consume the real approval's use budget.
            if (expectedApprovalId is { } expected && expected != Guid.Empty && match.Id != expected)
                throw new InvalidOperationException(
                    $"Approval {expected} is not the approval bound to execution {executionId} (fail closed).");
            if (match.EngagementId != engagementId)
                throw new InvalidOperationException("Approval engagement mismatch: material change invalidates approval.");
            if (match.TenantId != tenantId)
                throw new InvalidOperationException("Approval tenant mismatch: cross-tenant use denied.");
            if (!string.Equals(match.Target, target, StringComparison.Ordinal))
                throw new InvalidOperationException("Approval target mismatch: material change invalidates approval.");
            if (!string.Equals(match.ModuleId, moduleId, StringComparison.Ordinal))
                throw new InvalidOperationException("Approval module mismatch: material change invalidates approval.");
            if (!string.Equals(match.ModuleVersion, moduleVersion, StringComparison.Ordinal))
                throw new InvalidOperationException("Approval module-version mismatch: material change invalidates approval.");
            if (!string.Equals(match.OperationHash, operationHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Approval operation-hash mismatch: material change invalidates approval.");
            if (now.UtcDateTime > match.ExpiresAt)
                throw new InvalidOperationException("Approval has expired (fail closed).");
            if (match.Uses >= match.MaxUses)
                throw new InvalidOperationException("Approval use budget exhausted (single-use enforced).");

            var consumed = match with { Uses = match.Uses + 1 };
            Persist(consumed);
            return Task.FromResult(consumed);
        }
    }

    private Approval Require(Guid approvalId)
    {
        if (!_approvals.TryGetValue(approvalId, out var rec))
            throw new KeyNotFoundException($"Approval {approvalId} not found.");
        return rec;
    }

    private void Persist(Approval approval)
    {
        _approvals[approval.Id] = approval;
        _journal?.Append($"approval/{approval.Id}", "state", JournalJson.Serialize(approval), JournalActor.Name);
    }
}
