using Ares.Contracts;

namespace Ares.Domain;

/// <summary>
/// An authorized assessment window. A written authorization reference, a
/// bounded time window, and an explicit scope are mandatory: an engagement
/// without them cannot exist, so no execution can ever be evaluated against a
/// half-specified authorization (MEC §6.1).
/// </summary>
public sealed record Engagement : IEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>Contract/authorization ticket that permits this work to happen at all.</summary>
    public string AuthorizationReference { get; init; } = string.Empty;

    public DateTime StartAt { get; init; }
    public DateTime EndAt { get; init; }
    public string ScopeDefinition { get; init; } = string.Empty;
    public string[] AllowedAssessmentTypes { get; init; } = Array.Empty<string>();
    public string[] ExcludedTargets { get; init; } = Array.Empty<string>();
    public int MaximumConcurrency { get; init; } = 5;
    public string RateLimits { get; init; } = "default";
    public string[] ApprovalRequirements { get; init; } = Array.Empty<string>();
    public string Status { get; init; } = "Draft";
}
