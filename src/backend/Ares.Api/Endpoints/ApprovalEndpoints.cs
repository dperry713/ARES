using System.Security.Claims;
using Ares.Api.Auth;
using Ares.Api.Contracts;
using Ares.Api.Platform;
using Ares.Approvals.Services;
using Ares.Infrastructure.Audit;

namespace Ares.Api.Endpoints;

public static class ApprovalEndpoints
{
    public static IEndpointRouteBuilder MapApprovalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/approvals").WithTags("Approvals");

        // Requesting an approval is done against an EXECUTION, not against a
        // caller-supplied binding: the server derives engagement, tenant, target,
        // module, version, and operation hash from the execution record. This
        // removes the entire "approval signed for something else" failure class.
        app.MapPost("/api/v1/executions/{id:guid}/approval-requests",
                async (ClaimsPrincipal user, Ares.Execution.Services.IExecutionService executions,
                    IApprovalService approvals, IAuditLog audit, Guid id, RequestApprovalBody body) =>
                {
                    Ares.Domain.Execution execution;
                    try
                    {
                        execution = await executions.GetAsync(id);
                    }
                    catch (KeyNotFoundException)
                    {
                        return Results.NotFound();
                    }

                    if (!user.CanAccessTenant(execution.TenantId)) return Results.NotFound();
                    if (string.Equals(execution.Status, "Proposed", StringComparison.Ordinal))
                        return Results.Conflict(new { error = "Move the execution to AwaitingApproval before requesting approval." });

                    var approval = await approvals.RequestAsync(
                        execution.Id, execution.EngagementId, execution.TenantId, execution.Target,
                        execution.ModuleId, execution.ModuleVersion, execution.OperationHash,
                        user.SubjectId(), TimeSpan.FromMinutes(body.TtlMinutes), body.MaxUses ?? 1);

                    audit.Record("approval.requested", "allowed", "approval", approval.Id.ToString(),
                        detail: $"execution={execution.Id} target='{execution.Target}' ttl={body.TtlMinutes}m justification={body.Justification ?? "-"}",
                        subject: user.SubjectId(), tenantId: execution.TenantId);

                    return Results.Created($"/api/v1/approvals/{approval.Id}", approval);
                })
            .WithName("RequestApproval")
            .WithSummary("Request a bound approval for an execution")
            .RequireAuthorization(AresPolicies.ApprovalRequest);

        group.MapGet("", async (ClaimsPrincipal user, IApprovalService approvals, bool pending = true,
                int skip = 0, int take = 50) =>
            {
                if (user.IsPlatformAdmin() && !pending)
                    return Results.Problem(title: "Tenant required",
                        detail: "Platform-wide approval listing is not exposed; specify a tenant-scoped caller.",
                        statusCode: StatusCodes.Status400BadRequest);
                return Results.Ok(await approvals.ListPendingAsync(user.TenantId(), skip, take));
            })
            .WithName("ListApprovals")
            .RequireAuthorization(AresPolicies.ApprovalDecide);

        group.MapGet("/{id:guid}", async (ClaimsPrincipal user, IApprovalService approvals, Guid id) =>
            {
                var approval = await approvals.GetAsync(id);
                return user.CanAccessTenant(approval.TenantId) ? Results.Ok(approval) : Results.NotFound();
            })
            .WithName("GetApproval")
            .RequireAuthorization(AresPolicies.ApprovalDecide);

        group.MapPost("/{id:guid}/approve", async (ClaimsPrincipal user, IApprovalService approvals,
                IAuditLog audit, Guid id, ApproveBody? body) =>
            {
                var existing = await approvals.GetAsync(id);
                if (!user.CanAccessTenant(existing.TenantId)) return Results.NotFound();

                // The approver identity is the authenticated subject, never a
                // body field: separation of duties is enforced against the real
                // credential that requested the operation.
                var approved = await approvals.ApproveAsync(id, user.SubjectId());
                audit.Record("approval.approved", "allowed", "approval", id.ToString(),
                    detail: body?.Reason, subject: user.SubjectId(), tenantId: approved.TenantId);
                return Results.Ok(approved);
            })
            .WithName("ApproveOperation")
            .WithSummary("Human approves a pending request (cannot approve its own request)")
            .RequireAuthorization(AresPolicies.ApprovalDecide);

        group.MapPost("/{id:guid}/reject", async (ClaimsPrincipal user, IApprovalService approvals,
                IAuditLog audit, Guid id, RejectBody body) =>
            {
                var existing = await approvals.GetAsync(id);
                if (!user.CanAccessTenant(existing.TenantId)) return Results.NotFound();

                var rejected = await approvals.RejectAsync(id, user.SubjectId(), body.Reason);
                audit.Record("approval.rejected", "allowed", "approval", id.ToString(),
                    detail: body.Reason, subject: user.SubjectId(), tenantId: rejected.TenantId);
                return Results.Ok(rejected);
            })
            .WithName("RejectOperation")
            .RequireAuthorization(AresPolicies.ApprovalDecide);

        return app;
    }
}
