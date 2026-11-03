// P2 negative tests: fail-closed null-engagement behavior (MEC §10, KD-02).
// Written FIRST (RED): GenerateHypothesesAsync currently throws raw
// ArgumentNullException via ThrowIfNull. The test pins the REQUIRED contract:
// fail closed with a domain InvalidOperationException naming the missing
// engagement — never a NullReference, never silent hypotheses.
using Ares.Domain;
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

public sealed class NullEngagementFailClosedTests
{
    [Fact]
    public async Task GenerateHypothesesAsync_null_engagement_fails_closed_with_domain_error()
    {
        var svc = new IntelligenceService(new NeverAvailableProvider(), new PolicyEngine());
        var asset = new Asset
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(),
            CanonicalIdentifier = "staging.example.test"
        };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.GenerateHypothesesAsync(null!, asset, "recon"));
        Assert.Contains("engagement", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_null_engagement_denies_scope_without_throwing()
    {
        var svc = new IntelligenceService(new NeverAvailableProvider(), new PolicyEngine());
        var asset = new Asset
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(),
            CanonicalIdentifier = "staging.example.test"
        };
        // Must not throw NullReferenceException: fail closed, record denial.
        var vuln = await svc.AnalyzeAsync(null, asset, "recon");
        Assert.NotNull(vuln);
    }
}
