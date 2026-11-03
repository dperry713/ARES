// Separation-of-duties tests (MEC §10.3).
//
// A single credential must never be able to both request and release a security
// test. This is enforced in the approval store against the *requesting subject*,
// not against a caller-supplied string.
using Ares.Execution.Services;
using Xunit;

namespace Ares.UnitTests;

public sealed class SeparationOfDutiesTests
{
    [Fact]
    public async Task The_requesting_subject_cannot_approve_its_own_request()
    {
        var h = await AresHarness.CreateAsync();
        var execution = await h.ProposeAsync();
        execution = await h.RequestApprovalAsync(execution.Id);

        var approval = await h.Approvals.RequestAsync(
            execution.Id, execution.EngagementId, execution.TenantId, execution.Target,
            execution.ModuleId, execution.ModuleVersion, execution.OperationHash,
            AresHarness.Requester, TimeSpan.FromMinutes(30));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Approvals.ApproveAsync(approval.Id, AresHarness.Requester));
        Assert.Contains("Separation of duties", error.Message, StringComparison.Ordinal);

        // A different human still can.
        var approved = await h.Approvals.ApproveAsync(approval.Id, AresHarness.Approver);
        Assert.Equal("Approved", approved.Decision);
        Assert.Equal(AresHarness.Approver, approved.ApprovedBy);
    }

    [Fact]
    public async Task Self_approval_through_the_harness_is_impossible()
    {
        var h = await AresHarness.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.ApprovedExecutionAsync(requester: "solo-operator", approver: "solo-operator"));
    }

    [Fact]
    public async Task Rejection_is_recorded_with_a_reason_and_never_advances_the_execution()
    {
        var h = await AresHarness.CreateAsync();
        var execution = await h.ProposeAsync();
        execution = await h.RequestApprovalAsync(execution.Id);

        var approval = await h.Approvals.RequestAsync(
            execution.Id, execution.EngagementId, execution.TenantId, execution.Target,
            execution.ModuleId, execution.ModuleVersion, execution.OperationHash,
            AresHarness.Requester, TimeSpan.FromMinutes(30));

        var rejected = await h.Approvals.RejectAsync(approval.Id, AresHarness.Approver, "insufficient justification");
        Assert.Equal("Rejected", rejected.Decision);
        Assert.Equal("insufficient justification", rejected.Reason);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Executions.GrantApprovalAsync(execution.Id, approval.Id));
        Assert.Equal(nameof(ExecutionState.AwaitingApproval),
            (await h.Executions.GetAsync(execution.Id)).Status);
    }

    [Fact]
    public async Task Approval_ttl_is_capped_at_twenty_four_hours()
    {
        var h = await AresHarness.CreateAsync();
        var execution = await h.ProposeAsync();
        execution = await h.RequestApprovalAsync(execution.Id);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => h.Approvals.RequestAsync(
            execution.Id, execution.EngagementId, execution.TenantId, execution.Target,
            execution.ModuleId, execution.ModuleVersion, execution.OperationHash,
            AresHarness.Requester, TimeSpan.FromHours(25)));
    }

    [Fact]
    public async Task Approval_cannot_be_reused_after_the_grant_consumed_it()
    {
        var h = await AresHarness.CreateAsync();
        var (approved, approval) = await h.ApprovedExecutionAsync();

        // Second execution may not reuse the same approval id.
        var second = await h.ProposeAsync();
        second = await h.RequestApprovalAsync(second.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Executions.GrantApprovalAsync(second.Id, approval.Id));

        Assert.Equal(nameof(ExecutionState.Approved), (await h.Executions.GetAsync(approved.Id)).Status);
        Assert.Equal(nameof(ExecutionState.AwaitingApproval), (await h.Executions.GetAsync(second.Id)).Status);
    }
}
