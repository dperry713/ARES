using Ares.Contracts;
namespace Ares.Domain;

public sealed record Approval : IEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ExecutionId { get; init; }
    // MEC §10.3 binding: an approval authorizes ONE exact operation.
    public Guid EngagementId { get; init; }
    public Guid TenantId { get; init; }
    public string Target { get; init; } = string.Empty;
    public string ModuleId { get; init; } = string.Empty;
    public string ModuleVersion { get; init; } = string.Empty;
    public string OperationHash { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public string? ApprovedBy { get; init; }
    public string ApprovalScope { get; init; } = string.Empty;
    public DateTime ApprovedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string Decision { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    // Bounded-use semantics: single-use by default (MaxUses = 1).
    public int MaxUses { get; init; } = 1;
    public int Uses { get; init; }
}