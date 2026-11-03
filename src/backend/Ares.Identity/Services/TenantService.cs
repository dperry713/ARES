using Ares.Domain;
using Ares.Infrastructure.Journal;

namespace Ares.Identity.Services;

public interface ITenantService
{
    Task<Tenant> CreateAsync(string name, string description, string configuration, string retentionPolicy);
    Task<Tenant> GetByIdAsync(Guid tenantId);
    Task<IReadOnlyList<Tenant>> ListAsync();
    Task<Tenant> SuspendAsync(Guid tenantId, string reason);
}

/// <summary>
/// Tenant registry. Suspension is terminal for authorization purposes: the
/// policy engine refuses to permit anything for a suspended tenant.
/// </summary>
public class TenantService : ITenantService
{
    private readonly Dictionary<Guid, Tenant> _tenants;
    private readonly object _lock = new();
    private readonly IStateJournal? _journal;

    public TenantService(IStateJournal? journal = null)
    {
        _journal = journal;
        _tenants = JournalReplay.LatestById<Tenant>(journal, "tenant/");
    }

    public Task<Tenant> CreateAsync(string name, string description, string configuration, string retentionPolicy)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tenant name required.", nameof(name));

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            Configuration = configuration ?? string.Empty,
            RetentionPolicy = string.IsNullOrWhiteSpace(retentionPolicy) ? "default" : retentionPolicy
        };

        lock (_lock)
        {
            Persist(tenant);
            return Task.FromResult(tenant);
        }
    }

    public Task<Tenant> GetByIdAsync(Guid tenantId)
    {
        lock (_lock)
        {
            if (_tenants.TryGetValue(tenantId, out var tenant))
                return Task.FromResult(tenant);
            throw new KeyNotFoundException($"Tenant {tenantId} not found");
        }
    }

    public Task<IReadOnlyList<Tenant>> ListAsync()
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<Tenant>>(_tenants.Values.OrderBy(t => t.Name, StringComparer.Ordinal).ToList());
        }
    }

    public Task<Tenant> SuspendAsync(Guid tenantId, string reason)
    {
        lock (_lock)
        {
            if (!_tenants.TryGetValue(tenantId, out var tenant))
                throw new KeyNotFoundException($"Tenant {tenantId} not found");
            if (tenant.Status == "Suspended")
                throw new InvalidOperationException($"Tenant {tenantId} is already suspended.");

            var suspended = tenant with { Status = "Suspended" };
            Persist(suspended);
            _ = reason; // recorded by the caller's audit entry; kept for API symmetry
            return Task.FromResult(suspended);
        }
    }

    private void Persist(Tenant tenant)
    {
        _tenants[tenant.Id] = tenant;
        _journal?.Append($"tenant/{tenant.Id}", "state", JournalJson.Serialize(tenant), JournalActor.Name);
    }
}
