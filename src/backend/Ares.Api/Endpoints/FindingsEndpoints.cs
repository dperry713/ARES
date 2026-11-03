using System.Security.Claims;
using Ares.Api.Auth;
using Ares.Api.Contracts;
using Ares.Api.Platform;

namespace Ares.Api.Endpoints;

public static class FindingsEndpoints
{
    public static IEndpointRouteBuilder MapFindingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/findings").WithTags("Findings");

        group.MapGet("", async (ClaimsPrincipal user) =>
            {
                var tenantFilter = user.IsPlatformAdmin() ? null : (Guid?)user.TenantId();
                // TODO: replace with real findings service lookup
                return Results.Ok(new { items = new object[0], total = 0, skip = tenantFilter == null ? 0 : 0, take = 50 });
            })
            .WithName("ListFindings")
            .RequireAuthorization(AresPolicies.FindingsRead);

        group.MapGet("/{id:guid}", async (ClaimsPrincipal user, Guid id) =>
            {
                // TODO: replace with real findings lookup
                return Results.NotFound(new { error = "Finding not found (service pending)" });
            })
            .WithName("GetFinding")
            .RequireAuthorization(AresPolicies.FindingsRead);

        group.MapGet("/{id:guid}/evidence", async (ClaimsPrincipal user, Guid id) =>
            {
                // TODO: replace with real evidence service lookup
                return Results.NotFound(new { error = "Evidence not found (service pending)" });
            })
            .WithName("GetFindingEvidence")
            .RequireAuthorization(AresPolicies.FindingsRead);

        group.MapPost("/{id:guid}/review", async (ClaimsPrincipal user, Guid id, ReviewFindingBody body) =>
            {
                // TODO: implement review logic (status change, comments, etc.)
                return Results.Ok(new { id = id, status = "Unverified", note = "Review endpoint - logic pending" });
            })
            .WithName("ReviewFinding")
            .RequireAuthorization(AresPolicies.FindingsOperate);

        group.MapPost("/{id:guid}/resolve", async (ClaimsPrincipal user, Guid id, ResolveFindingBody body) =>
            {
                // TODO: implement resolve logic (mark as remediated, accepted risk, etc.)
                return Results.Ok(new { id = id, status = "Unverified", note = "Resolve endpoint - logic pending" });
            })
            .WithName("ResolveFinding")
            .RequireAuthorization(AresPolicies.FindingsOperate);

        return app;
    }
}

public sealed record ReviewFindingBody(string? Comments);

public sealed record ResolveFindingBody(string? Resolution, string? RemediationPlan);