// P5 store-backed evidence tests (closes KD-01).
// GenerateEvidenceAsync must persist into the tamper-evident store with the
// tenant resolved from the engagement/asset — never Guid.Empty — and the
// returned record must verify through the store. Null engagement / tenantless
// asset fail closed instead of fabricating attribution. Written FIRST (RED:
// the current helper fabricates Guid.Empty and touches no store).
using Ares.Domain;
using DomainExecution = Ares.Domain.Execution;
using Ares.Evidence.Services;
using Ares.Intelligence.Services;
using Ares.Policy.Services;
using Ares.ai.Providers;
using Xunit;

namespace Ares.UnitTests;

file sealed class NeverAvailableProvider : IIntelligenceProvider
{
    public Task<AISchemaValidation> FetchIntelligenceAsync(string reconnaissanceData)
        => Task.FromResult(new AISchemaValidation { IsValid = false });
    public Task<AIProviderStatus> CheckAvailabilityAsync()
        => Task.FromResult(AIProviderStatus.Unavailable);
    public Task<EpssScore> GetEpssScoreAsync(string cveId)
        => Task.FromResult(new EpssScore { CveId = cveId, Score = 0.0 });
    public Task<string?> GetCweForCveAsync(string cveId)
        => Task.FromResult<string?>(null);
}

public sealed class StoreBackedEvidenceTests
{
    private static (IntelligenceService intel, EvidenceService store) Setup()
    {
        var store = new EvidenceService();
        var intel = new IntelligenceService(new NeverAvailableProvider(), new PolicyEngine(), store);
        return (intel, store);
    }

    [Fact]
    public async Task GenerateEvidence_persists_with_real_tenant_and_verifies()
    {
        var (intel, store) = Setup();
        var tenant = Guid.NewGuid();
        var engagement = new Engagement
        {
            Id = Guid.NewGuid(), TenantId = tenant, Name = "p5",
            ScopeDefinition = "staging.example.test", Status = "Active"
        };
        var execution = new DomainExecution
        {
            Id = Guid.NewGuid(), EngagementId = engagement.Id,
            ModuleId = "http.security.headers", ModuleVersion = "1.2.0"
        };
        var evidence = await intel.GenerateEvidenceAsync(engagement, execution, "observation-bytes");
        Assert.NotEqual(Guid.Empty, evidence.TenantId);
        Assert.Equal(tenant, evidence.TenantId);
        Assert.Equal(engagement.Id, evidence.EngagementId);
        Assert.Equal(execution.Id, evidence.ExecutionId);
        Assert.True(await store.VerifyAsync(evidence.Id, tenant));
    }

    [Fact]
    public async Task GenerateEvidence_null_engagement_fails_closed()
    {
        var (intel, _) = Setup();
        var execution = new DomainExecution { Id = Guid.NewGuid(), ModuleId = "m", ModuleVersion = "1.0.0" };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => intel.GenerateEvidenceAsync(null!, execution, "bytes"));
    }

    [Fact]
    public async Task GenerateEvidence_tenant_mismatch_fails_closed()
    {
        var (intel, _) = Setup();
        var engagement = new Engagement
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Name = "p5",
            ScopeDefinition = "staging.example.test", Status = "Active"
        };
        // Execution claims a DIFFERENT engagement: binding mismatch, no record.
        var execution = new DomainExecution
        {
            Id = Guid.NewGuid(), EngagementId = Guid.NewGuid(),
            ModuleId = "m", ModuleVersion = "1.0.0"
        };
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => intel.GenerateEvidenceAsync(engagement, execution, "bytes"));
    }
}
