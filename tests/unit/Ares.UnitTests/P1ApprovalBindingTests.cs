// P1 approval-binding tests (MEC §10.3, blueprint §4.5).
// Prove the gap FIRST: these reference the approval store + bound-evaluation
// APIs that do not exist yet, so this file fails to compile until implemented.
// After implementation all tests must PASS with no weakening.
using Ares.Domain;
using Ares.Engagements;
using Ares.Policy.Services;
using Ares.Approvals.Services;
using Xunit;

namespace Ares.UnitTests;

public sealed class ApprovalBindingTests
{
    private static Engagement ActiveEngagement(Guid tenantId) => new Engagement
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = "p1",
        ScopeDefinition = "staging.example.test",
        AllowedAssessmentTypes = new[] { "safe_http_configuration_checks" },
        MaximumConcurrency = 5,
        Status = "Active",
        // The policy engine enforces the authorized assessment window (MEC §6.1).
        StartAt = DateTime.UtcNow.AddHours(-1),
        EndAt = DateTime.UtcNow.AddHours(1)
    };

    [Fact]
    public async Task Approval_is_bound_to_exact_operation_hash_material_change_invalidates()
    {
        var approvals = new ApprovalService();
        var tenant = Guid.NewGuid();
        var eng = ActiveEngagement(tenant);
        var execId = Guid.NewGuid();
        var rec = await approvals.RequestAsync(execId, eng.Id, tenant, "staging.example.test",
            "http.security.headers", "1.2.0", "ophash-aaa", "op-user", TimeSpan.FromMinutes(30));
        rec = await approvals.ApproveAsync(rec.Id, "human-lead");

        // Same hash validates.
        var ok = await approvals.ValidateForExecutionAsync(execId, eng.Id, tenant,
            "staging.example.test", "http.security.headers", "1.2.0", "ophash-aaa", DateTimeOffset.UtcNow);
        Assert.Equal("Approved", ok.Decision);

        // Material change (module version bump) invalidates — even with same approval id.
        await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.ValidateForExecutionAsync(
            execId, eng.Id, tenant, "staging.example.test", "http.security.headers",
            "9.9.9", "ophash-aaa", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Expired_approval_is_rejected_fail_closed()
    {
        var approvals = new ApprovalService();
        var tenant = Guid.NewGuid();
        var eng = ActiveEngagement(tenant);
        var execId = Guid.NewGuid();
        var rec = await approvals.RequestAsync(execId, eng.Id, tenant, "staging.example.test",
            "http.security.headers", "1.2.0", "ophash-bbb", "op-user", TimeSpan.FromMilliseconds(50));
        await Task.Delay(120); // let the bounded TTL lapse — expiry must fail closed
        await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.ApproveAsync(rec.Id, "human-lead"));
        // And a stale Approved record (e.g. approved just before expiry, consumed after)
        // must also fail at validation time, not only at approve time.
        var rec2 = await approvals.RequestAsync(Guid.NewGuid(), eng.Id, tenant, "staging.example.test",
            "http.security.headers", "1.2.0", "ophash-bbb2", "op-user", TimeSpan.FromMilliseconds(50));
        rec2 = await approvals.ApproveAsync(rec2.Id, "human-lead");
        await Task.Delay(120);
        await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.ValidateForExecutionAsync(
            rec2.ExecutionId, eng.Id, tenant, "staging.example.test", "http.security.headers",
            "1.2.0", "ophash-bbb2", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Single_use_approval_cannot_be_reused()
    {
        var approvals = new ApprovalService();
        var tenant = Guid.NewGuid();
        var eng = ActiveEngagement(tenant);
        var execId = Guid.NewGuid();
        var rec = await approvals.RequestAsync(execId, eng.Id, tenant, "staging.example.test",
            "http.security.headers", "1.2.0", "ophash-ccc", "op-user", TimeSpan.FromMinutes(30), maxUses: 1);
        await approvals.ApproveAsync(rec.Id, "human-lead");
        var first = await approvals.ValidateForExecutionAsync(execId, eng.Id, tenant,
            "staging.example.test", "http.security.headers", "1.2.0", "ophash-ccc", DateTimeOffset.UtcNow);
        Assert.Equal("Approved", first.Decision);
        await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.ValidateForExecutionAsync(
            execId, eng.Id, tenant, "staging.example.test", "http.security.headers",
            "1.2.0", "ophash-ccc", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Approval_for_execution_A_cannot_authorize_execution_B()
    {
        var approvals = new ApprovalService();
        var tenant = Guid.NewGuid();
        var eng = ActiveEngagement(tenant);
        var execA = Guid.NewGuid();
        var rec = await approvals.RequestAsync(execA, eng.Id, tenant, "staging.example.test",
            "http.security.headers", "1.2.0", "ophash-ddd", "op-user", TimeSpan.FromMinutes(30));
        await approvals.ApproveAsync(rec.Id, "human-lead");
        await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.ValidateForExecutionAsync(
            Guid.NewGuid(), eng.Id, tenant, "staging.example.test", "http.security.headers",
            "1.2.0", "ophash-ddd", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Policy_with_valid_bound_approval_permits_otherwise_requires_approval()
    {
        var engine = new PolicyEngine();
        var approvals = new ApprovalService();
        var tenant = Guid.NewGuid();
        var eng = ActiveEngagement(tenant);
        var execId = Guid.NewGuid();
        var rec = await approvals.RequestAsync(execId, eng.Id, tenant, "staging.example.test",
            "http.security.headers", "1.2.0", "ophash-eee", "op-user", TimeSpan.FromMinutes(30));
        rec = await approvals.ApproveAsync(rec.Id, "human-lead");

        var without = await engine.EvaluateWithApprovalAsync(eng, "safe_http_configuration_checks",
            "http.security.headers", "1.2.0", requiresHumanApproval: true, maximumConcurrency: 1,
            rateLimits: "default", approval: null);
        Assert.Equal(PolicyDecision.RequireApproval, without.Decision);

        var with = await engine.EvaluateWithApprovalAsync(eng, "safe_http_configuration_checks",
            "http.security.headers", "1.2.0", requiresHumanApproval: true, maximumConcurrency: 1,
            rateLimits: "default", approval: rec);
        Assert.Equal(PolicyDecision.Permit, with.Decision);
    }
}
