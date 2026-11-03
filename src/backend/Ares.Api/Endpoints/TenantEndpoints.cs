using System.Security.Claims;
using Ares.Api.Auth;
using Ares.Api.Platform;
using Ares.Identity.Services;
using Ares.Infrastructure.Audit;

namespace Ares.Api.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenants").WithTags("Tenants");

        group.MapGet("", async (ITenantService tenants) => Results.Ok(await tenants.ListAsync()))
            .WithName("ListTenants")
            .RequireAuthorization(AresPolicies.PlatformAdmin);

        group.MapGet("/{id:guid}", async (ITenantService tenants, Guid id) =>
            Results.Ok(await tenants.GetByIdAsync(id)))
            .WithName("GetTenant")
            .RequireAuthorization(AresPolicies.PlatformAdmin);

        group.MapPost("", async (ClaimsPrincipal user, ITenantService tenants, IAuditLog audit, CreateTenantBody body) =>
            {
                var tenant = await tenants.CreateAsync(body.Name, body.Description, body.Configuration, body.RetentionPolicy);
                audit.Record("tenant.created", "allowed", "tenant", tenant.Id.ToString(),
                    detail: $"name='{tenant.Name}' retention={tenant.RetentionPolicy}", subject: user.SubjectId());
                return Results.Created($"/api/v1/tenants/{tenant.Id}", tenant);
            })
            .WithName("CreateTenant")
            .RequireAuthorization(AresPolicies.PlatformAdmin);

        group.MapPost("/{id:guid}/suspend", async (ClaimsPrincipal user, ITenantService tenants, IAuditLog audit,
                Guid id, SuspendTenantBody body) =>
            {
                var tenant = await tenants.SuspendAsync(id, body.Reason);
                audit.Record("tenant.suspended", "allowed", "tenant", id.ToString(),
                    detail: body.Reason, subject: user.SubjectId(), tenantId: id);
                return Results.Ok(tenant);
            })
            .WithName("SuspendTenant")
            .WithSummary("Suspend a tenant: its credentials stop authenticating immediately")
            .RequireAuthorization(AresPolicies.PlatformAdmin);

        return app;
    }
}

public sealed record CreateTenantBody(string Name, string Description, string Configuration, string RetentionPolicy);
public sealed record SuspendTenantBody(string Reason);
