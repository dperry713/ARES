using System.Security.Claims;
using Ares.Api.Auth;
using Ares.Domain;
using Ares.Engagements;
using Ares.Identity.Auth;

namespace Ares.Api.Platform;

/// <summary>
/// Tenant isolation helpers. A caller may only resolve resources belonging to
/// its own tenant; foreign resources are reported exactly like missing ones so
/// no existence oracle leaks across tenants.
/// </summary>
public static class AresAccess
{
    public static Guid TenantId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirst(ApiKeyAuthenticationHandler.TenantClaim)?.Value;
        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }

    public static string SubjectId(this ClaimsPrincipal user) =>
        user.FindFirst(ApiKeyAuthenticationHandler.SubjectClaim)?.Value
        ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? "unknown";

    public static string DisplayName(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Name)?.Value ?? SubjectId(user);

    public static bool IsPlatformAdmin(this ClaimsPrincipal user) => user.IsInRole(AresRoles.PlatformAdmin);

    public static bool CanAccessTenant(this ClaimsPrincipal user, Guid tenantId) =>
        user.IsPlatformAdmin() || user.TenantId() == tenantId;

    /// <summary>Tenant-guarded engagement lookup; null when not accessible.</summary>
    public static async Task<Engagement?> FindEngagementAsync(this ClaimsPrincipal user, IEngagementService engagements, Guid id)
    {
        try
        {
            var engagement = await engagements.GetByIdAsync(id);
            return user.CanAccessTenant(engagement.TenantId) ? engagement : null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }
}
