// Shared test harness.
//
// Behind the F-02 fix every execution transition re-reads the engagement from
// the engagement service. Tests therefore cannot invent engagement ids: they
// must create a real Active engagement inside its authorized window, with a
// real category allow-list, and then bind a real approval record to the exact
// server-computed operation hash.
//
// The harness exists so every suite exercises the production wiring instead of
// constructing partially-wired services that would silently diverge from it.
using Ares.Approvals.Services;
using Ares.Domain;
using Ares.Engagements;
using Ares.Execution.Services;
using Ares.Infrastructure.Journal;
using Ares.Policy.Services;
using DomainExecution = Ares.Domain.Execution;

namespace Ares.UnitTests;

internal sealed class AresHarness
{
    public const string Target = "staging.example.test";
    public const string Category = "safe_http_configuration_checks";
    public const string ModuleId = "http.security.headers";
    public const string ModuleVersion = "1.2.0";
    public const string Requester = "operator-1";
    public const string Approver = "approver-1";

    private AresHarness(InMemoryStateJournal journal, Guid tenantId)
    {
        Journal = journal;
        TenantId = tenantId;
        Engagements = new EngagementService(journal);
        Approvals = new ApprovalService(journal);
        Policy = new PolicyEngine();
        Executions = new ExecutionService(Policy, Engagements, Approvals, journal);
    }

    public InMemoryStateJournal Journal { get; }
    public Guid TenantId { get; }
    public EngagementService Engagements { get; }
    public ApprovalService Approvals { get; }
    public PolicyEngine Policy { get; }
    public ExecutionService Executions { get; }

    public Guid EngagementId { get; private set; }

    /// <summary>Creates the default harness with one Active engagement.</summary>
    public static async Task<AresHarness> CreateAsync(
        string scope = Target,
        string[]? allowedCategories = null,
        string[]? excludedTargets = null,
        DateTime? startAt = null,
        DateTime? endAt = null,
        Guid? tenantId = null)
    {
        var harness = new AresHarness(new InMemoryStateJournal(), tenantId ?? Guid.NewGuid());
        await harness.CreateActiveEngagementAsync(scope, allowedCategories, excludedTargets, startAt, endAt);
        return harness;
    }

    public async Task<Engagement> CreateActiveEngagementAsync(
        string scope = Target,
        string[]? allowedCategories = null,
        string[]? excludedTargets = null,
        DateTime? startAt = null,
        DateTime? endAt = null)
    {
        var now = DateTime.UtcNow;
        var engagement = await Engagements.CreateAsync(
            TenantId,
            "harness engagement",
            "created by the unit-test harness",
            "AR-TEST-001",
            startAt ?? now.AddMinutes(-5),
            endAt ?? now.AddHours(6),
            scope,
            allowedCategories ?? [Category],
            excludedTargets ?? Array.Empty<string>());

        await Engagements.AuthorizeAsync(engagement.Id);
        engagement = await Engagements.StartAsync(engagement.Id);
        EngagementId = engagement.Id;
        return engagement;
    }

    public Task<Engagement> PauseEngagementAsync() => Engagements.PauseAsync(EngagementId);

    public Task<DomainExecution> ProposeAsync(
        Guid? engagementId = null,
        string target = Target,
        string category = Category,
        string moduleId = ModuleId,
        string moduleVersion = ModuleVersion,
        string? idempotencyKey = null,
        Guid? hypothesisId = null) =>
        Executions.ProposeAsync(engagementId ?? EngagementId, hypothesisId ?? Guid.NewGuid(), target, category,
            moduleId, moduleVersion, idempotencyKey);

    /// <summary>Proposes, requests approval, and returns a Proposed execution.</summary>
    public async Task<DomainExecution> ProposedAsync(string? idempotencyKey = null) =>
        await ProposeAsync(idempotencyKey: idempotencyKey);

    /// <summary>Moves an execution to AwaitingApproval.</summary>
    public Task<DomainExecution> RequestApprovalAsync(Guid executionId) =>
        Executions.RequestApprovalAsync(executionId);

    /// <summary>
    /// Creates a real approval bound to the execution's stored operation hash,
    /// has a second human approve it, and grants it. The requester and approver
    /// differ by default, satisfying separation of duties.
    /// </summary>
    public async Task<(DomainExecution Execution, Approval Approval)> ApprovedExecutionAsync(
        string requester = Requester,
        string approver = Approver,
        TimeSpan? ttl = null,
        string? idempotencyKey = null)
    {
        var execution = await ProposeAsync(idempotencyKey: idempotencyKey);
        execution = await RequestApprovalAsync(execution.Id);

        var approval = await Approvals.RequestAsync(
            execution.Id, execution.EngagementId, execution.TenantId, execution.Target,
            execution.ModuleId, execution.ModuleVersion, execution.OperationHash,
            requester, ttl ?? TimeSpan.FromMinutes(30));
        approval = await Approvals.ApproveAsync(approval.Id, approver);
        execution = await Executions.GrantApprovalAsync(execution.Id, approval.Id);

        return (execution, approval);
    }

    /// <summary>Approved execution that has passed the queue transition.</summary>
    public async Task<DomainExecution> QueuedExecutionAsync(string? idempotencyKey = null)
    {
        var (execution, _) = await ApprovedExecutionAsync(idempotencyKey: idempotencyKey);
        return await Executions.QueueAsync(execution.Id);
    }

    /// <summary>Approved execution leased and started (Running).</summary>
    public async Task<DomainExecution> RunningExecutionAsync(string workerId = "worker-1")
    {
        var execution = await QueuedExecutionAsync();
        await Executions.LeaseAsync(execution.Id, workerId, TimeSpan.FromMinutes(5));
        return await Executions.StartAsync(execution.Id);
    }
}
