// F-02 closure tests: the execution path re-evaluates policy against CURRENT
// engagement state at queue and start, not just at proposal time.
//
// Each test changes the world AFTER the approval was bound and then proves the
// transition is refused and the execution does not advance. A regression here
// re-opens audit finding F-02 ("approval authorized a stale world").
using Ares.Execution.Services;
using Xunit;

namespace Ares.UnitTests;

public sealed class PolicyReevaluationTests
{
    [Fact]
    public async Task Pausing_after_approval_blocks_queue_and_preserves_state()
    {
        var h = await AresHarness.CreateAsync();
        var (approved, _) = await h.ApprovedExecutionAsync();
        await h.PauseEngagementAsync();

        var denied = await Assert.ThrowsAsync<PolicyDeniedException>(
            () => h.Executions.QueueAsync(approved.Id));

        Assert.Equal("ENGAGEMENT_NOT_ACTIVE", denied.ReasonCode);
        Assert.Equal(nameof(ExecutionState.Approved), (await h.Executions.GetAsync(approved.Id)).Status);

        // Resuming re-opens the gate for the same execution: the denial was a
        // state decision, not corruption.
        await h.Engagements.ResumeAsync(h.EngagementId);
        var queued = await h.Executions.QueueAsync(approved.Id);
        Assert.Equal(nameof(ExecutionState.Queued), queued.Status);
    }

    [Fact]
    public async Task Pausing_after_queue_blocks_start()
    {
        var h = await AresHarness.CreateAsync();
        var queued = await h.QueuedExecutionAsync();
        await h.PauseEngagementAsync();

        var denied = await Assert.ThrowsAsync<PolicyDeniedException>(
            () => h.Executions.StartAsync(queued.Id));

        Assert.Equal("ENGAGEMENT_NOT_ACTIVE", denied.ReasonCode);
        Assert.Equal(nameof(ExecutionState.Queued), (await h.Executions.GetAsync(queued.Id)).Status);
    }

    [Fact]
    public async Task Category_outside_the_allow_list_is_refused_at_queue()
    {
        var h = await AresHarness.CreateAsync();

        // Proposal validates scope only, so this succeeds...
        var (approved, _) = await h.ApprovedExecutionAsync();

        // ...but a category the engagement never authorized cannot be queued.
        var execution = await h.ProposeAsync(category: "active_service_interaction");
        execution = await h.RequestApprovalAsync(execution.Id);
        var approval = await h.Approvals.RequestAsync(
            execution.Id, execution.EngagementId, execution.TenantId, execution.Target,
            execution.ModuleId, execution.ModuleVersion, execution.OperationHash,
            AresHarness.Requester, TimeSpan.FromMinutes(30));
        await h.Approvals.ApproveAsync(approval.Id, AresHarness.Approver);
        execution = await h.Executions.GrantApprovalAsync(execution.Id, approval.Id);

        var denied = await Assert.ThrowsAsync<PolicyDeniedException>(
            () => h.Executions.QueueAsync(execution.Id));
        Assert.Equal("CATEGORY_NOT_PERMITTED", denied.ReasonCode);

        // The control execution (allowed category) is unaffected.
        Assert.Equal(nameof(ExecutionState.Approved), approved.Status);
    }

    [Fact]
    public async Task Queue_outside_the_authorized_window_is_denied()
    {
        // Window opens in the future: proposal (scope-only) succeeds, execution
        // is approved, but queueing before the window opens is unauthorized testing.
        var h = await AresHarness.CreateAsync(
            startAt: DateTime.UtcNow.AddHours(1),
            endAt: DateTime.UtcNow.AddHours(2));

        var (approved, _) = await h.ApprovedExecutionAsync();
        var denied = await Assert.ThrowsAsync<PolicyDeniedException>(
            () => h.Executions.QueueAsync(approved.Id));

        Assert.Equal("ENGAGEMENT_WINDOW_CLOSED", denied.ReasonCode);
        Assert.Equal(nameof(ExecutionState.Approved), (await h.Executions.GetAsync(approved.Id)).Status);
    }

    [Fact]
    public async Task Expired_approval_blocks_queue_even_though_it_was_granted_in_time()
    {
        var h = await AresHarness.CreateAsync();
        var (approved, _) = await h.ApprovedExecutionAsync(ttl: TimeSpan.FromMilliseconds(400));
        Assert.Equal(nameof(ExecutionState.Approved), approved.Status);
        Assert.NotNull(approved.ApprovalExpiresAt);

        await Task.Delay(700); // approval expires after the grant

        var denied = await Assert.ThrowsAsync<PolicyDeniedException>(
            () => h.Executions.QueueAsync(approved.Id));

        Assert.Equal("APPROVAL_EXPIRED", denied.ReasonCode);
        Assert.Equal(nameof(ExecutionState.Approved), (await h.Executions.GetAsync(approved.Id)).Status);
    }

    [Fact]
    public async Task Queue_and_start_stamp_distinct_permit_decisions()
    {
        var h = await AresHarness.CreateAsync();
        var queued = await h.QueuedExecutionAsync();
        var started = await h.Executions.StartAsync(queued.Id);

        Assert.Equal("PERMIT", queued.PolicyReasonCode);
        Assert.Equal("PERMIT", started.PolicyReasonCode);
        Assert.NotNull(queued.PolicyDecisionId);
        Assert.NotNull(started.PolicyDecisionId);
        // Each transition produces its own evaluation id, so the audit trail can
        // answer "which decision permitted THIS step".
        Assert.NotEqual(queued.PolicyDecisionId, started.PolicyDecisionId);
        Assert.NotNull(started.PolicyEvaluatedAt);
    }

    [Fact]
    public async Task Queue_is_refused_while_the_global_emergency_stop_is_latched()
    {
        var h = await AresHarness.CreateAsync();
        var queued = await h.QueuedExecutionAsync();

        try
        {
            EmergencyStop.Activate("test", "latched");
            await Assert.ThrowsAsync<InvalidOperationException>(() => h.Executions.StartAsync(queued.Id));
            Assert.Equal(nameof(ExecutionState.Queued), (await h.Executions.GetAsync(queued.Id)).Status);
        }
        finally
        {
            EmergencyStop.ResetForTest();
        }
    }
}
