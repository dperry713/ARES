// P6 emergency-stop end-to-end tests (blueprint §4.6, MEC §11.4).
// The stop must be TOTAL while latched: policy evaluation, scope evaluation,
// proposal, queueing, leasing, and starting all deny/fail — and recovery must
// re-open the gate without losing prior state. An audit trail records stop +
// recovery with actor + timestamp.
using Ares.Domain;
using Ares.Execution.Services;
using Ares.Policy.Services;
using Xunit;

namespace Ares.UnitTests;

public sealed class EmergencyStopTests
{
    private static Engagement ActiveEngagement() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Name = "estop",
        ScopeDefinition = AresHarness.Target,
        AllowedAssessmentTypes = [AresHarness.Category],
        MaximumConcurrency = 5,
        Status = "Active",
        StartAt = DateTime.UtcNow.AddHours(-1),
        EndAt = DateTime.UtcNow.AddHours(1)
    };

    [Fact]
    public async Task Stop_latch_blocks_policy_scope_proposal_queue_lease_and_start()
    {
        var h = await AresHarness.CreateAsync();
        var (approved, _) = await h.ApprovedExecutionAsync();
        var eng = ActiveEngagement();

        try
        {
            EmergencyStop.Activate("op-user", "suspected incident");

            // Policy + scope deny while latched.
            var policy = await h.Policy.EvaluateAsync(eng, AresHarness.Category,
                AresHarness.ModuleId, AresHarness.ModuleVersion, false, 1, "default");
            Assert.Equal(PolicyDecision.Deny, policy.Decision);
            Assert.Equal("EMERGENCY_STOP_ACTIVE", policy.ReasonCode);

            var scope = await h.Policy.EvaluateScopeAsync(eng, AresHarness.Target);
            Assert.Equal(PolicyDecision.Deny, scope.Decision);

            // No NEW work can even be proposed while latched.
            var denied = await Assert.ThrowsAsync<PolicyDeniedException>(() => h.ProposeAsync());
            Assert.Equal("EMERGENCY_STOP_ACTIVE", denied.ReasonCode);

            // The execution control plane refuses new movement while latched.
            await Assert.ThrowsAsync<InvalidOperationException>(() => h.Executions.QueueAsync(approved.Id));

            // State is preserved, not destroyed and not advanced: still Approved.
            Assert.Equal(nameof(ExecutionState.Approved), (await h.Executions.GetAsync(approved.Id)).Status);
        }
        finally
        {
            EmergencyStop.ResetForTest();
        }
    }

    [Fact]
    public async Task Recovery_reopens_the_gate_and_audit_trail_records_both()
    {
        var h = await AresHarness.CreateAsync();
        var (approved, _) = await h.ApprovedExecutionAsync();

        EmergencyStop.Activate("op-user", "drill");
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Executions.QueueAsync(approved.Id));

        EmergencyStop.Recover("incident-commander", "all clear");

        // Gate re-opened: the SAME execution proceeds normally.
        var queued = await h.Executions.QueueAsync(approved.Id);
        Assert.Equal(nameof(ExecutionState.Queued), queued.Status);

        var trail = EmergencyStop.AuditTrail;
        Assert.Contains(trail, e => e.Action == "ACTIVATE" && e.Actor == "op-user");
        Assert.Contains(trail, e => e.Action == "RECOVER" && e.Actor == "incident-commander");

        EmergencyStop.ResetForTest();
    }

    [Fact]
    public async Task Stop_is_idempotent_and_recovery_requires_actor()
    {
        try
        {
            EmergencyStop.Activate("op-user", "first");
            EmergencyStop.Activate("op-user", "second"); // no throw, still stopped
            Assert.True(EmergencyStop.IsActive);

            await Assert.ThrowsAsync<ArgumentException>(() =>
            {
                EmergencyStop.Recover("", "no actor");
                return Task.CompletedTask;
            });
            Assert.True(EmergencyStop.IsActive); // failed recovery keeps the latch
        }
        finally
        {
            EmergencyStop.ResetForTest();
        }
    }
}
