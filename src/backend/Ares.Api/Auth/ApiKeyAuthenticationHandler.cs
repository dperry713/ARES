using System.Security.Claims;
using System.Text.Encodings.Web;
using Ares.Identity.Auth;
using Ares.Identity.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Ares.Api.Auth;

/// <summary>
/// API-key authentication. The credential is read from <c>X-Ares-Api-Key</c>
/// (or a Bearer token, so standard clients work). Every failure mode is a
/// deliberate 401 with a problem+json body and an audit record: unknown key,
/// empty key, suspended tenant, or unknown tenant.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "AresApiKey";
    public const string HeaderName = "X-Ares-Api-Key";
    public const string TenantClaim = "tenant_id";
    public const string SubjectClaim = "sub_id";

    private readonly IApiKeyStore _keys;
    private readonly ITenantService _tenants;
    private readonly Ares.Infrastructure.Audit.IAuditLog _audit;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyStore keys,
        ITenantService tenants,
        Ares.Infrastructure.Audit.IAuditLog audit)
        : base(options, logger, encoder)
    {
        _keys = keys;
        _tenants = tenants;
        _audit = audit;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var presented = ReadCredential();
        if (string.IsNullOrWhiteSpace(presented))
            return AuthenticateResult.NoResult();

        if (!_keys.TryAuthenticate(presented, out var identity))
        {
            _audit.Record("auth.failed", "denied", "credential", "unknown",
                detail: "presented api key did not match any active key", subject: "anonymous");
            return AuthenticateResult.Fail("Invalid API key.");
        }

        if (identity.TenantId != Guid.Empty)
        {
            try
            {
                var tenant = await _tenants.GetByIdAsync(identity.TenantId);
                if (!string.Equals(tenant.Status, "Active", StringComparison.Ordinal))
                {
                    _audit.Record("auth.denied", "denied", "tenant", identity.TenantId.ToString(),
                        detail: $"tenant status is '{tenant.Status}'", subject: identity.SubjectId);
                    return AuthenticateResult.Fail("Tenant is not active.");
                }
            }
            catch (KeyNotFoundException)
            {
                _audit.Record("auth.denied", "denied", "tenant", identity.TenantId.ToString(),
                    detail: "tenant not found", subject: identity.SubjectId);
                return AuthenticateResult.Fail("Tenant is not active.");
            }
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identity.SubjectId),
            new(ClaimTypes.Name, identity.DisplayName),
            new(SubjectClaim, identity.SubjectId),
            new(TenantClaim, identity.TenantId.ToString("D")),
            new("credential_type", identity.CredentialType)
        };
        foreach (var role in identity.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "AresApiKey realm=\"ares\", header=\"X-Ares-Api-Key\"";
        Response.ContentType = "application/problem+json";
        await Response.WriteAsJsonAsync(new
        {
            type = "https://ares.local/errors/unauthenticated",
            title = "Authentication required",
            status = 401,
            detail = $"Supply a valid API key in the '{HeaderName}' header.",
            traceId = Context.TraceIdentifier
        });
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/problem+json";
        await Response.WriteAsJsonAsync(new
        {
            type = "https://ares.local/errors/forbidden",
            title = "Forbidden",
            status = 403,
            detail = "The authenticated credential does not hold the role required for this operation.",
            traceId = Context.TraceIdentifier
        });
    }

    private string? ReadCredential()
    {
        if (Request.Headers.TryGetValue(HeaderName, out StringValues header))
        {
            var value = header.ToString();
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        var authorization = Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var bearer = authorization["Bearer ".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(bearer)) return bearer;
        }

        return null;
    }
}
