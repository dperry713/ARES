using Ares.Api.Auth;
using Ares.Api.Contracts;
using Ares.Api.Endpoints;
using Ares.Api.Platform;
using Ares.Infrastructure.Journal;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.AddAresPlatform();

var app = builder.Build();

// One exception boundary for the whole surface -> RFC 9457 problem+json.
app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Cross-Origin-Opener-Policy"] = "same-origin";
    headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), microphone=()";
    if (context.Request.Path.StartsWithSegments("/api"))
        headers["Cache-Control"] = "no-store";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();

    // When TLS terminates upstream (ingress / load balancer) the API itself
    // only ever sees HTTP, so redirecting here would loop. Turning the
    // redirect off is an explicit, audited deployment decision; the default
    // stays on so a bare `dotnet Ares.Api.dll` is still safe.
    if (app.Configuration.GetValue("Ares:Http:UseHttpsRedirection", true))
        app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Attribution: every journaled transition carries the authenticated subject.
app.Use(async (context, next) =>
{
    using var scope = JournalActor.Push(context.User.SubjectId());
    await next();
});

app.MapOpenApi().RequireAuthorization(AresPolicies.Authenticated);

app.MapEngagementEndpoints();
app.MapExecutionEndpoints();
app.MapApprovalEndpoints();
app.MapAuditEndpoints();
app.MapPolicyEndpoints();
app.MapSystemEndpoints();
app.MapTenantEndpoints();
app.MapFindingsEndpoints();

if (!string.IsNullOrEmpty(AresStartup.BootstrapKey))
{
    app.Logger.LogWarning(
        "No API keys configured. A one-time bootstrap administrator credential was generated for this process: {Key}. " +
        "Set ARES_BOOTSTRAP_ADMIN_KEY (or Ares:Auth:Keys) to control it yourself. It is not persisted.",
        AresStartup.BootstrapKey);
}

app.Logger.LogInformation("ARES datastore: {JournalPath}", AresStartup.JournalPath);
foreach (var descriptor in AresStartup.KeyDescriptors)
    app.Logger.LogInformation("Credential {KeyId} roles=[{Roles}] tenant={Tenant} fingerprint={Fingerprint}",
        descriptor.Id, string.Join(",", descriptor.Roles), descriptor.TenantId, descriptor.Fingerprint);

app.Run();

/// <summary>
/// Test seam: WebApplicationFactory needs a Program entry point for top-level
/// statements. Public by design so the API test project can boot the real host.
/// </summary>
public partial class Program;
