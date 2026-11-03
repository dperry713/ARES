using Ares.Contracts;
using Ares.Domain;
using Ares.Infrastructure.Journal;

namespace Ares.Engagements;

public interface IEngagementService
{
    Task<Engagement> CreateAsync(Guid tenantId, string name, string description, string authorizationReference,
        DateTime startAt, DateTime endAt, string scopeDefinition, string[] allowedAssessmentTypes,
        string[] excludedTargets, int maximumConcurrency = 5, string rateLimits = "default");

    Task<Engagement> AuthorizeAsync(Guid engagementId);
    Task<Engagement> StartAsync(Guid engagementId);
    Task<Engagement> PauseAsync(Guid engagementId);
    Task<Engagement> ResumeAsync(Guid engagementId);
    Task<Engagement> CompleteAsync(Guid engagementId);
    Task<Engagement> GetByIdAsync(Guid engagementId);

    /// <summary>Tenant-guarded read: throws <see cref="KeyNotFoundException"/> for a foreign tenant.</summary>
    Task<Engagement> GetByIdAsync(Guid engagementId, Guid tenantId);

    Task<IReadOnlyList<Engagement>> ListAsync(Guid? tenantId = null, int skip = 0, int take = 50);
}

/// <summary>
/// Engagement lifecycle authority. Enforces the MEC §6.1 preconditions at
/// creation (written authorization, bounded window, non-empty scope and
/// category allow-list) and the §6.2 transition table thereafter. Every
/// transition is journaled, so an authorization cannot be silently rewritten.
/// </summary>
public class EngagementService : IEngagementService
{
    private const int MaxConcurrencyCeiling = 64;
    private readonly Dictionary<Guid, Engagement> _engagements;
    private readonly object _lock = new();
    private readonly IStateJournal? _journal;

    public EngagementService(IStateJournal? journal = null)
    {
        _journal = journal;
        _engagements = JournalReplay.LatestById<Engagement>(journal, "engagement/");
    }

    public Task<Engagement> CreateAsync(Guid tenantId, string name, string description,
        string authorizationReference, DateTime startAt, DateTime endAt, string scopeDefinition,
        string[] allowedAssessmentTypes, string[] excludedTargets, int maximumConcurrency = 5,
        string rateLimits = "default")
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Engagement name required.", nameof(name));
        if (string.IsNullOrWhiteSpace(authorizationReference))
            throw new ArgumentException(
                "A written authorization reference is required: an engagement cannot exist without documented authorization (MEC §6.1).",
                nameof(authorizationReference));
        if (string.IsNullOrWhiteSpace(scopeDefinition))
            throw new ArgumentException(
                "A scope definition is required: an engagement without scope would authorize nothing.", nameof(scopeDefinition));
        if (allowedAssessmentTypes is null || allowedAssessmentTypes.Length == 0)
            throw new ArgumentException(
                "At least one allowed assessment type is required (empty would be fail-open).", nameof(allowedAssessmentTypes));
        if (allowedAssessmentTypes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Allowed assessment types must not contain blank entries.", nameof(allowedAssessmentTypes));
        if (startAt >= endAt)
            throw new ArgumentException("The assessment window is empty: start must precede end.", nameof(endAt));
        if (maximumConcurrency is < 1 or > MaxConcurrencyCeiling)
            throw new ArgumentOutOfRangeException(nameof(maximumConcurrency),
                $"Concurrency must be between 1 and {MaxConcurrencyCeiling}.");

        var engagement = new Engagement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description ?? string.Empty,
            AuthorizationReference = authorizationReference.Trim(),
            StartAt = startAt,
            EndAt = endAt,
            ScopeDefinition = scopeDefinition.Trim(),
            AllowedAssessmentTypes = allowedAssessmentTypes.Distinct(StringComparer.Ordinal).ToArray(),
            ExcludedTargets = excludedTargets ?? Array.Empty<string>(),
            MaximumConcurrency = maximumConcurrency,
            RateLimits = string.IsNullOrWhiteSpace(rateLimits) ? "default" : rateLimits,
            ApprovalRequirements = new[] { "human_approval" },
            Status = "Draft"
        };

        lock (_lock)
        {
            Persist(engagement);
            return Task.FromResult(engagement);
        }
    }

    public Task<Engagement> AuthorizeAsync(Guid engagementId) =>
        Transition(engagementId, "Authorized", ["Draft"]);

    public Task<Engagement> StartAsync(Guid engagementId) =>
        Transition(engagementId, "Active", ["Authorized"]);

    public Task<Engagement> PauseAsync(Guid engagementId) =>
        Transition(engagementId, "Paused", ["Active"]);

    public Task<Engagement> ResumeAsync(Guid engagementId) =>
        Transition(engagementId, "Active", ["Paused"]);

    public Task<Engagement> CompleteAsync(Guid engagementId) =>
        Transition(engagementId, "Completed", ["Active", "Paused"]);

    public Task<Engagement> GetByIdAsync(Guid engagementId) => Task.FromResult(Require(engagementId));

    public Task<Engagement> GetByIdAsync(Guid engagementId, Guid tenantId)
    {
        var engagement = Require(engagementId);
        // Foreign-tenant reads are indistinguishable from missing ids: no existence oracle.
        if (engagement.TenantId != tenantId)
            throw new KeyNotFoundException($"Engagement {engagementId} not found");
        return Task.FromResult(engagement);
    }

    public Task<IReadOnlyList<Engagement>> ListAsync(Guid? tenantId = null, int skip = 0, int take = 50)
    {
        if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
        if (take is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(take), "Page size must be 1..500.");
        lock (_lock)
        {
            var query = _engagements.Values.AsEnumerable();
            if (tenantId is not null) query = query.Where(e => e.TenantId == tenantId);
            return Task.FromResult<IReadOnlyList<Engagement>>(
                query.OrderByDescending(e => e.StartAt).Skip(skip).Take(take).ToList());
        }
    }

    private Task<Engagement> Transition(Guid engagementId, string target, string[] allowedFrom)
    {
        lock (_lock)
        {
            var engagement = Require(engagementId);
            if (!allowedFrom.Contains(engagement.Status, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    $"Engagement {engagementId} cannot move to '{target}' from '{engagement.Status}' " +
                    $"(allowed from: {string.Join(", ", allowedFrom)}).");

            var updated = engagement with { Status = target };
            Persist(updated);
            return Task.FromResult(updated);
        }
    }

    private Engagement Require(Guid engagementId)
    {
        if (!_engagements.TryGetValue(engagementId, out var engagement))
            throw new KeyNotFoundException($"Engagement {engagementId} not found");
        return engagement;
    }

    private void Persist(Engagement engagement)
    {
        _engagements[engagement.Id] = engagement;
        _journal?.Append($"engagement/{engagement.Id}", "state", JournalJson.Serialize(engagement), JournalActor.Name);
    }
}
