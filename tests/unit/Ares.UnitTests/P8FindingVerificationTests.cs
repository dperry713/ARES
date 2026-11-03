// P8 finding-verification tests (MEC §13.2, blueprint §6.1/§6.4).
// A finding climbs Unverified -> Suspected -> EvidenceCollected ->
// VerificationInProgress -> Verified ONLY through recorded evidence + an
// INDEPENDENT re-check (different method/module from the one that produced the
// observation). Rules pinned here:
//  (a) transitions are validated (no jumps, no Verified-from-Unverified),
//  (b) Verified requires >=1 supporting evidence record in the tamper-evident
//      store AND a passed independent check,
//  (c) a failed re-check moves to NotReproducible (never stays "almost Verified"),
//  (d) terminal states (Verified/FalsePositive/AcceptedRisk/Remediated) are terminal,
//  (e) model/AI confidence alone can never produce Verified — only the
//      deterministic verification path can.
// Written FIRST (RED): Ares.Verification is a Class1.cs shell; nothing exists.
using Ares.Domain;
using DomainEvidence = Ares.Domain.Evidence;
using Ares.Evidence.Services;
using Ares.Verification.Services;
using Xunit;

namespace Ares.UnitTests;

public sealed class FindingVerificationTests
{
    private static (VerificationService svc, EvidenceService store, Guid tenant, Guid engagement) Setup()
    {
        var store = new EvidenceService();
        return (new VerificationService(store), store, Guid.NewGuid(), Guid.NewGuid());
    }

    private static Finding NewFinding(Guid engagement, Guid asset) => new()
    {
        Id = Guid.NewGuid(), EngagementId = engagement, AssetId = asset,
        Severity = "High", Confidence = 0.9, Description = "missing security header",
        VerificationStatus = nameof(VerificationState.Unverified)
    };

    private static Task<DomainEvidence> CollectEvidenceAsync(EvidenceService store, Guid tenant,
        Guid engagement, Guid execution, string content = "observation-bytes") =>
        store.IngestAsync(tenant, engagement, execution, "test_result", content, "Internal", "worker w-1");

    [Fact]
    public async Task Full_path_to_Verified_requires_evidence_plus_independent_check()
    {
        var (svc, store, tenant, engagement) = Setup();
        var asset = Guid.NewGuid();
        var execution = Guid.NewGuid();
        var finding = NewFinding(engagement, asset);
        // Skipping straight to Verified is rejected.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.MarkVerifiedAsync(finding, tenant, "checker-human"));
        // Walk the ladder with evidence attached at each rung.
        finding = await svc.MarkSuspectedAsync(finding, tenant, "analyst-1", "plausible header gap");
        Assert.Equal(nameof(VerificationState.Suspected), finding.VerificationStatus);
        var ev1 = await CollectEvidenceAsync(store, tenant, engagement, execution);
        finding = await svc.AttachEvidenceAsync(finding, tenant, ev1.Id, "supports missing-header");
        Assert.Equal(nameof(VerificationState.EvidenceCollected), finding.VerificationStatus);
        finding = await svc.BeginVerificationAsync(finding, tenant, "verifier-module v2.0");
        Assert.Equal(nameof(VerificationState.VerificationInProgress), finding.VerificationStatus);
        // Independent re-check must be a DIFFERENT method than the observation.
        var check = await svc.RunIndependentCheckAsync(finding, tenant, "repeat_read_only_http_observation",
            "verified header still absent on re-read");
        Assert.True(check.Passed);
        finding = await svc.MarkVerifiedAsync(finding, tenant, "checker-human");
        Assert.Equal(nameof(VerificationState.Verified), finding.VerificationStatus);
        Assert.NotEmpty(finding.EvidenceReferences);
    }

    [Fact]
    public async Task Failed_recheck_moves_to_NotReproducible_not_Verified()
    {
        var (svc, store, tenant, engagement) = Setup();
        var finding = NewFinding(engagement, Guid.NewGuid());
        finding = await svc.MarkSuspectedAsync(finding, tenant, "analyst-1", "maybe");
        var ev = await CollectEvidenceAsync(store, tenant, engagement, Guid.NewGuid());
        finding = await svc.AttachEvidenceAsync(finding, tenant, ev.Id, "support");
        finding = await svc.BeginVerificationAsync(finding, tenant, "verifier-module v2.0");
        // A re-check that finds nothing fails deterministically (empty re-observation).
        var failed = await svc.RunIndependentCheckAsync(finding, tenant, "repeat_read_only_http_observation", "");
        Assert.False(failed.Passed);
        finding = await svc.MarkNotReproducibleAsync(finding, tenant, "checker-human", "re-read shows header present");
        Assert.Equal(nameof(VerificationState.NotReproducible), finding.VerificationStatus);
        // Terminal: Verified is no longer reachable from here.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.MarkVerifiedAsync(finding, tenant, "checker-human"));
    }

    [Fact]
    public async Task High_AI_confidence_alone_cannot_produce_Verified()
    {
        var (svc, _, tenant, engagement) = Setup();
        var finding = NewFinding(engagement, Guid.NewGuid()) with { Confidence = 0.99 };
        // Even at 0.99 confidence, Verified without evidence + check is rejected.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.MarkVerifiedAsync(finding, tenant, "checker-human"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.BeginVerificationAsync(finding, tenant, "verifier-module v2.0"));
    }

    [Fact]
    public async Task Evidence_from_another_tenant_is_rejected()
    {
        var (svc, store, tenant, engagement) = Setup();
        var finding = NewFinding(engagement, Guid.NewGuid());
        finding = await svc.MarkSuspectedAsync(finding, tenant, "analyst-1", "maybe");
        var foreign = await store.IngestAsync(Guid.NewGuid(), engagement, Guid.NewGuid(),
            "test_result", "foreign-bytes", "Internal", "w");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.AttachEvidenceAsync(finding, tenant, foreign.Id, "support"));
    }

    [Fact]
    public async Task Terminal_states_reject_further_transitions()
    {
        var (svc, _, tenant, engagement) = Setup();
        var finding = NewFinding(engagement, Guid.NewGuid());
        finding = await svc.MarkSuspectedAsync(finding, tenant, "analyst-1", "maybe");
        finding = await svc.MarkFalsePositiveAsync(finding, tenant, "checker-human", "benign by design");
        Assert.Equal(nameof(VerificationState.FalsePositive), finding.VerificationStatus);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.MarkSuspectedAsync(finding, tenant, "analyst-1", "again"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.BeginVerificationAsync(finding, tenant, "v"));
    }
}
