using Ares.Contracts;
namespace Ares.Domain;

public sealed record TestHypothesis : IEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid EngagementId { get; init; }
    public Guid AssetId { get; init; }
    public Guid? VulnerabilityId { get; init; }
    public string ProposedModule { get; init; } = string.Empty;
    public string[] Prerequisites { get; init; } = Array.Empty<string>();
    public string EvidenceReferences { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public string Priority { get; init; } = "normal";
    public string ProposedBy { get; init; } = string.Empty;
    public string Status { get; init; } = "Proposed";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string? Explanation { get; init; } = null;
    public string? ReferencesToIntelligenceSources { get; init; } = null;
}