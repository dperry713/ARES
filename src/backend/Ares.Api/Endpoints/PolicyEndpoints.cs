using System.Security.Claims;
using Ares.Api.Auth;
using Ares.Api.Contracts;
using Ares.Api.Platform;
using Ares.Engagements;
using Ares.Policy.Services;

namespace Ares.Api.Endpoints;

public static class PolicyEndpoints
{
    public static IEndpointRouteBuilder MapPolicyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/policy").WithTags("Policy");

        group.MapPost("evaluate", async (ClaimsPrincipal user, IPolicyEngine policyEngine, IEngagementService engagements, EvaluatePolicyRequest request) =>
        {
            var engagement = await engagements.GetByIdAsync(request.EngagementId);
            if (engagement is null || engagement.TenantId != user.TenantId()) return Results.NotFound(new { error = "Engagement not found or not accessible" });

            var result = await policyEngine.EvaluateAsync(
                engagement,
                request.TestCategory,
                request.ModuleId,
                request.ModuleVersion,
                request.RequiresHumanApproval,
                request.MaximumConcurrency,
                request.RateLimits);

            return Results.Ok(new
            {
                Decision = result.Decision.ToString().ToLower(),
                ReasonCode = result.ReasonCode,
                Explanations = result.Explanations,
                EvaluatedAt = result.EvaluatedAt
            });
        })
        .WithName("EvaluatePolicy")
        .WithSummary("Evaluate a policy decision for an engagement/test combination")
        .RequireAuthorization(AresPolicies.Authenticated);

        return app;
    }
}

public sealed record EvaluatePolicyRequest(
    Guid EngagementId,
    string TestCategory,
    string ModuleId,
    string ModuleVersion,
    bool RequiresHumanApproval,
    int MaximumConcurrency,
    string RateLimits);