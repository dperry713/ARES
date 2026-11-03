using Ares.Api.Auth;
using Ares.Api.Platform;
using Ares.Infrastructure.Audit;
using Ares.Infrastructure.Journal;

namespace Ares.Api.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/audit").WithTags("Audit");

        group.MapGet("", (System.Security.Claims.ClaimsPrincipal user, IAuditLog audit,
                string? action = null, long after = 0, int limit = 200) =>
            {
                // Auditors see their own tenant only; platform admins see everything.
                var tenantFilter = user.IsPlatformAdmin() ? null : (Guid?)user.TenantId();
                return Results.Ok(audit.Read(action ?? string.Empty, after, Math.Clamp(limit, 1, 500), tenantFilter));
            })
            .WithName("ReadAuditTrail")
            .WithSummary("Read the security audit trail")
            .WithDescription("Audit records live in the same hash-chained journal as state transitions.")
            .RequireAuthorization(AresPolicies.AuditRead);

        group.MapGet("/integrity", (IStateJournal journal) =>
            {
                var verification = journal.VerifyChain();
                return Results.Ok(new
                {
                    intact = verification.IsIntact,
                    entriesVerified = verification.EntriesVerified,
                    firstInvalidSequence = verification.FirstInvalidSequence,
                    detail = verification.Detail,
                    entries = journal.Count
                });
            })
            .WithName("VerifyAuditIntegrity")
            .WithSummary("Recompute the hash chain over the whole journal")
            .RequireAuthorization(AresPolicies.AuditRead);

        return app;
    }
}
