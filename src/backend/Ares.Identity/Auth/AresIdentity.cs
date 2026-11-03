namespace Ares.Identity.Auth;

/// <summary>Canonical role names. Policies are built from these only.</summary>
public static class AresRoles
{
    /// <summary>Platform-wide operator: manages tenants, API keys, emergency stop.</summary>
    public const string PlatformAdmin = "platform_admin";

    /// <summary>Creates and drives engagements (authorize, start, pause, complete).</summary>
    public const string EngagementManager = "engagement_manager";

    /// <summary>Decides approval requests. Cannot approve its own request.</summary>
    public const string Approver = "approver";

    /// <summary>Runtime agent: leases and heartbeats executions. Cannot approve.</summary>
    public const string Worker = "worker";

    /// <summary>Read-only: findings, evidence, audit trail.</summary>
    public const string Auditor = "auditor";

    public static readonly string[] All =
        [PlatformAdmin, EngagementManager, Approver, Worker, Auditor];

    public static bool IsKnown(string role) => Array.IndexOf(All, role) >= 0;
}

/// <summary>
/// The authenticated caller. Produced only by credential validation, never from
/// request body or headers, so no field here is attacker-controlled.
/// </summary>
public sealed record AresIdentity(
    string SubjectId,
    string DisplayName,
    Guid TenantId,
    IReadOnlySet<string> Roles,
    string CredentialType = "api_key")
{
    public bool IsPlatformAdmin => Roles.Contains(AresRoles.PlatformAdmin);

    public bool HasRole(string role) => Roles.Contains(role);

    /// <summary>Tenant isolation: platform admins span tenants, everyone else is pinned.</summary>
    public bool CanAccessTenant(Guid tenantId) => IsPlatformAdmin || TenantId == tenantId;
}
