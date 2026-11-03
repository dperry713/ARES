// ARES P0 fail-closed audit tests.
// Each test names the MEC requirement it guards. At audit time these tests
// PROVE the gaps: they currently FAIL against the implementation.
// After the fix they must all PASS. No test may be weakened to pass.
using Ares.Domain;
using Ares.Engagements;
using Ares.Policy.Services;
using Ares.Execution.Services;
using Xunit;

namespace Ares.UnitTests;

// MEC §10.1: checks 1+2 — an engagement that is merely Authorized (not Active)
// must NOT receive Permit for execution. Current PolicyEngine permits it.
public sealed class PolicyEngineFailClosedTests
{
    private static Engagement AuthorizedButNotActive() => new Engagement
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Name = "audit",
        ScopeDefinition = "staging.example.test",
        AllowedAssessmentTypes = new[] { "safe_http_configuration_checks" },
        Status = "Authorized"
    };

    [Fact]
    public async Task EvaluateAsync_authorized_but_not_active_must_not_permit()
    {
        var engine = new PolicyEngine();
        var result = await engine.EvaluateAsync(
            AuthorizedButNotActive(), "safe_http_configuration_checks",
            "http.security.headers", "1.2.0",
            requiresHumanApproval: false, maximumConcurrency: 1, rateLimits: "default");
        Assert.True(
            result.Decision == PolicyDecision.Deny || result.Decision == PolicyDecision.RequireApproval,
            $"MEC §10.1 checks 1+2: Authorized-but-not-Active engagement must not be Permitted (got {result.Decision}).");
    }

    // MEC §10.2: any approval-gated operation without a real approval must not Permit.
    [Fact]
    public async Task EvaluateAsync_requires_approval_without_approval_must_not_permit()
    {
        var engine = new PolicyEngine();
        var engagement = AuthorizedButNotActive() with { Status = "Active" };
        var result = await engine.EvaluateAsync(
            engagement, "active_service_interaction",
            "http.security.headers", "1.2.0",
            requiresHumanApproval: true, maximumConcurrency: 1, rateLimits: "default");
        Assert.True(
            result.Decision == PolicyDecision.Deny || result.Decision == PolicyDecision.RequireApproval,
            $"MEC §10.2/§10.3: approval-gated operation without approval must not Permit (got {result.Decision}).");
    }

    // Blueprint §4.3: excluded targets must be denied, even when scope text exists.
    [Fact]
    public async Task EvaluateScopeAsync_excluded_target_must_be_denied()
    {
        var engine = new PolicyEngine();
        var engagement = AuthorizedButNotActive() with
        {
            Status = "Active",
            ExcludedTargets = new[] { "admin.staging.example.test" }
        };
        var result = await engine.EvaluateScopeAsync(engagement, "admin.staging.example.test");
        Assert.Equal(PolicyDecision.Deny, result.Decision);
    }

    // Blueprint §4.3: out-of-scope targets must be denied.
    [Fact]
    public async Task EvaluateScopeAsync_out_of_scope_target_must_be_denied()
    {
        var engine = new PolicyEngine();
        var engagement = AuthorizedButNotActive() with { Status = "Active" };
        var result = await engine.EvaluateScopeAsync(engagement, "evil.example.net");
        Assert.Equal(PolicyDecision.Deny, result.Decision);
    }

    // Blueprint §4.3: in-scope target must still be permitted (no over-blocking).
    [Fact]
    public async Task EvaluateScopeAsync_in_scope_target_must_be_permitted()
    {
        var engine = new PolicyEngine();
        var engagement = AuthorizedButNotActive() with { Status = "Active" };
        var result = await engine.EvaluateScopeAsync(engagement, "staging.example.test");
        Assert.Equal(PolicyDecision.Permit, result.Decision);
    }
}

// MEC §6.2: engagement lifecycle must follow
// Draft -> Authorized -> Active -> Paused -> Active -> Completed (-> Archived)
// and must reject invalid jumps. Current service allows any transition.
public sealed class EngagementLifecycleTests
{
    [Fact]
    public async Task StartAsync_from_draft_must_be_rejected()
    {
        var svc = new EngagementService();
        var e = await svc.CreateAsync(Guid.NewGuid(), "n", "d", "AR-1",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(1), "scope",
            new[] { "web" }, Array.Empty<string>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.StartAsync(e.Id));
    }

    [Fact]
    public async Task PauseAsync_from_draft_must_be_rejected()
    {
        var svc = new EngagementService();
        var e = await svc.CreateAsync(Guid.NewGuid(), "n", "d", "AR-1",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(1), "scope",
            new[] { "web" }, Array.Empty<string>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.PauseAsync(e.Id));
    }

    [Fact]
    public async Task AuthorizeAsync_is_idempotent_only_from_draft_or_authorized()
    {
        var svc = new EngagementService();
        var e = await svc.CreateAsync(Guid.NewGuid(), "n", "d", "AR-1",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(1), "scope",
            new[] { "web" }, Array.Empty<string>());
        e = await svc.AuthorizeAsync(e.Id);
        Assert.Equal("Authorized", e.Status);
        // Completing then re-authorizing must fail: terminal states are terminal.
        e = await svc.StartAsync(e.Id);
        await svc.CompleteAsync(e.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AuthorizeAsync(e.Id));
    }

    [Fact]
    public async Task ResumeAsync_from_active_must_be_rejected()
    {
        var svc = new EngagementService();
        var e = await svc.CreateAsync(Guid.NewGuid(), "n", "d", "AR-1",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(1), "scope",
            new[] { "web" }, Array.Empty<string>());
        e = await svc.AuthorizeAsync(e.Id);
        e = await svc.StartAsync(e.Id);
        Assert.Equal("Active", e.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ResumeAsync(e.Id));
    }
}

// MEC §6.2: execution lifecycle must follow
// Proposed -> AwaitingApproval -> Approved -> Queued -> Running -> Verifying -> Completed.
// Wired end to end: GrantApprovalAsync validates through the approval store —
// a raw Guid never authorizes anything. MEC §10.3 / §15.7.
//
// Since the F-02 fix every execution transition reads the engagement back from
// the engagement service, so the tests build a real Active engagement in its
// authorized window (see TestHarness) instead of fabricating ids.
public sealed class ExecutionApprovalBindingTests
{
    [Fact]
    public async Task ProposeAsync_returns_a_trackable_handle_with_Proposed_status()
    {
        var h = await AresHarness.CreateAsync();
        var execution = await h.ProposeAsync();
        Assert.NotEqual(Guid.Empty, execution.Id);
        Assert.Equal(nameof(ExecutionState.Proposed), execution.Status);
        Assert.Equal(h.TenantId, execution.TenantId);
        Assert.NotEqual(string.Empty, execution.OperationHash);

        var fetched = await h.Executions.GetAsync(execution.Id);
        Assert.Equal(execution.Id, fetched.Id);
    }

    [Fact]
    public async Task ProposeAsync_refuses_an_out_of_scope_target()
    {
        var h = await AresHarness.CreateAsync();
        var denied = await Assert.ThrowsAsync<PolicyDeniedException>(
            () => h.ProposeAsync(target: "evil.example.net"));
        Assert.Equal("OUT_OF_SCOPE", denied.ReasonCode);
    }

    [Fact]
    public async Task Queueing_without_a_bound_approval_is_rejected()
    {
        var h = await AresHarness.CreateAsync();
        var execution = await h.ProposeAsync();
        execution = await h.RequestApprovalAsync(execution.Id);
        Assert.Equal(nameof(ExecutionState.AwaitingApproval), execution.Status);
        // Status alone is not authorization: there is no bound approval yet.
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Executions.QueueAsync(execution.Id));
    }

    [Fact]
    public async Task Full_lifecycle_passes_through_the_Approved_state()
    {
        var h = await AresHarness.CreateAsync();
        var (approved, approval) = await h.ApprovedExecutionAsync();

        Assert.Equal(nameof(ExecutionState.Approved), approved.Status);
        Assert.Equal(approval.Id, approved.ApprovalId);
        Assert.Equal(AresHarness.Approver, approved.ApprovedBy);

        var execution = await h.Executions.QueueAsync(approved.Id);
        Assert.Equal(nameof(ExecutionState.Queued), execution.Status);
        // F-02: the permitting policy decision is stamped, not implied.
        Assert.Equal("PERMIT", execution.PolicyReasonCode);
        Assert.NotNull(execution.PolicyDecisionId);
        Assert.NotNull(execution.PolicyEvaluatedAt);

        execution = await h.Executions.StartAsync(execution.Id);
        Assert.Equal(nameof(ExecutionState.Running), execution.Status);
        Assert.NotNull(execution.PolicyDecisionId);

        execution = await h.Executions.CompleteAsync(execution.Id, "result-ref");
        Assert.Equal(nameof(ExecutionState.Verifying), execution.Status);
        execution = await h.Executions.VerifyAsync(execution.Id);
        Assert.Equal(nameof(ExecutionState.Completed), execution.Status);
    }

    [Fact]
    public async Task Grant_with_forged_binding_or_forged_approval_id_is_rejected_execution_stays_put()
    {
        var h = await AresHarness.CreateAsync();
        var execution = await h.ProposeAsync();
        execution = await h.RequestApprovalAsync(execution.Id);

        var approval = await h.Approvals.RequestAsync(
            execution.Id, execution.EngagementId, execution.TenantId, execution.Target,
            execution.ModuleId, execution.ModuleVersion, execution.OperationHash,
            AresHarness.Requester, TimeSpan.FromMinutes(30));
        approval = await h.Approvals.ApproveAsync(approval.Id, AresHarness.Approver);

        var realBinding = new ExecutionOperationBinding(execution.EngagementId, execution.TenantId,
            execution.Target, execution.OperationHash);

        // Forged approval id with an otherwise-correct binding.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Executions.GrantApprovalAsync(execution.Id, Guid.NewGuid(), realBinding));

        // Correct id, tampered operation hash (material change).
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Executions.GrantApprovalAsync(
            execution.Id, approval.Id,
            realBinding with { OperationHash = "tampered-operation-hash" }));

        // Correct id, different target (material change).
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Executions.GrantApprovalAsync(
            execution.Id, approval.Id, realBinding with { Target = "evil.example.net" }));

        // Correct id, wrong tenant (cross-tenant attempt).
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Executions.GrantApprovalAsync(
            execution.Id, approval.Id, realBinding with { TenantId = Guid.NewGuid() }));

        Assert.Equal(nameof(ExecutionState.AwaitingApproval), (await h.Executions.GetAsync(execution.Id)).Status);

        // The untouched approval still works with the exact stored binding.
        var granted = await h.Executions.GrantApprovalAsync(execution.Id, approval.Id);
        Assert.Equal(nameof(ExecutionState.Approved), granted.Status);
    }

    [Fact]
    public async Task Grant_by_a_rejected_or_pending_approval_is_rejected()
    {
        var h = await AresHarness.CreateAsync();
        var execution = await h.ProposeAsync();
        execution = await h.RequestApprovalAsync(execution.Id);

        var pending = await h.Approvals.RequestAsync(
            execution.Id, execution.EngagementId, execution.TenantId, execution.Target,
            execution.ModuleId, execution.ModuleVersion, execution.OperationHash,
            AresHarness.Requester, TimeSpan.FromMinutes(30));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Executions.GrantApprovalAsync(execution.Id, pending.Id));
        Assert.Equal(nameof(ExecutionState.AwaitingApproval), (await h.Executions.GetAsync(execution.Id)).Status);

        await h.Approvals.RejectAsync(pending.Id, AresHarness.Approver, "not justified");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Executions.GrantApprovalAsync(execution.Id, pending.Id));
        Assert.Equal(nameof(ExecutionState.AwaitingApproval), (await h.Executions.GetAsync(execution.Id)).Status);
    }
}
