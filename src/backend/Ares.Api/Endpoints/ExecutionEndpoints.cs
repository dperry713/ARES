using System.Security.Claims;
using Ares.Api.Auth;
using Ares.Api.Contracts;
using Ares.Api.Platform;
using Ares.Execution.Services;
using Ares.Infrastructure.Audit;

namespace Ares.Api.Endpoints;

public static class ExecutionEndpoints
{
    public static IEndpointRouteBuilder MapExecutionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/executions").WithTags("Executions");

        group.MapPost("", async (ClaimsPrincipal user, IExecutionService executions,
                Ares.Engagements.IEngagementService engagements, IAuditLog audit,
                ProposeExecutionRequest request) =>
            {
                var engagement = await user.FindEngagementAsync(engagements, request.EngagementId);
                if (engagement is null) return Results.NotFound();

                var execution = await executions.ProposeAsync(request.EngagementId, request.HypothesisId,
                    request.Target, request.TestCategory, request.ModuleId, request.ModuleVersion,
                    request.IdempotencyKey);

                audit.Record("execution.proposed", "allowed", "execution", execution.Id.ToString(),
                    detail: $"target='{execution.Target}' module={execution.ModuleId}@{execution.ModuleVersion}",
                    subject: user.SubjectId(), tenantId: execution.TenantId);

                return Results.Created($"/api/v1/executions/{execution.Id}", execution);
            })
            .WithName("ProposeExecution")
            .WithSummary("Propose an execution")
            .WithDescription("Scope is validated at proposal time; policy is re-validated at queue and start.")
            .RequireAuthorization(AresPolicies.ExecutionOperate);

        group.MapGet("", async (ClaimsPrincipal user, IExecutionService executions,
                Guid? engagementId, int skip = 0, int take = 50) =>
            {
                var tenantFilter = user.IsPlatformAdmin() ? null : (Guid?)user.TenantId();
                return Results.Ok(await executions.ListAsync(engagementId, tenantFilter, skip, take));
            })
            .WithName("ListExecutions")
            .RequireAuthorization(AresPolicies.EngagementRead);

        group.MapGet("/{id:guid}", async (ClaimsPrincipal user, IExecutionService executions, Guid id) =>
            {
                var execution = await LoadAsync(user, executions, id);
                return execution is null ? Results.NotFound() : Results.Ok(execution);
            })
            .WithName("GetExecution")
            .RequireAuthorization(AresPolicies.EngagementRead);

        group.MapPost("/{id:guid}/request-approval", async (ClaimsPrincipal user, IExecutionService executions,
                IAuditLog audit, Guid id) =>
            {
                var execution = await LoadAsync(user, executions, id);
                if (execution is null) return Results.NotFound();

                var updated = await executions.RequestApprovalAsync(id);
                audit.Record("execution.approval-requested", "allowed", "execution", id.ToString(),
                    subject: user.SubjectId(), tenantId: updated.TenantId);
                return Results.Ok(updated);
            })
            .WithName("RequestExecutionApproval")
            .WithSummary("Move execution Proposed -> AwaitingApproval")
            .RequireAuthorization(AresPolicies.ExecutionOperate);

        group.MapPost("/{id:guid}/grant", async (ClaimsPrincipal user, IExecutionService executions,
                IAuditLog audit, Guid id, GrantBody body) =>
            {
                var execution = await LoadAsync(user, executions, id);
                if (execution is null) return Results.NotFound();

                var updated = await executions.GrantApprovalAsync(id, body.ApprovalId);
                audit.Record("execution.approved", "allowed", "execution", id.ToString(),
                    detail: $"approval={body.ApprovalId} approver={updated.ApprovedBy}",
                    subject: user.SubjectId(), tenantId: updated.TenantId);
                return Results.Ok(updated);
            })
            .WithName("GrantExecutionApproval")
            .WithSummary("Bind a validated approval to the execution (Approved)")
            .WithDescription("The approval store re-validates engagement, tenant, target, module, operation hash, expiry, and single use.")
            .RequireAuthorization(AresPolicies.ExecutionOperate);

        group.MapPost("/{id:guid}/queue", async (ClaimsPrincipal user, IExecutionService executions,
                IAuditLog audit, Guid id) =>
            {
                var execution = await LoadAsync(user, executions, id);
                if (execution is null) return Results.NotFound();

                var updated = await executions.QueueAsync(id);
                audit.Record("execution.queued", "allowed", "execution", id.ToString(),
                    detail: $"policy={updated.PolicyReasonCode} decision={updated.PolicyDecisionId}",
                    subject: user.SubjectId(), tenantId: updated.TenantId);
                return Results.Ok(updated);
            })
            .WithName("QueueExecution")
            .WithSummary("Queue an approved execution (re-evaluates policy)")
            .RequireAuthorization(AresPolicies.ExecutionOperate);

        group.MapPost("/{id:guid}/lease", async (ClaimsPrincipal user, IExecutionService executions, Guid id,
                LeaseBody body) =>
            {
                var execution = await LoadAsync(user, executions, id);
                if (execution is null) return Results.NotFound();
                return Results.Ok(await executions.LeaseAsync(id, body.WorkerId, TimeSpan.FromSeconds(body.LeaseSeconds)));
            })
            .WithName("LeaseExecution")
            .WithSummary("Lease a queued execution to a worker")
            .RequireAuthorization(AresPolicies.ExecutionOperate);

        group.MapPost("/{id:guid}/heartbeat", async (ClaimsPrincipal user, IExecutionService executions, Guid id,
                WorkerBody body) =>
            {
                var execution = await LoadAsync(user, executions, id);
                if (execution is null) return Results.NotFound();
                return Results.Ok(await executions.HeartbeatAsync(id, body.WorkerId, TimeSpan.FromSeconds(body.ExtensionSeconds)));
            })
            .WithName("HeartbeatExecution")
            .RequireAuthorization(AresPolicies.ExecutionOperate);

        group.MapPost("/{id:guid}/start", async (ClaimsPrincipal user, IExecutionService executions,
                IAuditLog audit, Guid id) =>
            {
                var execution = await LoadAsync(user, executions, id);
                if (execution is null) return Results.NotFound();

                var updated = await executions.StartAsync(id);
                audit.Record("execution.started", "allowed", "execution", id.ToString(),
                    detail: $"policy={updated.PolicyReasonCode} decision={updated.PolicyDecisionId}",
                    subject: user.SubjectId(), tenantId: updated.TenantId);
                return Results.Ok(updated);
            })
            .WithName("StartExecution")
            .WithSummary("Start an execution (re-evaluates policy immediately before the operation)")
            .RequireAuthorization(AresPolicies.ExecutionOperate);

        group.MapPost("/{id:guid}/complete", async (ClaimsPrincipal user, IExecutionService executions, Guid id,
                CompleteBody body) =>
            {
                var execution = await LoadAsync(user, executions, id);
                if (execution is null) return Results.NotFound();
                return Results.Ok(await executions.CompleteAsync(id, body.ResultReference));
            })
            .WithName("CompleteExecution")
            .RequireAuthorization(AresPolicies.ExecutionOperate);

        group.MapPost("/{id:guid}/verify", async (ClaimsPrincipal user, IExecutionService executions, Guid id) =>
            {
                var execution = await LoadAsync(user, executions, id);
                if (execution is null) return Results.NotFound();
                return Results.Ok(await executions.VerifyAsync(id));
            })
            .WithName("VerifyExecution")
            .RequireAuthorization(AresPolicies.ExecutionOperate);

        group.MapPost("/{id:guid}/cancel", async (ClaimsPrincipal user, IExecutionService executions,
                IAuditLog audit, Guid id, CancelBody? body) =>
            {
                var execution = await LoadAsync(user, executions, id);
                if (execution is null) return Results.NotFound();

                var updated = await executions.CancelAsync(id, user.SubjectId(), body?.Reason);
                audit.Record("execution.cancelled", "allowed", "execution", id.ToString(),
                    detail: body?.Reason, subject: user.SubjectId(), tenantId: updated.TenantId);
                return Results.Ok(updated);
            })
            .WithName("CancelExecution")
            .RequireAuthorization(AresPolicies.ExecutionOperate);

        return app;
    }

    private static async Task<Ares.Domain.Execution?> LoadAsync(ClaimsPrincipal user,
        IExecutionService executions, Guid id)
    {
        try
        {
            var execution = await executions.GetAsync(id);
            return user.CanAccessTenant(execution.TenantId) ? execution : null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }
}
