using System.Security.Claims;
using Ares.Api.Auth;
using Ares.Api.Contracts;
using Ares.Api.Platform;
using Ares.Domain;
using Ares.Engagements;
using Ares.Infrastructure.Audit;

namespace Ares.Api.Endpoints;

public static class EngagementEndpoints
{
    public static IEndpointRouteBuilder MapEngagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/engagements").WithTags("Engagements");

        group.MapPost("", async (ClaimsPrincipal user, IEngagementService engagements, IAuditLog audit,
                CreateEngagementRequest request) =>
            {
                var tenantId = user.IsPlatformAdmin() && request.TenantId is { } requested && requested != Guid.Empty
                    ? requested
                    : user.TenantId();

                if (tenantId == Guid.Empty)
                    return Results.Problem(
                        title: "Tenant required",
                        detail: "A platform administrator must name the tenant; other callers act in their own tenant.",
                        statusCode: StatusCodes.Status400BadRequest);

                var engagement = await engagements.CreateAsync(
                    tenantId, request.Name, request.Description, request.AuthorizationReference,
                    request.StartAt, request.EndAt, request.ScopeDefinition,
                    request.AllowedAssessmentTypes, request.ExcludedTargets,
                    request.MaximumConcurrency ?? 5, request.RateLimits ?? "default");

                audit.Record("engagement.created", "allowed", "engagement", engagement.Id.ToString(),
                    detail: $"scope='{engagement.ScopeDefinition}' window={engagement.StartAt:O}/{engagement.EndAt:O}",
                    subject: user.SubjectId(), tenantId: tenantId);

                return Results.Created($"/api/v1/engagements/{engagement.Id}", engagement);
            })
            .WithName("CreateEngagement")
            .WithSummary("Create an engagement")
            .WithDescription("Requires a written authorization reference, a bounded window, and a non-empty scope.")
            .RequireAuthorization(AresPolicies.EngagementWrite);

        group.MapGet("/", async (ClaimsPrincipal user, IEngagementService engagements, int skip = 0, int take = 50) =>
            {
                var tenantFilter = user.IsPlatformAdmin() ? null : (Guid?)user.TenantId();
                return Results.Ok(await engagements.ListAsync(tenantFilter, skip, take));
            })
            .WithName("ListEngagements")
            .WithSummary("List engagements visible to the caller")
            .RequireAuthorization(AresPolicies.EngagementRead);

        group.MapGet("/{id:guid}", async (ClaimsPrincipal user, IEngagementService engagements, Guid id) =>
            {
                var engagement = await user.FindEngagementAsync(engagements, id);
                return engagement is null ? Results.NotFound() : Results.Ok(engagement);
            })
            .WithName("GetEngagement")
            .WithSummary("Get an engagement")
            .RequireAuthorization(AresPolicies.EngagementRead);

        MapTransition(group, "authorize", "AuthorizeEngagement", "Authorize an engagement (Draft -> Authorized)",
            (svc, id) => svc.AuthorizeAsync(id));
        MapTransition(group, "start", "StartEngagement", "Start an authorized engagement (Authorized -> Active)",
            (svc, id) => svc.StartAsync(id));
        MapTransition(group, "pause", "PauseEngagement", "Pause an active engagement (Active -> Paused)",
            (svc, id) => svc.PauseAsync(id));
        MapTransition(group, "resume", "ResumeEngagement", "Resume a paused engagement (Paused -> Active)",
            (svc, id) => svc.ResumeAsync(id));
        MapTransition(group, "complete", "CompleteEngagement", "Complete an engagement (Active/Paused -> Completed)",
            (svc, id) => svc.CompleteAsync(id));

        group.MapGet("/{id:guid}/executions", async (ClaimsPrincipal user, IEngagementService engagements,
                Ares.Execution.Services.IExecutionService executions, Guid id, int skip = 0, int take = 50) =>
            {
                var engagement = await user.FindEngagementAsync(engagements, id);
                if (engagement is null) return Results.NotFound();
                return Results.Ok(await executions.ListAsync(id, null, skip, take));
            })
            .WithName("ListEngagementExecutions")
            .RequireAuthorization(AresPolicies.EngagementRead);

        return app;
    }

    private static void MapTransition(IEndpointRouteBuilder group, string route, string name, string summary,
        Func<IEngagementService, Guid, Task<Engagement>> transition)
    {
        group.MapPost($"/{{id:guid}}/{route}", async (ClaimsPrincipal user, IEngagementService engagements,
                IAuditLog audit, Guid id) =>
            {
                var engagement = await user.FindEngagementAsync(engagements, id);
                if (engagement is null) return Results.NotFound();

                var updated = await transition(engagements, id);
                audit.Record($"engagement.{route}", "allowed", "engagement", id.ToString(),
                    detail: $"status {engagement.Status} -> {updated.Status}",
                    subject: user.SubjectId(), tenantId: updated.TenantId);
                return Results.Ok(updated);
            })
            .WithName(name)
            .WithSummary(summary)
            .RequireAuthorization(AresPolicies.EngagementWrite);
    }
}
