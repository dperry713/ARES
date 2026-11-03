namespace Ares.Api.Contracts;

public sealed record CreateEngagementRequest(
    Guid? TenantId,
    string Name,
    string Description,
    string AuthorizationReference,
    DateTime StartAt,
    DateTime EndAt,
    string ScopeDefinition,
    string[] AllowedAssessmentTypes,
    string[] ExcludedTargets,
    int? MaximumConcurrency,
    string? RateLimits);

public sealed record ProposeExecutionRequest(
    Guid EngagementId,
    Guid HypothesisId,
    string Target,
    string TestCategory,
    string ModuleId,
    string ModuleVersion,
    string? IdempotencyKey);

public sealed record RequestApprovalBody(int TtlMinutes, int? MaxUses, string? Justification);

public sealed record ApproveBody(string? Reason);

public sealed record RejectBody(string Reason);

public sealed record GrantBody(Guid ApprovalId);

public sealed record CancelBody(string? Reason);

public sealed record LeaseBody(string WorkerId, int LeaseSeconds);

public sealed record WorkerBody(string WorkerId, int ExtensionSeconds);

public sealed record CompleteBody(string ResultReference);

public sealed record EmergencyStopBody(string Reason);
