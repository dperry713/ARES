using System.Security.Cryptography;
using System.Text;
using Ares.Approvals.Services;
using Ares.Engagements;
using Ares.Execution.Services;
using Ares.Infrastructure.Journal;
using Ares.Policy.Services;
using DomainExecution = Ares.Domain.Execution;

namespace Ares.Execution.Services;

/// <summary>Raised when the policy engine refuses a transition. Maps to HTTP 403.</summary>
public sealed class PolicyDeniedException : Exception
{
    public PolicyDeniedException(string reasonCode, IReadOnlyList<string> explanations)
        : base($"{reasonCode}: {string.Join("; ", explanations)}")
    {
        ReasonCode = reasonCode;
        Explanations = explanations;
    }

    public string ReasonCode { get; }
    public IReadOnlyList<string> Explanations { get; }
    public bool RequiresApproval => ReasonCode is "APPROVAL_REQUIRED" or "APPROVAL_NOT_GRANTED";
}

/// <summary>
/// The exact operation an approval must be bound to (MEC §10.3). Callers may
/// present it for defense in depth; the execution service validates the STORED
/// values, never the presented ones, so a forged binding cannot authorize work.
/// </summary>
public sealed record ExecutionOperationBinding(
    Guid EngagementId,
    Guid TenantId,
    string Target,
    string OperationHash);

public enum ExecutionState
{
    Proposed,
    AwaitingApproval,
    Approved,
    Queued,
    Leased,
    Running,
    Verifying,
    Completed,
    Failed,
    Cancelled
}

public interface IExecutionService
{
    Task<DomainExecution> ProposeAsync(Guid engagementId, Guid hypothesisId, string target, string testCategory,
        string moduleId, string moduleVersion, string? idempotencyKey = null);

    Task<DomainExecution> LeaseAsync(Guid executionId, string workerId, TimeSpan leaseTtl);
    Task<DomainExecution> HeartbeatAsync(Guid executionId, string workerId, TimeSpan leaseExtension);
    Task<IReadOnlyList<Guid>> ReclaimExpiredLeasesAsync(DateTimeOffset now);
    Task<DomainExecution> RecordAttemptFailureAsync(Guid executionId, string failure, int maxAttempts);
    Task<DomainExecution> RequestApprovalAsync(Guid executionId);
    Task<DomainExecution> GrantApprovalAsync(Guid executionId, Guid approvalId, ExecutionOperationBinding? presented = null);
    Task<DomainExecution> QueueAsync(Guid executionId);
    Task<DomainExecution> StartAsync(Guid executionId);
    Task<DomainExecution> CompleteAsync(Guid executionId, string resultReference);
    Task<DomainExecution> VerifyAsync(Guid executionId);
    Task<DomainExecution> CancelAsync(Guid executionId, string? cancelledBy = null, string? reason = null);
    Task<DomainExecution> GetAsync(Guid executionId);
    Task<ExecutionState> GetStateAsync(Guid executionId);
    Task<IReadOnlyList<DomainExecution>> ListAsync(Guid? engagementId = null, Guid? tenantId = null, int skip = 0, int take = 50);
}

/// <summary>
/// Execution control plane.
///
/// Two invariants are enforced structurally, not by convention:
/// <list type="number">
/// <item>Every transition that can cause a network operation (queue, start)
/// re-evaluates the policy engine against CURRENT engagement state. A paused
/// engagement, a closed assessment window, an out-of-scope target, an expired
/// approval, or an active emergency stop denies the transition. (Audit F-02.)</item>
/// <item>The permission that allowed a transition is stamped on the execution
/// (<see cref="DomainExecution.PolicyDecisionId"/>, <c>PolicyReasonCode</c>), so
/// "why did this run" is answerable after the fact.</item>
/// </list>
/// </summary>
public class ExecutionService : IExecutionService
{
    private readonly Dictionary<Guid, DomainExecution> _executions;
    private readonly Dictionary<string, Guid> _idempotencyKeys = new();
    private readonly object _lock = new();
    private readonly IApprovalService _approvals;
    private readonly IPolicyEngine _policy;
    private readonly IEngagementService _engagements;
    private readonly IStateJournal? _journal;
    private readonly Lazy<JobObject?> _jobObject;

public ExecutionService(IPolicyEngine policy, IEngagementService engagements,
    IApprovalService approvals, IStateJournal? journal = null)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _engagements = engagements ?? throw new ArgumentNullException(nameof(engagements));
        _approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
        _journal = journal;
        _executions = JournalReplay.LatestById<DomainExecution>(journal, "execution/");
        _jobObject = new Lazy<JobObject?>(() =>
        {
            try
            {
                return new JobObject(memoryLimitMB: 512, activeProcessLimit: 1, killOnClose: true);
            }
            catch
            {
                return null; // Job Object not available; gracefully degrade
            }
        });
    }

    public async Task<DomainExecution> ProposeAsync(Guid engagementId, Guid hypothesisId, string target,
        string testCategory, string moduleId, string moduleVersion, string? idempotencyKey = null)
    {
        if (engagementId == Guid.Empty) throw new ArgumentException("Engagement id required.", nameof(engagementId));
        if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("Target required.", nameof(target));
        if (string.IsNullOrWhiteSpace(testCategory)) throw new ArgumentException("Test category required.", nameof(testCategory));
        if (string.IsNullOrWhiteSpace(moduleId)) throw new ArgumentException("Module id required.", nameof(moduleId));
        if (string.IsNullOrWhiteSpace(moduleVersion)) throw new ArgumentException("Module version required.", nameof(moduleVersion));

        // Scope is checked at proposal time too: an out-of-scope target never
        // becomes a proposal. The authoritative re-check still happens at queue
        // and start.
        var engagement = await _engagements.GetByIdAsync(engagementId);
        var scope = await _policy.EvaluateScopeAsync(engagement, target);
        if (scope.Decision != PolicyDecision.Permit)
            throw new PolicyDeniedException(scope.ReasonCode, scope.Explanations);

        var operationHash = ComputeOperationHash(engagement.Id, target, testCategory, moduleId, moduleVersion);

        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(idempotencyKey) &&
                _idempotencyKeys.TryGetValue(idempotencyKey, out var existingId) &&
                _executions.TryGetValue(existingId, out var existing))
            {
                if (existing.HypothesisId != hypothesisId ||
                    existing.EngagementId != engagementId ||
                    !string.Equals(existing.Target, target, StringComparison.Ordinal) ||
                    !string.Equals(existing.TestCategory, testCategory, StringComparison.Ordinal) ||
                    !string.Equals(existing.ModuleId, moduleId, StringComparison.Ordinal) ||
                    !string.Equals(existing.ModuleVersion, moduleVersion, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Idempotency key '{idempotencyKey}' was already used for a different operation (fail closed).");
                return existing;
            }

            var execution = new DomainExecution
            {
                Id = Guid.NewGuid(),
                TenantId = engagement.TenantId,
                EngagementId = engagement.Id,
                HypothesisId = hypothesisId,
                Target = target,
                TestCategory = testCategory,
                ModuleId = moduleId,
                ModuleVersion = moduleVersion,
                OperationHash = operationHash,
                Status = nameof(ExecutionState.Proposed),
                IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey
            };
            return Persist(execution, stamp: null);
        }
    }

    public Task<DomainExecution> RequestApprovalAsync(Guid executionId)
    {
        lock (_lock)
        {
            var execution = Require(executionId);
            if (execution.Status != nameof(ExecutionState.Proposed))
                throw new InvalidOperationException(
                    $"Execution {executionId} cannot request approval from status '{execution.Status}' (expected 'Proposed').");
            var updated = Persist(execution with { Status = nameof(ExecutionState.AwaitingApproval) }, stamp: null);
            if (!string.IsNullOrWhiteSpace(execution.IdempotencyKey))
                _idempotencyKeys[execution.IdempotencyKey] = execution.Id;
            return Task.FromResult(updated);
        }
    }

    public async Task<DomainExecution> GrantApprovalAsync(Guid executionId, Guid approvalId,
        ExecutionOperationBinding? presented = null)
    {
        DomainExecution execution;
        lock (_lock)
        {
            execution = Require(executionId);
            if (execution.Status != nameof(ExecutionState.AwaitingApproval))
                throw new InvalidOperationException(
                    $"Execution {executionId} cannot grant approval from status '{execution.Status}' (expected 'AwaitingApproval').");
        }

        if (approvalId == Guid.Empty)
            throw new ArgumentException("A real approval id is required; approvals cannot be self-granted (MEC §10.3).", nameof(approvalId));

        if (presented is not null &&
            (presented.EngagementId != execution.EngagementId ||
             presented.TenantId != execution.TenantId ||
             !string.Equals(presented.Target, execution.Target, StringComparison.Ordinal) ||
             !string.Equals(presented.OperationHash, execution.OperationHash, StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"Presented binding does not match execution {executionId}: material change invalidates approval (fail closed).");

        var validated = await _approvals.ValidateForExecutionAsync(
            executionId, execution.EngagementId, execution.TenantId, execution.Target,
            execution.ModuleId, execution.ModuleVersion, execution.OperationHash,
            DateTimeOffset.UtcNow, expectedApprovalId: approvalId);

        if (validated.Id != approvalId)
            throw new InvalidOperationException(
                $"Approval {approvalId} is not the approval bound to execution {executionId} (fail closed).");

        lock (_lock)
        {
            execution = Require(executionId);
            return Persist(execution with
            {
                Status = nameof(ExecutionState.Approved),
                ApprovalId = validated.Id,
                ApprovedBy = validated.ApprovedBy,
                ApprovalExpiresAt = validated.ExpiresAt
            }, stamp: null);
        }
    }

    public async Task<DomainExecution> QueueAsync(Guid executionId)
    {
        EmergencyStop.ThrowIfStopped("queue");
        var execution = Require(executionId);
        if (execution.Status != nameof(ExecutionState.Approved) &&
            execution.Status != nameof(ExecutionState.Leased))
            throw new InvalidOperationException(
                $"Execution {executionId} cannot queue from status '{execution.Status}' (expected 'Approved' with a bound approval).");

        var authorized = await EnforcePolicyAsync(execution, "queue");

        lock (_lock)
        {
            return Persist(authorized with
            {
                Status = nameof(ExecutionState.Queued),
                WorkerId = null,
                LeaseExpiresAt = null
            }, stamp: authorized);
        }
    }

    public Task<DomainExecution> LeaseAsync(Guid executionId, string workerId, TimeSpan leaseTtl)
    {
        if (string.IsNullOrWhiteSpace(workerId)) throw new ArgumentException("Worker id required.", nameof(workerId));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseTtl, TimeSpan.Zero);
        EmergencyStop.ThrowIfStopped("lease");

        lock (_lock)
        {
            var execution = Require(executionId);
            if (execution.Status != nameof(ExecutionState.Queued))
                throw new InvalidOperationException(
                    $"Execution {executionId} cannot be leased from status '{execution.Status}' (expected 'Queued').");
            return Task.FromResult(Persist(execution with
            {
                Status = nameof(ExecutionState.Leased),
                WorkerId = workerId,
                LeaseExpiresAt = DateTimeOffset.UtcNow.Add(leaseTtl)
            }, stamp: null));
        }
    }

    public Task<DomainExecution> HeartbeatAsync(Guid executionId, string workerId, TimeSpan leaseExtension)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseExtension, TimeSpan.Zero);
        lock (_lock)
        {
            var execution = Require(executionId);
            if (execution.Status != nameof(ExecutionState.Leased))
                throw new InvalidOperationException(
                    $"Execution {executionId} cannot heartbeat from status '{execution.Status}' (expected 'Leased').");
            if (!string.Equals(execution.WorkerId, workerId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Heartbeat for execution {executionId} came from '{workerId}' but the lease is held by '{execution.WorkerId}' (fail closed).");
            return Task.FromResult(Persist(execution with
            {
                LeaseExpiresAt = DateTimeOffset.UtcNow.Add(leaseExtension)
            }, stamp: null));
        }
    }

    public Task<IReadOnlyList<Guid>> ReclaimExpiredLeasesAsync(DateTimeOffset now)
    {
        lock (_lock)
        {
            var reclaimed = new List<Guid>();
            foreach (var execution in _executions.Values.ToList())
            {
                if (execution.Status == nameof(ExecutionState.Leased) &&
                    execution.LeaseExpiresAt is not null &&
                    execution.LeaseExpiresAt <= now)
                {
                    Persist(execution with { Status = nameof(ExecutionState.Queued), WorkerId = null, LeaseExpiresAt = null }, stamp: null);
                    reclaimed.Add(execution.Id);
                }
            }
            return Task.FromResult<IReadOnlyList<Guid>>(reclaimed);
        }
    }

    public Task<DomainExecution> RecordAttemptFailureAsync(Guid executionId, string failure, int maxAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        lock (_lock)
        {
            var execution = Require(executionId);
            if (execution.Status != nameof(ExecutionState.Running))
                throw new InvalidOperationException(
                    $"Execution {executionId} cannot record failure from status '{execution.Status}' (expected 'Running').");
            var attempts = execution.AttemptCount + 1;
            if (attempts < maxAttempts)
            {
                return Task.FromResult(Persist(execution with
                {
                    Status = nameof(ExecutionState.Queued),
                    WorkerId = null,
                    LeaseExpiresAt = null,
                    AttemptCount = attempts,
                    ResultReference = $"attempt {attempts}/{maxAttempts} failed: {failure}"
                }, stamp: null));
            }
            return Task.FromResult(Persist(execution with
            {
                Status = nameof(ExecutionState.Failed),
                CompletedAt = DateTime.UtcNow,
                WorkerId = null,
                LeaseExpiresAt = null,
                AttemptCount = attempts,
                ResultReference = $"dead-letter after {attempts} attempts; last error: {failure}"
            }, stamp: null));
        }
    }

    public async Task<DomainExecution> StartAsync(Guid executionId)
    {
        EmergencyStop.ThrowIfStopped("start");
        var execution = Require(executionId);
        if (execution.Status != nameof(ExecutionState.Queued) &&
            execution.Status != nameof(ExecutionState.Leased))
            throw new InvalidOperationException($"Execution {executionId} is not Queued or Leased (is '{execution.Status}').");

        // Re-evaluate immediately before the network operation: the engagement may
        // have been paused, the approval may have expired, or a stop may have been
        // latched since queueing.
        var authorized = await EnforcePolicyAsync(execution, "start");

        lock (_lock)
        {
            return Persist(authorized with
            {
                Status = nameof(ExecutionState.Running),
                StartedAt = DateTime.UtcNow
            }, stamp: authorized);
        }
    }

    public Task<DomainExecution> CompleteAsync(Guid executionId, string resultReference)
    {
        if (string.IsNullOrWhiteSpace(resultReference))
            throw new ArgumentException("A result reference is required: an execution cannot complete without evidence.", nameof(resultReference));
        lock (_lock)
        {
            var execution = Require(executionId);
            if (execution.Status != nameof(ExecutionState.Running))
                throw new InvalidOperationException($"Execution {executionId} is not Running");
            return Task.FromResult(Persist(execution with
            {
                Status = nameof(ExecutionState.Verifying),
                ResultReference = resultReference
            }, stamp: null));
        }
    }

    public Task<DomainExecution> VerifyAsync(Guid executionId)
    {
        lock (_lock)
        {
            var execution = Require(executionId);
            if (execution.Status != nameof(ExecutionState.Verifying))
                throw new InvalidOperationException($"Execution {executionId} is not Verifying");
            return Task.FromResult(Persist(execution with
            {
                Status = nameof(ExecutionState.Completed),
                CompletedAt = DateTime.UtcNow
            }, stamp: null));
        }
    }

    public Task<DomainExecution> CancelAsync(Guid executionId, string? cancelledBy = null, string? reason = null)
    {
        lock (_lock)
        {
            var execution = Require(executionId);
            if (execution.Status is nameof(ExecutionState.Completed) or nameof(ExecutionState.Cancelled) or nameof(ExecutionState.Failed))
                throw new InvalidOperationException(
                    $"Execution {executionId} is terminal ('{execution.Status}') and cannot be cancelled.");

            return Task.FromResult(Persist(execution with
            {
                Status = nameof(ExecutionState.Cancelled),
                CompletedAt = DateTime.UtcNow,
                WorkerId = null,
                LeaseExpiresAt = null,
                ResultReference = $"cancelled by {cancelledBy ?? "unknown"}: {reason ?? "no reason given"}"
            }, stamp: null));
        }
    }

    public Task<DomainExecution> GetAsync(Guid executionId) => Task.FromResult(Require(executionId));

    public Task<ExecutionState> GetStateAsync(Guid executionId) =>
        Task.FromResult(ParseState(Require(executionId).Status));

    public Task<IReadOnlyList<DomainExecution>> ListAsync(Guid? engagementId = null, Guid? tenantId = null,
        int skip = 0, int take = 50)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        if (take is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(take), "Page size must be 1..500.");
        lock (_lock)
        {
            var query = _executions.Values.AsEnumerable();
            if (engagementId is not null) query = query.Where(e => e.EngagementId == engagementId);
            if (tenantId is not null) query = query.Where(e => e.TenantId == tenantId);
            return Task.FromResult<IReadOnlyList<DomainExecution>>(
                query.OrderByDescending(e => e.StartedAt).Skip(skip).Take(take).ToList());
        }
    }

    /// <summary>
    /// F-02 closure: policy is re-evaluated against live state on every
    /// transition that permits a network operation, and the decision is stamped
    /// on the execution. Denials throw instead of silently proceeding.
    /// </summary>
    private async Task<DomainExecution> EnforcePolicyAsync(DomainExecution execution, string transition)
    {
        var engagement = await _engagements.GetByIdAsync(execution.EngagementId);

        var requiresHumanApproval = engagement.ApprovalRequirements
            .Contains("human_approval", StringComparer.OrdinalIgnoreCase);
        if (requiresHumanApproval)
        {
            if (execution.ApprovalId is null || execution.ApprovalId == Guid.Empty)
                throw new PolicyDeniedException("APPROVAL_REQUIRED",
                    ["This engagement requires a bound human approval before execution."]);
            if (execution.ApprovalExpiresAt is { } expiresAt && expiresAt <= DateTime.UtcNow)
                throw new PolicyDeniedException("APPROVAL_EXPIRED",
                    ["The bound approval expired; request a new approval."]);
        }

        var scope = await _policy.EvaluateScopeAsync(engagement, execution.Target);
        if (scope.Decision != PolicyDecision.Permit)
            throw new PolicyDeniedException(scope.ReasonCode, scope.Explanations);

        var decision = await _policy.EvaluateAsync(engagement, execution.TestCategory, execution.ModuleId,
            execution.ModuleVersion, requiresHumanApproval: false, maximumConcurrency: 1,
            rateLimits: engagement.RateLimits);
        if (decision.Decision != PolicyDecision.Permit)
            throw new PolicyDeniedException(decision.ReasonCode, decision.Explanations);

        var evaluatedAt = DateTime.UtcNow;
        return execution with
        {
            PolicyReasonCode = decision.ReasonCode,
            PolicyDecisionId = ComputeDecisionId(execution.Id, transition, decision.ReasonCode, evaluatedAt),
            PolicyEvaluatedAt = evaluatedAt
        };
    }

    private DomainExecution Require(Guid executionId)
    {
        if (!_executions.TryGetValue(executionId, out var execution))
            throw new KeyNotFoundException($"Execution {executionId} not found");
        return execution;
    }

    /// <summary>
    /// Stores the new state and journals it. <paramref name="stamp"/> carries the
    /// policy decision fields (if any) so they are persisted with the transition.
    /// </summary>
    private DomainExecution Persist(DomainExecution execution, DomainExecution? stamp)
    {
        var toStore = stamp is null ? execution : execution with
        {
            PolicyDecisionId = stamp.PolicyDecisionId ?? execution.PolicyDecisionId,
            PolicyReasonCode = stamp.PolicyReasonCode ?? execution.PolicyReasonCode,
            PolicyEvaluatedAt = stamp.PolicyEvaluatedAt ?? execution.PolicyEvaluatedAt
        };

        _executions[toStore.Id] = toStore;
        if (!string.IsNullOrWhiteSpace(toStore.IdempotencyKey))
            _idempotencyKeys[toStore.IdempotencyKey] = toStore.Id;
        _journal?.Append($"execution/{toStore.Id}", "state", JournalJson.Serialize(toStore), JournalActor.Name);
        return toStore;
    }

    private static string ComputeOperationHash(Guid engagementId, string target, string testCategory,
        string moduleId, string moduleVersion)
    {
        var canonical = string.Join('\n', "ares.operation.v1", engagementId.ToString("N"),
            target.Trim().ToLowerInvariant(), testCategory, moduleId, moduleVersion);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static Guid ComputeDecisionId(Guid executionId, string transition, string reasonCode, DateTime at)
    {
        var canonical = string.Join('\n', executionId.ToString("N"), transition, reasonCode,
            at.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)).AsSpan(0, 16));
    }

    /// <summary>
    /// Gets the associated Job Object for worker isolation, if available.
    /// </summary>
    public JobObject? JobObject => _jobObject.Value;

    /// <summary>
    /// Gets whether Job Object isolation is available and active.
    /// </summary>
    public bool IsJobObjectAvailable => _jobObject.Value is not null;

    private static ExecutionState ParseState(string status)
        => Enum.TryParse<ExecutionState>(status, out var state)
            ? state
            : throw new InvalidOperationException($"Unknown execution status '{status}'");
}
