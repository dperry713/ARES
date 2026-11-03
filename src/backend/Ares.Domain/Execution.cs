using Ares.Contracts;

namespace Ares.Domain;

/// <summary>
/// A single authorized security-test operation. The record carries the exact
/// target/category it was proposed for, the human approval that authorized it,
/// and the policy decision that permitted queueing — so an execution can never
/// drift away from what was approved (MEC §10.1–§10.3).
/// </summary>
public sealed record Execution : IEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid EngagementId { get; init; }
    public Guid HypothesisId { get; init; }

    /// <summary>Destination the operation acts on. Re-checked against scope at queue and start.</summary>
    public string Target { get; init; } = string.Empty;

    /// <summary>Assessment category (e.g. safe_http_configuration_checks).</summary>
    public string TestCategory { get; init; } = string.Empty;

    public string ModuleId { get; init; } = string.Empty;
    public string ModuleVersion { get; init; } = "1.0.0";

    /// <summary>Server-computed binding hash of the exact operation an approval must match.</summary>
    public string OperationHash { get; init; } = string.Empty;

    public Guid? ApprovalId { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTime? ApprovalExpiresAt { get; init; }

    /// <summary>Deterministic id of the policy evaluation that permitted the last transition.</summary>
    public Guid? PolicyDecisionId { get; init; }
    public string? PolicyReasonCode { get; init; }
    public DateTime? PolicyEvaluatedAt { get; init; }

    public string Status { get; init; } = "Proposed";
    public DateTime StartedAt { get; init; } = DateTime.MinValue;
    public DateTime? CompletedAt { get; init; }
    public string? WorkerId { get; init; }
    public string? ResultReference { get; init; }

    // Durable-queue bookkeeping (MEC §11.3, blueprint §5.1).
    public DateTimeOffset? LeaseExpiresAt { get; init; }
    public int AttemptCount { get; init; }
    public string? IdempotencyKey { get; init; }
}
