using Ares.Contracts;
namespace Ares.Domain;

public sealed record Finding : IEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid EngagementId { get; init; }
    public Guid AssetId { get; init; }
    public Guid? VulnerabilityId { get; init; }
    public string Severity { get; init; } = "Medium";
    public double Confidence { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Impact { get; init; } = string.Empty;
    public string Remediation { get; init; } = string.Empty;
    public string EvidenceReferences { get; init; } = string.Empty;
    public DateTime FirstObservedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastObservedAt { get; init; } = DateTime.UtcNow;
    public string VerificationStatus { get; set; } = "Unverified";
}