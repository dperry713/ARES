// Test host for the real ARES API: real composition root, real SQLite journal,
// real authentication/authorization policies. Only the data directory and the
// credential set are injected, which is exactly how a deployment supplies them.
//
// Configuration is injected as environment variables because the platform
// composition root reads configuration inside WebApplication.CreateBuilder;
// generic-host ConfigureAppConfiguration callbacks are applied too late to be
// visible there (minimal-hosting ordering).
//
// Tenants are seeded straight into the state journal (the same projection the
// process replays at boot) because tenant ids must be known before the
// credential set is validated at startup.
using Ares.Domain;
using Ares.Identity.Auth;
using Ares.Infrastructure.Journal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Ares.ApiTests;

public sealed class AresApiFactory : WebApplicationFactory<Program>
{
    private readonly List<string> _environmentVariables = [];

    public AresApiFactory()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "ares-api-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DataDirectory);

        TenantA = Guid.NewGuid();
        TenantB = Guid.NewGuid();
        TenantSuspended = Guid.NewGuid();

        SeedTenant(TenantA, "Tenant A", "Active");
        SeedTenant(TenantB, "Tenant B", "Active");
        SeedTenant(TenantSuspended, "Tenant Suspended", "Suspended");

        AdminKey = ApiKeyHasher.GenerateKey();
        ManagerAKey = ApiKeyHasher.GenerateKey();
        ApproverAKey = ApiKeyHasher.GenerateKey();
        WorkerAKey = ApiKeyHasher.GenerateKey();
        AuditorAKey = ApiKeyHasher.GenerateKey();
        ManagerBKey = ApiKeyHasher.GenerateKey();
        DualRoleAKey = ApiKeyHasher.GenerateKey();
        SuspendedKey = ApiKeyHasher.GenerateKey();

        SetEnvironmentVariable("ARES_DATA_DIR", DataDirectory);
        SetEnvironmentVariable("Ares__RateLimit__PermitPerMinute", "10000");

        AddKey(0, "admin", AdminKey, Guid.Empty, AresRoles.PlatformAdmin);
        AddKey(1, "manager-a", ManagerAKey, TenantA, AresRoles.EngagementManager);
        AddKey(2, "approver-a", ApproverAKey, TenantA, AresRoles.Approver);
        AddKey(3, "worker-a", WorkerAKey, TenantA, AresRoles.Worker);
        AddKey(4, "auditor-a", AuditorAKey, TenantA, AresRoles.Auditor);
        AddKey(5, "manager-b", ManagerBKey, TenantB, AresRoles.EngagementManager);
        AddKey(6, "dual-a", DualRoleAKey, TenantA, AresRoles.EngagementManager, AresRoles.Approver);
        AddKey(7, "suspended", SuspendedKey, TenantSuspended, AresRoles.EngagementManager);
    }

    public string DataDirectory { get; }
    public Guid TenantA { get; }
    public Guid TenantB { get; }
    public Guid TenantSuspended { get; }

    public string AdminKey { get; }
    public string ManagerAKey { get; }
    public string ApproverAKey { get; }
    public string WorkerAKey { get; }
    public string AuditorAKey { get; }
    public string ManagerBKey { get; }
    public string DualRoleAKey { get; }
    public string SuspendedKey { get; }

    public HttpClient ClientFor(string apiKey)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Remove("X-Ares-Api-Key");
        client.DefaultRequestHeaders.Add("X-Ares-Api-Key", apiKey);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment(Environments.Development);

    private void SetEnvironmentVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _environmentVariables.Add(name);
    }

    private void AddKey(int index, string id, string secret, Guid tenantId, params string[] roles)
    {
        var prefix = $"Ares__Auth__Keys__{index}";
        SetEnvironmentVariable($"{prefix}__Id", id);
        SetEnvironmentVariable($"{prefix}__Name", id);
        SetEnvironmentVariable($"{prefix}__TenantId", tenantId.ToString("D"));
        SetEnvironmentVariable($"{prefix}__Sha256", ApiKeyHasher.Hash(secret));
        SetEnvironmentVariable($"{prefix}__Enabled", "true");
        for (var i = 0; i < roles.Length; i++)
            SetEnvironmentVariable($"{prefix}__Roles__{i}", roles[i]);
    }

    private void SeedTenant(Guid id, string name, string status)
    {
        using var journal = new SqliteStateJournal(Path.Combine(DataDirectory, "ares.journal.db"));
        var tenant = new Tenant
        {
            Id = id,
            Name = name,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            Configuration = "{}",
            RetentionPolicy = "default"
        };
        journal.Append($"tenant/{id}", "state", JournalJson.Serialize(tenant), "test-seed");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        foreach (var name in _environmentVariables)
            Environment.SetEnvironmentVariable(name, null);

        try
        {
            if (Directory.Exists(DataDirectory)) Directory.Delete(DataDirectory, recursive: true);
        }
        catch (IOException)
        {
            // The host may still hold the SQLite handle; leftover temp files are harmless.
        }
    }
}
