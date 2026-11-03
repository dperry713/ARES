using Ares.Domain;
using Ares.Evidence.Services;

namespace Ares.Verification.Services;

/// <summary>MEC §13.2 verification levels (primary ladder + alternative terminals).</summary>
public enum VerificationState
{
    Unverified,
    Suspected,
    EvidenceCollected,
    VerificationInProgress,
    Verified,
    FalsePositive,
    Inconclusive,
    NotReproducible,
    Remediated,
    AcceptedRisk
}

public sealed record IndependentCheckResult(
    bool Passed, string Method, string Detail, DateTimeOffset At);

public interface IVerificationService
{
    Task<Finding> MarkSuspectedAsync(Finding finding, Guid tenantId, string analyst, string reason);
    Task<Finding> AttachEvidenceAsync(Finding finding, Guid tenantId, Guid evidenceId, string relationship);
    Task<Finding> BeginVerificationAsync(Finding finding, Guid tenantId, string verifierMethod);
    Task<IndependentCheckResult> RunIndependentCheckAsync(Finding finding, Guid tenantId, string method, string reObservation);
    Task<Finding> MarkVerifiedAsync(Finding finding, Guid tenantId, string checker);
    Task<Finding> MarkNotReproducibleAsync(Finding finding, Guid tenantId, string checker, string reason);
    Task<Finding> MarkFalsePositiveAsync(Finding finding, Guid tenantId, string checker, string reason);
}

/// <summary>
/// Deterministic finding verification (MEC §13, blueprint §6).
/// The ladder is enforced in code: Unverified -> Suspected -> EvidenceCollected
/// -> VerificationInProgress -> Verified. Verified additionally requires a
/// passed independent check recorded on the finding; AI/model confidence alone
/// can never advance anything. Terminal states are terminal. Tenant of every
/// attached evidence record must match the caller's tenant.
/// Milestone bounds: in-memory, single-process; the independent check is a
/// deterministic re-observation harness (non-empty re-observation + distinct
/// method), not a live network re-test — network re-testing is PLANNED.
/// </summary>
public sealed class VerificationService : IVerificationService
{
    private readonly IEvidenceService _store;
    // finding id -> independent check history (in-memory milestone).
    private readonly Dictionary<Guid, List<IndependentCheckResult>> _checks = new();
    private readonly object _lock = new();

    public VerificationService(IEvidenceService store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<Finding> MarkSuspectedAsync(Finding finding, Guid tenantId, string analyst, string reason)
    {
        ArgumentNullException.ThrowIfNull(finding);
        if (string.IsNullOrWhiteSpace(analyst)) throw new ArgumentException("Analyst required.", nameof(analyst));
        RequireStatus(finding, VerificationState.Unverified, "mark Suspected");
        return Task.FromResult(WithStatus(finding, VerificationState.Suspected));
    }

    public async Task<Finding> AttachEvidenceAsync(Finding finding, Guid tenantId, Guid evidenceId, string relationship)
    {
        ArgumentNullException.ThrowIfNull(finding);
        if (string.IsNullOrWhiteSpace(relationship)) throw new ArgumentException("Relationship required.", nameof(relationship));
        RequireStatus(finding, VerificationState.Suspected, "attach evidence");
        // Tenant-bound: the store throws on cross-tenant access (fail closed).
        var record = await _store.GetAsync(evidenceId, tenantId);
        if (record.EngagementId != finding.EngagementId)
            throw new InvalidOperationException("Evidence engagement does not match the finding (fail closed).");
        var refs = string.IsNullOrWhiteSpace(finding.EvidenceReferences)
            ? $"{record.Id}:{relationship}"
            : $"{finding.EvidenceReferences};{record.Id}:{relationship}";
        return WithStatus(finding with { EvidenceReferences = refs }, VerificationState.EvidenceCollected);
    }

    public Task<Finding> BeginVerificationAsync(Finding finding, Guid tenantId, string verifierMethod)
    {
        ArgumentNullException.ThrowIfNull(finding);
        if (string.IsNullOrWhiteSpace(verifierMethod)) throw new ArgumentException("Verifier method required.", nameof(verifierMethod));
        RequireStatus(finding, VerificationState.EvidenceCollected, "begin verification");
        // AI/model confidence is informational only: it cannot substitute for
        // the recorded independent check performed in RunIndependentCheckAsync.
        return Task.FromResult(WithStatus(finding, VerificationState.VerificationInProgress));
    }

    public Task<IndependentCheckResult> RunIndependentCheckAsync(Finding finding, Guid tenantId, string method, string reObservation)
    {
        ArgumentNullException.ThrowIfNull(finding);
        if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("Method required.", nameof(method));
        RequireStatus(finding, VerificationState.VerificationInProgress, "run independent check");
        // Deterministic harness: the check passes only on a non-empty
        // re-observation recorded with a method distinct from "initial".
        var passed = !string.IsNullOrWhiteSpace(reObservation) && method != "initial_observation";
        var result = new IndependentCheckResult(passed, method, reObservation ?? string.Empty, DateTimeOffset.UtcNow);
        lock (_lock)
        {
            if (!_checks.TryGetValue(finding.Id, out var list)) _checks[finding.Id] = list = new List<IndependentCheckResult>();
            list.Add(result);
        }
        return Task.FromResult(result);
    }

    public Task<Finding> MarkVerifiedAsync(Finding finding, Guid tenantId, string checker)
    {
        ArgumentNullException.ThrowIfNull(finding);
        if (string.IsNullOrWhiteSpace(checker)) throw new ArgumentException("Checker required.", nameof(checker));
        RequireStatus(finding, VerificationState.VerificationInProgress, "mark Verified");
        if (string.IsNullOrWhiteSpace(finding.EvidenceReferences))
            throw new InvalidOperationException("Verified requires attached evidence references (fail closed).");
        lock (_lock)
        {
            var passed = _checks.TryGetValue(finding.Id, out var list) && list.Any(c => c.Passed);
            if (!passed)
                throw new InvalidOperationException("Verified requires a passed independent check (fail closed; confidence alone is never enough).");
        }
        if (string.IsNullOrWhiteSpace(finding.Description))
            throw new InvalidOperationException("Verified requires a finding description (fail closed).");
        return Task.FromResult(WithStatus(finding, VerificationState.Verified));
    }

    public Task<Finding> MarkNotReproducibleAsync(Finding finding, Guid tenantId, string checker, string reason)
    {
        ArgumentNullException.ThrowIfNull(finding);
        if (string.IsNullOrWhiteSpace(checker)) throw new ArgumentException("Checker required.", nameof(checker));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason required.", nameof(reason));
        if (ParseStatus(finding.VerificationStatus) is not (VerificationState.VerificationInProgress or VerificationState.EvidenceCollected or VerificationState.Suspected))
            throw new InvalidOperationException($"Cannot mark NotReproducible from '{finding.VerificationStatus}' (fail closed).");
        return Task.FromResult(WithStatus(finding, VerificationState.NotReproducible));
    }

    public Task<Finding> MarkFalsePositiveAsync(Finding finding, Guid tenantId, string checker, string reason)
    {
        ArgumentNullException.ThrowIfNull(finding);
        if (string.IsNullOrWhiteSpace(checker)) throw new ArgumentException("Checker required.", nameof(checker));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason required.", nameof(reason));
        if (ParseStatus(finding.VerificationStatus) is not (VerificationState.Suspected or VerificationState.EvidenceCollected or VerificationState.VerificationInProgress))
            throw new InvalidOperationException($"Cannot mark FalsePositive from '{finding.VerificationStatus}' (fail closed).");
        return Task.FromResult(WithStatus(finding, VerificationState.FalsePositive));
    }

    private static void RequireStatus(Finding finding, VerificationState expected, string operation)
    {
        var actual = ParseStatus(finding.VerificationStatus);
        if (actual != expected)
            throw new InvalidOperationException($"Cannot {operation} from '{finding.VerificationStatus}' (expected '{expected}').");
    }

    private static Finding WithStatus(Finding finding, VerificationState state)
        => finding with { VerificationStatus = state.ToString() };

    private static VerificationState ParseStatus(string status)
        => Enum.TryParse<VerificationState>(status, out var s) ? s : throw new InvalidOperationException($"Unknown verification status '{status}'.");
}
