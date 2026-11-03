using System.Reflection;
using System.Security.Claims;
using Ares.Api.Auth;
using Ares.Api.Contracts;
using Ares.Api.Platform;
using Ares.Execution.Services;
using Ares.Infrastructure.Audit;
using Ares.Infrastructure.Journal;
using Ares.Policy.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ares.Api.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/system").WithTags("System");

        group.MapGet("/info", (ClaimsPrincipal user, IStateJournal journal) =>
            {
                var version = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                    ?? "unknown";

                var chain = journal.VerifyChain();
                return Results.Ok(new
                {
                    product = "ARES",
                    version,
                    subject = user.SubjectId(),
                    tenant = user.TenantId(),
                    roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
                    journal = new { entries = journal.Count, chainIntact = chain.IsIntact },
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
                });
            })
            .WithName("GetSystemInfo")
            .RequireAuthorization(AresPolicies.Authenticated);

        group.MapGet("/emergency-stop", () => Results.Ok(new
            {
                active = EmergencyStop.IsActive,
                activationRequired = false
            }))
            .WithName("GetEmergencyStopState")
            .RequireAuthorization(AresPolicies.Authenticated);

        group.MapPost("/emergency-stop", (ClaimsPrincipal user, IAuditLog audit, EmergencyStopBody body) =>
            {
                if (string.IsNullOrWhiteSpace(body.Reason))
                    return Results.Problem(title: "Reason required",
                        detail: "An emergency stop must record why it was activated.", statusCode: 400);

                EmergencyStop.Activate(user.SubjectId(), body.Reason);
                audit.Record("platform.emergency-stop.activated", "allowed", "platform", "global",
                    detail: body.Reason, subject: user.SubjectId());
                return Results.Ok(new { active = true, reason = body.Reason });
            })
            .WithName("ActivateEmergencyStop")
            .WithSummary("Latch the global emergency stop (all execution refused)")
            .RequireAuthorization(AresPolicies.PlatformAdmin);

        group.MapPost("/emergency-stop/recover", (ClaimsPrincipal user, IAuditLog audit, EmergencyStopBody body) =>
            {
                if (string.IsNullOrWhiteSpace(body.Reason))
                    return Results.Problem(title: "Reason required",
                        detail: "Recovery must record why the stop was lifted.", statusCode: 400);

                EmergencyStop.Recover(user.SubjectId(), body.Reason);
                audit.Record("platform.emergency-stop.recovered", "allowed", "platform", "global",
                    detail: body.Reason, subject: user.SubjectId());
                return Results.Ok(new { active = false, reason = body.Reason });
            })
            .WithName("RecoverEmergencyStop")
            .RequireAuthorization(AresPolicies.PlatformAdmin);

        // ---- Health probes (anonymous by design: orchestrators cannot hold keys) ----
        app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }))
            .WithTags("Health").AllowAnonymous();

        app.MapGet("/health/ready", async (HealthCheckService health, CancellationToken cancellationToken) =>
            {
                var report = await health.CheckHealthAsync(r => r.Tags.Contains("ready"), cancellationToken);
                var body = new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(), e.Value.Description })
                };
                return report.Status == HealthStatus.Healthy
                    ? Results.Ok(body)
                    : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
            })
            .WithTags("Health").AllowAnonymous();

        return app;
    }
}
