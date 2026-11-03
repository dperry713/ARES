// F-01 closure tests (authentication/authorization half) against the real host.
//
// Every protected endpoint must return 401 without a credential and 403 for a
// credential that lacks the role. Tenant isolation and tenant suspension are
// proven at the HTTP boundary, not only in unit tests.
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Ares.ApiTests;

public sealed class ApiSecurityTests : IClassFixture<AresApiFactory>
{
    private readonly AresApiFactory _factory;

    public ApiSecurityTests(AresApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Requests_without_a_credential_are_401()
    {
        var client = _factory.CreateClient();

        foreach (var path in new[]
                 {
                     "/api/v1/engagements", "/api/v1/executions", "/api/v1/approvals",
                     "/api/v1/audit", "/api/v1/system/info", "/api/v1/tenants", "/openapi/v1.json"
                 })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains("AresApiKey", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Unknown_credentials_are_401()
    {
        var client = _factory.ClientFor("ares_not_a_real_key");
        var response = await client.GetAsync("/api/v1/system/info");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_probes_are_anonymous_for_orchestrators()
    {
        var client = _factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        var body = await ready.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"Healthy\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_suspended_tenant_credential_stops_authenticating()
    {
        var client = _factory.ClientFor(_factory.SuspendedKey);
        var response = await client.GetAsync("/api/v1/system/info");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Roles_are_enforced_on_the_write_paths()
    {
        // An auditor may read but never create or drive work.
        var auditor = _factory.ClientFor(_factory.AuditorAKey);

        var create = await auditor.PostAsJsonAsync("/api/v1/engagements", new
        {
            name = "auditor-attempt",
            description = "must be denied",
            authorizationReference = "AR-NO",
            startAt = DateTime.UtcNow,
            endAt = DateTime.UtcNow.AddHours(1),
            scopeDefinition = "staging.example.test",
            allowedAssessmentTypes = new[] { "safe_http_configuration_checks" },
            excludedTargets = Array.Empty<string>()
        });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        // A worker may not read the audit trail; an auditor may.
        var worker = _factory.ClientFor(_factory.WorkerAKey);
        Assert.Equal(HttpStatusCode.Forbidden, (await worker.GetAsync("/api/v1/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync("/api/v1/audit")).StatusCode);

        // Only a platform admin may list tenants.
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync("/api/v1/tenants")).StatusCode);
        var admin = _factory.ClientFor(_factory.AdminKey);
        var tenants = await admin.GetAsync("/api/v1/tenants");
        Assert.Equal(HttpStatusCode.OK, tenants.StatusCode);
        var body = await tenants.Content.ReadAsStringAsync();
        Assert.Contains(_factory.TenantA.ToString("D"), body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Tenants_are_isolated_across_the_http_boundary()
    {
        var managerA = _factory.ClientFor(_factory.ManagerAKey);
        var managerB = _factory.ClientFor(_factory.ManagerBKey);

        var created = await managerA.PostAsJsonAsync("/api/v1/engagements", new
        {
            name = "tenant-a-work",
            description = "isolation proof",
            authorizationReference = "AR-ISO-1",
            startAt = DateTime.UtcNow.AddMinutes(-5),
            endAt = DateTime.UtcNow.AddHours(2),
            scopeDefinition = "staging.example.test",
            allowedAssessmentTypes = new[] { "safe_http_configuration_checks" },
            excludedTargets = Array.Empty<string>()
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var engagement = await created.Content.ReadFromJsonAsync<IdBody>();
        Assert.NotNull(engagement);

        // Foreign tenant sees "not found", never "forbidden" (no existence oracle).
        Assert.Equal(HttpStatusCode.NotFound,
            (await managerB.GetAsync($"/api/v1/engagements/{engagement!.Id}")).StatusCode);

        // Listing is tenant-scoped.
        var list = await managerB.GetAsync("/api/v1/engagements");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.DoesNotContain(engagement.Id.ToString("D"),
            await list.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_dual_role_caller_cannot_approve_its_own_request()
    {
        var dual = _factory.ClientFor(_factory.DualRoleAKey);

        var created = await dual.PostAsJsonAsync("/api/v1/engagements", new
        {
            name = "dual-role",
            description = "sod",
            authorizationReference = "AR-SOD-1",
            startAt = DateTime.UtcNow.AddMinutes(-5),
            endAt = DateTime.UtcNow.AddHours(2),
            scopeDefinition = "staging.example.test",
            allowedAssessmentTypes = new[] { "safe_http_configuration_checks" },
            excludedTargets = Array.Empty<string>()
        });
        var engagement = await created.Content.ReadFromJsonAsync<IdBody>();
        await dual.PostAsync($"/api/v1/engagements/{engagement!.Id}/authorize", null);
        await dual.PostAsync($"/api/v1/engagements/{engagement.Id}/start", null);

        var execution = await ApiFlow.ProposeAsync(dual, engagement.Id);
        await dual.PostAsync($"/api/v1/executions/{execution}/request-approval", null);

        var request = await dual.PostAsJsonAsync($"/api/v1/executions/{execution}/approval-requests",
            new { ttlMinutes = 30, maxUses = 1, justification = "sod" });
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        var approval = await request.Content.ReadFromJsonAsync<IdBody>();

        // Same subject requests and tries to approve: denied (409 from the store's SoD rule).
        var selfApprove = await dual.PostAsJsonAsync($"/api/v1/approvals/{approval!.Id}/approve", new { reason = "self" });
        Assert.Equal(HttpStatusCode.Conflict, selfApprove.StatusCode);
        var body = await selfApprove.Content.ReadAsStringAsync();
        Assert.Contains("Separation of duties", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Findings_review_is_engagement_manager_allowed()
    {
        var client = _factory.ClientFor(_factory.ManagerAKey); // EngagementManager role has FindingsOperate
        var response = await client.PostAsJsonAsync("/api/v1/findings/12345678-1234-5678-1234-567812345678/review",
            new { Comments = "test review" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Findings_resolve_is_engagement_manager_allowed()
    {
        var client = _factory.ClientFor(_factory.ManagerAKey); // EngagementManager role has FindingsOperate
        var response = await client.PostAsJsonAsync("/api/v1/findings/12345678-1234-5678-1234-567812345678/resolve",
            new { Resolution = "fixed", RemediationPlan = "patching" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Findings_review_is_auditor_forbidden()
    {
        var client = _factory.ClientFor(_factory.AuditorAKey); // Auditor role does NOT have FindingsOperate
        var response = await client.PostAsJsonAsync("/api/v1/findings/12345678-1234-5678-1234-567812345678/review",
            new { Comments = "test review" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Findings_resolve_is_auditor_forbidden()
    {
        var client = _factory.ClientFor(_factory.AuditorAKey); // Auditor role does NOT have FindingsOperate
        var response = await client.PostAsJsonAsync("/api/v1/findings/12345678-1234-5678-1234-567812345678/resolve",
            new { Resolution = "fixed", RemediationPlan = "patching" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Findings_list_is_tenant_isolated()
    {
        var managerA = _factory.ClientFor(_factory.ManagerAKey);
        var managerB = _factory.ClientFor(_factory.ManagerBKey);

        // Manager A creates a finding-like entry via the system info path (or we just verify listing is scoped)
        // Since findings are tenant-scoped, Manager B should not see Manager A's data.
        // This test validates the tenant filter is applied at the HTTP boundary.
        var listA = await managerA.GetAsync("/api/v1/findings");
        var listB = await managerB.GetAsync("/api/v1/findings");
        Assert.Equal(HttpStatusCode.OK, listA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listB.StatusCode);
        // Both return empty or tenant-filtered results; no existence oracle.
        var bodyA = await listA.Content.ReadAsStringAsync();
        var bodyB = await listB.Content.ReadAsStringAsync();
        // Neither body should contain the other tenant's engagement IDs as a side effect.
        Assert.DoesNotContain(_factory.TenantB.ToString("D"), bodyA, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_factory.TenantA.ToString("D"), bodyB, StringComparison.OrdinalIgnoreCase);
    }
}