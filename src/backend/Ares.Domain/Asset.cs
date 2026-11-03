using Ares.Contracts;
namespace Ares.Domain;

public sealed record Asset : IEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid? EngagementId { get; init; }
    public string AssetType { get; init; } = string.Empty;
    public string CanonicalIdentifier { get; init; } = string.Empty;
    public string NetworkLocation { get; init; } = string.Empty;
    public string TechnologyFingerprint { get; init; } = string.Empty;
    public string DiscoverySource { get; init; } = string.Empty;
    public DateTime FirstObservedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastObservedAt { get; init; } = DateTime.UtcNow;
    public double Confidence { get; init; } = 1.0;
}