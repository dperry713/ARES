using Ares.Contracts;
namespace Ares.Domain;

public sealed record Evidence : IEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid EngagementId { get; init; }
    public Guid ExecutionId { get; init; }
    public string EvidenceType { get; init; } = string.Empty;
    public string ObjectReference { get; init; } = string.Empty;
    public string SHA256 { get; init; } = string.Empty;
    public DateTime CollectedAt { get; init; } = DateTime.UtcNow;
    public string Classification { get; init; } = "Internal";
    public string Provenance { get; init; } = string.Empty;
    // Tamper-evident chain (blueprint §6.4): hex SHA-256 over the canonical
    // record bytes; PreviousHash commits to the prior record in the same
    // engagement chain (null for the genesis record).
    public string RecordHash { get; init; } = string.Empty;
    public string? PreviousHash { get; init; }
    public long Sequence { get; init; }
}