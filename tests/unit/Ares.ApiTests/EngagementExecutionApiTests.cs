// End-to-end HTTP tests for the authorized-execution path (MEC §15.2/§15.7).
//
// These drive the real host: create -> authorize -> start -> propose ->
// request-approval -> approval record -> approve -> grant -> queue -> lease ->
// start -> complete -> verify, plus the fail-closed paths (invalid transition
// = 409, unknown id = 404, forged grant = 409, double-cancel = 409) and the
// F-02 property that policy is re-evaluated when work is actually released.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Ares.ApiTests;

public sealed class EngagementExecutionApiTests : IClassFixture<AresApiFactory>
{
    private readonly AresApiFactory _factory;

    public EngagementExecutionApiTests(AresApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Engagement_lifecycle_over_http()
    {
        var manager = _factory.ClientFor(_factory.ManagerAKey);
        var id = await ApiFlow.CreateActiveEngagementAsync(manager, "lifecycle");

        foreach (var action in new[] { "pause", "resume", "complete" })
        {
            var response = await manager.PostAsync($"/api/v1/engagements/{id}/{action}", null);
            Assert.True(response.IsSuccessStatusCode, $"{action} failed: {(int)response.StatusCode}");
        }

        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/api/v1/engagements/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await manager.GetAsync($"/api/v1/engagements/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_transition_is_409_not_500()
    {
        var manager = _factory.ClientFor(_factory.ManagerAKey);
        var created = await manager.PostAsJsonAsync("/api/v1/engagements", new
        {
            name = "invalid-transition",
            description = "d",
            authorizationReference = "AR-BAD-1",
            startAt = DateTime.UtcNow,
            endAt = DateTime.UtcNow.AddHours(1),
            scopeDefinition = ApiFlow.Target,
            allowedAssessmentTypes = new[] { ApiFlow.Category },
            excludedTargets = Array.Empty<string>()
        });
        var engagement = await created.Content.ReadFromJsonAsync<IdBody>();

        // Draft -> start without authorize must conflict, not crash.
        var response = await manager.PostAsync($"/api/v1/engagements/{engagement!.Id}/start", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Full_authorized_execution_path_completes_with_stamped_policy_decisions()
    {
        var manager = _factory.ClientFor(_factory.ManagerAKey);
        var approver = _factory.ClientFor(_factory.ApproverAKey);
        var worker = _factory.ClientFor(_factory.WorkerAKey);

        var engagementId = await ApiFlow.CreateActiveEngagementAsync(manager, "full-path");
        var (executionId, approvalId) = await ApiFlow.AwaitingApprovalAsync(manager, engagementId);

        // Queueing before the approval is granted is refused (409: wrong state).
        Assert.Equal(HttpStatusCode.Conflict,
            (await manager.PostAsync($"/api/v1/executions/{executionId}/queue", null)).StatusCode);

        await ApiFlow.ApproveAsync(approver, approvalId);
        var granted = await ApiFlow.GrantAsync(worker, executionId, approvalId);
        Assert.Equal("Approved", granted.Status);

        var queued = await ApiFlow.PostExecutionActionAsync(worker, executionId, "queue");
        Assert.Equal("Queued", queued.Status);
        Assert.Equal("PERMIT", queued.PolicyReasonCode);

        var leased = await ApiFlow.PostExecutionActionAsync(worker, executionId, "lease",
            new { workerId = "worker-a-1", leaseSeconds = 120 });
        Assert.Equal("Leased", leased.Status);

        var started = await ApiFlow.PostExecutionActionAsync(worker, executionId, "start");
        Assert.Equal("Running", started.Status);
        Assert.Equal("PERMIT", started.PolicyReasonCode);

        var verifying = await ApiFlow.PostExecutionActionAsync(worker, executionId, "complete",
            new { resultReference = "evidence://run-1" });
        Assert.Equal("Verifying", verifying.Status);

        var completed = await ApiFlow.PostExecutionActionAsync(worker, executionId, "verify");
        Assert.Equal("Completed", completed.Status);
    }

    [Fact]
    public async Task Forged_grant_and_double_cancel_are_refused()
    {
        var manager = _factory.ClientFor(_factory.ManagerAKey);
        var approver = _factory.ClientFor(_factory.ApproverAKey);
        var worker = _factory.ClientFor(_factory.WorkerAKey);

        var engagementId = await ApiFlow.CreateActiveEngagementAsync(manager, "forged-grant");
        var (executionId, approvalId) = await ApiFlow.AwaitingApprovalAsync(manager, engagementId);
        await ApiFlow.ApproveAsync(approver, approvalId);

        // A random approval id cannot authorize anything.
        var forged = await worker.PostAsJsonAsync($"/api/v1/executions/{executionId}/grant",
            new { approvalId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, forged.StatusCode);

        // Real grant, then cancel; a second cancel is 409.
        await ApiFlow.GrantAsync(worker, executionId, approvalId);
        Assert.Equal(HttpStatusCode.OK,
            (await worker.PostAsJsonAsync($"/api/v1/executions/{executionId}/cancel",
                new { reason = "done" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await worker.PostAsJsonAsync($"/api/v1/executions/{executionId}/cancel",
                new { reason = "again" })).StatusCode);

        // Missing execution is 404, not 500.
        Assert.Equal(HttpStatusCode.NotFound,
            (await worker.GetAsync($"/api/v1/executions/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task F02_pausing_the_engagement_after_grant_blocks_queue_with_403()
    {
        var manager = _factory.ClientFor(_factory.ManagerAKey);
        var approver = _factory.ClientFor(_factory.ApproverAKey);
        var worker = _factory.ClientFor(_factory.WorkerAKey);

        var engagementId = await ApiFlow.CreateActiveEngagementAsync(manager, "f02");
        var (executionId, approvalId) = await ApiFlow.AwaitingApprovalAsync(manager, engagementId);
        await ApiFlow.ApproveAsync(approver, approvalId);
        await ApiFlow.GrantAsync(worker, executionId, approvalId);

        // The world changes after the approval was bound: the engagement is paused.
        Assert.Equal(HttpStatusCode.OK,
            (await manager.PostAsync($"/api/v1/engagements/{engagementId}/pause", null)).StatusCode);

        var denied = await worker.PostAsync($"/api/v1/executions/{executionId}/queue", null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var problem = JsonDocument.Parse(await denied.Content.ReadAsStringAsync());
        Assert.Equal("ENGAGEMENT_NOT_ACTIVE",
            problem.RootElement.GetProperty("reasonCode").GetString());
        Assert.Equal("https://ares.local/errors/policy-denied",
            problem.RootElement.GetProperty("type").GetString());

        // The execution did not advance.
        var current = await worker.GetAsync($"/api/v1/executions/{executionId}");
        var execution = await current.Content.ReadFromJsonAsync<ExecutionBody>();
        Assert.Equal("Approved", execution!.Status);
    }

    [Fact]
    public async Task F02_an_unapproved_category_is_refused_at_queue()
    {
        var manager = _factory.ClientFor(_factory.ManagerAKey);
        var approver = _factory.ClientFor(_factory.ApproverAKey);
        var worker = _factory.ClientFor(_factory.WorkerAKey);

        var engagementId = await ApiFlow.CreateActiveEngagementAsync(manager, "category");
        var executionId = await ApiFlow.ProposeAsync(manager, engagementId,
            category: "active_service_interaction");
        await manager.PostAsync($"/api/v1/executions/{executionId}/request-approval", null);

        var request = await manager.PostAsJsonAsync($"/api/v1/executions/{executionId}/approval-requests",
            new { ttlMinutes = 30, maxUses = 1 });
        var approval = await request.Content.ReadFromJsonAsync<IdBody>();
        await ApiFlow.ApproveAsync(approver, approval!.Id);
        await ApiFlow.GrantAsync(worker, executionId, approval.Id);

        var denied = await worker.PostAsync($"/api/v1/executions/{executionId}/queue", null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var problem = JsonDocument.Parse(await denied.Content.ReadAsStringAsync());
        Assert.Equal("CATEGORY_NOT_PERMITTED",
            problem.RootElement.GetProperty("reasonCode").GetString());
    }

    [Fact]
    public async Task Approval_cannot_be_requested_for_an_unrequested_execution()
    {
        var manager = _factory.ClientFor(_factory.ManagerAKey);
        var engagementId = await ApiFlow.CreateActiveEngagementAsync(manager, "approval-state");
        var executionId = await ApiFlow.ProposeAsync(manager, engagementId);

        // Still Proposed: the operator must move it to AwaitingApproval first.
        var response = await manager.PostAsJsonAsync(
            $"/api/v1/executions/{executionId}/approval-requests",
            new { ttlMinutes = 30, maxUses = 1 });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Audit_trail_and_chain_integrity_are_readable_by_an_auditor()
    {
        var manager = _factory.ClientFor(_factory.ManagerAKey);
        var auditor = _factory.ClientFor(_factory.AuditorAKey);
        var admin = _factory.ClientFor(_factory.AdminKey);

        var engagementId = await ApiFlow.CreateActiveEngagementAsync(manager, "audit");
        await ApiFlow.ProposeAsync(manager, engagementId);

        var integrity = await auditor.GetAsync("/api/v1/audit/integrity");
        Assert.Equal(HttpStatusCode.OK, integrity.StatusCode);
        using var document = JsonDocument.Parse(await integrity.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("intact").GetBoolean());
        Assert.True(document.RootElement.GetProperty("entriesVerified").GetInt64() > 0);
        Assert.True(document.RootElement.GetProperty("entries").GetInt64() > 0);

        var trail = await auditor.GetAsync("/api/v1/audit?limit=50");
        Assert.Equal(HttpStatusCode.OK, trail.StatusCode);
        var text = await trail.Content.ReadAsStringAsync();
        Assert.Contains("engagement.created", text, StringComparison.Ordinal);
        Assert.Contains("execution.proposed", text, StringComparison.Ordinal);

        // System info proves identity + chain health for the caller.
        var info = await admin.GetAsync("/api/v1/system/info");
        Assert.Equal(HttpStatusCode.OK, info.StatusCode);
        using var infoDocument = JsonDocument.Parse(await info.Content.ReadAsStringAsync());
        Assert.Equal("ARES", infoDocument.RootElement.GetProperty("product").GetString());
        Assert.True(infoDocument.RootElement.GetProperty("journal").GetProperty("chainIntact").GetBoolean());
    }

    [Fact]
    public async Task Emergency_stop_is_admin_only_and_refuses_new_work()
    {
        var manager = _factory.ClientFor(_factory.ManagerAKey);
        var admin = _factory.ClientFor(_factory.AdminKey);
        var approver = _factory.ClientFor(_factory.ApproverAKey);
        var worker = _factory.ClientFor(_factory.WorkerAKey);

        var engagementId = await ApiFlow.CreateActiveEngagementAsync(manager, "estop");
        var (executionId, approvalId) = await ApiFlow.AwaitingApprovalAsync(manager, engagementId);
        await ApiFlow.ApproveAsync(approver, approvalId);
        await ApiFlow.GrantAsync(worker, executionId, approvalId);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await manager.PostAsJsonAsync("/api/v1/system/emergency-stop",
                new { reason = "not admin" })).StatusCode);

        try
        {
            Assert.Equal(HttpStatusCode.OK,
                (await admin.PostAsJsonAsync("/api/v1/system/emergency-stop",
                    new { reason = "drill" })).StatusCode);

            Assert.Equal(HttpStatusCode.Conflict,
                (await worker.PostAsync($"/api/v1/executions/{executionId}/queue", null)).StatusCode);

            var state = await admin.GetAsync("/api/v1/system/emergency-stop");
            Assert.Contains("true", await state.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        finally
        {
            Assert.Equal(HttpStatusCode.OK,
                (await admin.PostAsJsonAsync("/api/v1/system/emergency-stop/recover",
                    new { reason = "drill over" })).StatusCode);
        }
    }
}
