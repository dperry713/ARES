// P7 tenant-boundary tests (MEC §6, blueprint §14).
// The platform's core promise: tenant A's data and authorizations must never
// be visible or usable by tenant B. Fail-first set:
//  (a) engagement tenant pinning — a tenant-scoped read of another tenant's
//      engagement is indistinguishable from a missing one (no existence oracle);
//  (b) approval cross-tenant denial, pinned at the EXECUTION GRANT layer;
//  (c) executions inherit the engagement tenant and are filtered by it.
using Ares.Domain;
using Ares.Execution.Services;
using DomainExecution = Ares.Domain.Execution;
using Xunit;

namespace Ares.UnitTests;

public sealed class TenantBoundaryTests
{
    [Fact]
    public async Task Engagement_records_pin_their_tenant()
    {
        var h = await AresHarness.CreateAsync();
        var fetched = await h.Engagements.GetByIdAsync(h.EngagementId);
        Assert.Equal(h.TenantId, fetched.TenantId);
        Assert.Equal("Active", fetched.Status);
    }

    [Fact]
    public async Task Foreign_tenant_read_is_indistinguishable_from_missing()
    {
        var h = await AresHarness.CreateAsync();
        var foreignTenant = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => h.Engagements.GetByIdAsync(h.EngagementId, foreignTenant));

        // The owner is unaffected.
        var owned = await h.Engagements.GetByIdAsync(h.EngagementId, h.TenantId);
        Assert.Equal(h.EngagementId, owned.Id);
    }

    [Fact]
    public async Task Tenant_scoped_listing_never_returns_another_tenants_engagements()
    {
        var h = await AresHarness.CreateAsync();
        Assert.Empty(await h.Engagements.ListAsync(Guid.NewGuid()));
        Assert.Single(await h.Engagements.ListAsync(h.TenantId));
    }

    [Fact]
    public async Task Grant_with_wrong_tenant_binding_does_not_advance_execution()
    {
        var h = await AresHarness.CreateAsync();
        var execution = await h.ProposeAsync();
        execution = await h.RequestApprovalAsync(execution.Id);

        var approval = await h.Approvals.RequestAsync(
            execution.Id, execution.EngagementId, execution.TenantId, execution.Target,
            execution.ModuleId, execution.ModuleVersion, execution.OperationHash,
            AresHarness.Requester, TimeSpan.FromMinutes(30));
        approval = await h.Approvals.ApproveAsync(approval.Id, AresHarness.Approver);

        // Attacker presents another tenant's identity for this tenant's approval.
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Executions.GrantApprovalAsync(
            execution.Id, approval.Id,
            new ExecutionOperationBinding(execution.EngagementId, Guid.NewGuid(),
                execution.Target, execution.OperationHash)));

        Assert.Equal(nameof(ExecutionState.AwaitingApproval), (await h.Executions.GetAsync(execution.Id)).Status);
    }

    [Fact]
    public async Task Executions_inherit_the_engagement_tenant_and_are_filtered_by_it()
    {
        var h = await AresHarness.CreateAsync();
        var execution = await h.ProposeAsync();
        Assert.Equal(h.TenantId, execution.TenantId);

        Assert.Empty(await h.Executions.ListAsync(tenantId: Guid.NewGuid()));
        Assert.Single(await h.Executions.ListAsync(tenantId: h.TenantId));
    }

    [Fact]
    public async Task Engagement_lifecycle_ignores_unknown_records()
    {
        var h = await AresHarness.CreateAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Engagements.StartAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Engagements.GetByIdAsync(Guid.NewGuid()));
    }
}
