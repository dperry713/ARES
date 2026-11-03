// P3 durable-queue tests (MEC §11.3, blueprint §5.1).
// The execution manager must provide, even in its in-memory milestone form:
//  (a) idempotent propose (same key -> same execution, no duplicate),
//  (b) lease + heartbeat (a leased execution that stops heartbeating expires
//      back to Queued so another worker can take it — no silent stuck jobs),
//  (c) cancellation propagation (cancel reaches Queued/Leased/Running and the
//      record shows who cancelled + when),
//  (d) dead-letter (an execution that exhausts retries with failures lands in
//      Failed with attempts preserved, instead of vanishing).
//
// Every execution is created against a real Active engagement (see TestHarness)
// because the F-02 fix re-reads engagement state on queue and start.
using Ares.Execution.Services;
using Xunit;

namespace Ares.UnitTests;

public sealed class DurableQueueTests
{
    [Fact]
    public async Task Propose_with_same_idempotency_key_returns_same_execution()
    {
        var h = await AresHarness.CreateAsync();
        var key = "idem-" + Guid.NewGuid();
        var hypothesis = Guid.NewGuid();

        var first = await h.ProposeAsync(idempotencyKey: key, hypothesisId: hypothesis);
        var second = await h.ProposeAsync(idempotencyKey: key, hypothesisId: hypothesis);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(nameof(ExecutionState.Proposed), second.Status);
        Assert.Single(await h.Executions.ListAsync(h.EngagementId));
    }

    [Fact]
    public async Task Propose_with_same_key_but_different_module_is_rejected()
    {
        var h = await AresHarness.CreateAsync();
        var key = "idem-" + Guid.NewGuid();

        await h.ProposeAsync(idempotencyKey: key);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.ProposeAsync(moduleId: "other.module", moduleVersion: "9.9.9", idempotencyKey: key));
    }

    [Fact]
    public async Task Lease_expires_without_heartbeat_and_execution_requeues()
    {
        var h = await AresHarness.CreateAsync();
        var execution = await h.QueuedExecutionAsync();

        var leased = await h.Executions.LeaseAsync(execution.Id, "worker-1", TimeSpan.FromMilliseconds(60));
        Assert.Equal("worker-1", leased.WorkerId);

        await Task.Delay(150); // lease lapses with no heartbeat
        var reclaimed = await h.Executions.ReclaimExpiredLeasesAsync(DateTimeOffset.UtcNow);

        Assert.Contains(execution.Id, reclaimed);
        Assert.Equal(ExecutionState.Queued, await h.Executions.GetStateAsync(execution.Id)); // visible again, not stuck
    }

    [Fact]
    public async Task Heartbeat_keeps_lease_alive()
    {
        var h = await AresHarness.CreateAsync();
        var execution = await h.QueuedExecutionAsync();

        await h.Executions.LeaseAsync(execution.Id, "worker-1", TimeSpan.FromMilliseconds(120));
        await Task.Delay(60);
        await h.Executions.HeartbeatAsync(execution.Id, "worker-1", TimeSpan.FromMilliseconds(200));
        await Task.Delay(100); // past the ORIGINAL expiry, inside the heartbeat extension

        var reclaimed = await h.Executions.ReclaimExpiredLeasesAsync(DateTimeOffset.UtcNow);
        Assert.DoesNotContain(execution.Id, reclaimed);

        var leased = await h.Executions.GetAsync(execution.Id);
        Assert.Equal("worker-1", leased.WorkerId);
        Assert.Equal(nameof(ExecutionState.Leased), leased.Status);
    }

    [Fact]
    public async Task Heartbeat_from_a_worker_that_does_not_hold_the_lease_is_rejected()
    {
        var h = await AresHarness.CreateAsync();
        var execution = await h.QueuedExecutionAsync();
        await h.Executions.LeaseAsync(execution.Id, "worker-1", TimeSpan.FromMinutes(5));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Executions.HeartbeatAsync(execution.Id, "worker-2", TimeSpan.FromMinutes(5)));

        Assert.Equal("worker-1", (await h.Executions.GetAsync(execution.Id)).WorkerId);
    }

    [Fact]
    public async Task Cancel_propagates_to_queued_and_running_with_audit_trail()
    {
        var h = await AresHarness.CreateAsync();

        var queued = await h.QueuedExecutionAsync();
        var cancelled = await h.Executions.CancelAsync(queued.Id, cancelledBy: "op-user", reason: "operator stop");
        Assert.Equal(nameof(ExecutionState.Cancelled), cancelled.Status);
        Assert.NotNull(cancelled.CompletedAt);
        Assert.Contains("op-user", cancelled.ResultReference);

        var running = await h.RunningExecutionAsync("worker-9");
        Assert.Equal(nameof(ExecutionState.Running), running.Status);

        var cancelled2 = await h.Executions.CancelAsync(running.Id, cancelledBy: "op-user", reason: "e-stop");
        Assert.Equal(nameof(ExecutionState.Cancelled), cancelled2.Status);

        // Terminal cancel is itself terminal: cancelling twice fails.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Executions.CancelAsync(running.Id, cancelledBy: "op-user", reason: "again"));
    }

    [Fact]
    public async Task Exhausted_retries_dead_letter_with_attempts_preserved()
    {
        var h = await AresHarness.CreateAsync();
        var running = await h.RunningExecutionAsync();

        await h.Executions.RecordAttemptFailureAsync(running.Id, "tool timeout", maxAttempts: 2);
        var retrying = await h.Executions.GetAsync(running.Id);
        Assert.Equal(nameof(ExecutionState.Queued), retrying.Status); // attempt 1 -> backoff requeue
        Assert.Equal(1, retrying.AttemptCount);

        await h.Executions.LeaseAsync(running.Id, "worker-2", TimeSpan.FromMinutes(5));
        await h.Executions.StartAsync(running.Id);
        await h.Executions.RecordAttemptFailureAsync(running.Id, "tool timeout again", maxAttempts: 2);

        var dead = await h.Executions.GetAsync(running.Id);
        Assert.Equal(nameof(ExecutionState.Failed), dead.Status); // attempt 2 exhausted -> dead-letter
        Assert.Equal(2, dead.AttemptCount);
        Assert.Contains("tool timeout again", dead.ResultReference ?? string.Empty);
    }

    [Fact]
    public async Task Completion_requires_an_evidence_reference()
    {
        var h = await AresHarness.CreateAsync();
        var running = await h.RunningExecutionAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => h.Executions.CompleteAsync(running.Id, "   "));
        Assert.Equal(nameof(ExecutionState.Running), (await h.Executions.GetAsync(running.Id)).Status);
    }

    [Fact]
    public async Task ListAsync_is_paginated_and_bounded()
    {
        var h = await AresHarness.CreateAsync();
        await h.ProposeAsync();
        await h.ProposeAsync();

        Assert.Equal(2, (await h.Executions.ListAsync(tenantId: h.TenantId, take: 50)).Count);
        Assert.Single(await h.Executions.ListAsync(tenantId: h.TenantId, take: 1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => h.Executions.ListAsync(take: 501));
    }
}
