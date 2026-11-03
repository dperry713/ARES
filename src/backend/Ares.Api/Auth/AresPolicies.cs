using Ares.Identity.Auth;

namespace Ares.Api.Auth;

/// <summary>
/// Authorization policy names and their role requirements. Policies are the only
/// place role logic lives; endpoints reference a name, never a raw role string.
/// </summary>
public static class AresPolicies
{
    public const string Authenticated = "Ares.Authenticated";
    public const string EngagementRead = "Ares.Engagement.Read";
    public const string EngagementWrite = "Ares.Engagement.Write";
    public const string ExecutionOperate = "Ares.Execution.Operate";
    public const string ApprovalRequest = "Ares.Approval.Request";
    public const string ApprovalDecide = "Ares.Approval.Decide";
    public const string AuditRead = "Ares.Audit.Read";
    public const string PlatformAdmin = "Ares.Platform.Admin";
    public const string FindingsRead = "Ares.Findings.Read";
    public const string FindingsOperate = "Ares.Findings.Operate";

    public static void Register(Microsoft.AspNetCore.Authorization.AuthorizationOptions options)
    {
        options.AddPolicy(Authenticated, p => p.RequireAuthenticatedUser());

        options.AddPolicy(EngagementRead, p => p.RequireAuthenticatedUser()
            .RequireRole(AresRoles.PlatformAdmin, AresRoles.EngagementManager, AresRoles.Approver,
                AresRoles.Worker, AresRoles.Auditor));

        options.AddPolicy(EngagementWrite, p => p.RequireAuthenticatedUser()
            .RequireRole(AresRoles.PlatformAdmin, AresRoles.EngagementManager));

        options.AddPolicy(ExecutionOperate, p => p.RequireAuthenticatedUser()
            .RequireRole(AresRoles.PlatformAdmin, AresRoles.EngagementManager, AresRoles.Worker));

        options.AddPolicy(ApprovalRequest, p => p.RequireAuthenticatedUser()
            .RequireRole(AresRoles.PlatformAdmin, AresRoles.EngagementManager));

        options.AddPolicy(ApprovalDecide, p => p.RequireAuthenticatedUser()
            .RequireRole(AresRoles.PlatformAdmin, AresRoles.Approver));

        options.AddPolicy(AuditRead, p => p.RequireAuthenticatedUser()
            .RequireRole(AresRoles.PlatformAdmin, AresRoles.Auditor));

        options.AddPolicy(PlatformAdmin, p => p.RequireAuthenticatedUser()
            .RequireRole(AresRoles.PlatformAdmin));

        options.AddPolicy(FindingsRead, p => p.RequireAuthenticatedUser()
            .RequireRole(AresRoles.PlatformAdmin, AresRoles.EngagementManager, AresRoles.Approver));

        options.AddPolicy(FindingsOperate, p => p.RequireAuthenticatedUser()
            .RequireRole(AresRoles.PlatformAdmin, AresRoles.EngagementManager, AresRoles.Approver, AresRoles.Worker));
    }
}
