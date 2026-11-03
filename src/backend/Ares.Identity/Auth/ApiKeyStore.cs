using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Ares.Identity.Auth;

/// <summary>Non-secret description of a configured credential (safe to list and log).</summary>
public sealed record ApiKeyDescriptor(
    string Id,
    string Name,
    Guid TenantId,
    string[] Roles,
    string Fingerprint,
    bool Enabled);

/// <summary>
/// Configuration shape for one API key. Only the SHA-256 hash of the secret is
/// ever stored; the plaintext is shown once at issuance and never persisted.
/// </summary>
public sealed class ApiKeyOptions
{
    public const string SectionName = "Ares:Auth";

    public ApiKeyEntry[] Keys { get; set; } = [];

    /// <summary>Optional pepper mixed into key hashes. Supply via ARES_AUTH_PEPPER.</summary>
    public string Pepper { get; set; } = string.Empty;

    /// <summary>
    /// First-boot operator key (plaintext) supplied out-of-band via
    /// ARES_BOOTSTRAP_ADMIN_KEY. Disabled automatically once static keys exist.
    /// </summary>
    public string BootstrapAdminKey { get; set; } = string.Empty;
}

public sealed class ApiKeyEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string[] Roles { get; set; } = [];
    public string Sha256 { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

public interface IApiKeyStore
{
    /// <summary>Constant-time verification of a presented secret. Never throws on bad input.</summary>
    bool TryAuthenticate(string? presentedKey, out AresIdentity identity);

    IReadOnlyList<ApiKeyDescriptor> Describe();
}

/// <summary>
/// Validates the configured key set at construction and fails closed on any
/// malformed entry: unknown roles, duplicate ids, empty hashes, non-admin keys
/// without a tenant, or a bootstrap key that collides with a stored key.
/// </summary>
public sealed class ConfiguredApiKeyStore : IApiKeyStore
{
    private readonly List<(ApiKeyEntry Entry, AresIdentity Identity)> _entries = [];
    private readonly string _pepper;

    public ConfiguredApiKeyStore(ApiKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _pepper = options.Pepper ?? string.Empty;

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in options.Keys)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
                throw new InvalidOperationException("Ares:Auth:Keys entry is missing 'id'.");
            if (!ids.Add(entry.Id))
                throw new InvalidOperationException($"Duplicate API key id '{entry.Id}'.");
            if (string.IsNullOrWhiteSpace(entry.Sha256) || entry.Sha256.Length != 64)
                throw new InvalidOperationException($"API key '{entry.Id}' has no valid sha256 hash (expected 64 hex chars).");
            if (!hashes.Add(entry.Sha256))
                throw new InvalidOperationException($"API key '{entry.Id}' reuses another key's hash.");
            foreach (var role in entry.Roles)
                if (!AresRoles.IsKnown(role))
                    throw new InvalidOperationException($"API key '{entry.Id}' declares unknown role '{role}'.");
            if (entry.Roles.Length == 0)
                throw new InvalidOperationException($"API key '{entry.Id}' has no roles; grant an explicit role.");
            if (entry.TenantId == Guid.Empty && !entry.Roles.Contains(AresRoles.PlatformAdmin))
                throw new InvalidOperationException($"API key '{entry.Id}' has no tenant and is not platform_admin.");

            _entries.Add((entry, Build(entry.TenantId, entry.Id, entry.Name, entry.Roles, "api_key")));
        }

        var bootstrap = options.BootstrapAdminKey;
        if (!string.IsNullOrWhiteSpace(bootstrap))
        {
            var bootstrapHash = ApiKeyHasher.Hash(bootstrap, _pepper);
            if (hashes.Contains(bootstrapHash))
                throw new InvalidOperationException("Bootstrap admin key duplicates a configured key hash.");
            var entry = new ApiKeyEntry
            {
                Id = "bootstrap-admin",
                Name = "Bootstrap administrator",
                TenantId = Guid.Empty,
                Roles = [AresRoles.PlatformAdmin],
                Sha256 = bootstrapHash,
                Enabled = true
            };
            _entries.Add((entry, Build(Guid.Empty, entry.Id, entry.Name, entry.Roles, "bootstrap")));
        }
    }

    public bool TryAuthenticate(string? presentedKey, out AresIdentity identity)
    {
        identity = null!;
        if (string.IsNullOrWhiteSpace(presentedKey)) return false;

        var matched = -1;
        // Evaluate every entry: fixed work regardless of position (no timing oracle).
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (!entry.Entry.Enabled) continue;
            var ok = ApiKeyHasher.Verify(presentedKey, entry.Entry.Sha256, _pepper);
            if (ok) matched = i;
        }

        if (matched < 0) return false;
        identity = _entries[matched].Identity;
        return true;
    }

    public IReadOnlyList<ApiKeyDescriptor> Describe() =>
        _entries.Select(e => new ApiKeyDescriptor(
            e.Entry.Id, e.Entry.Name, e.Entry.TenantId, e.Entry.Roles,
            ApiKeyHasher.Fingerprint(e.Entry.Sha256), e.Entry.Enabled)).ToList();

    private static AresIdentity Build(Guid tenantId, string id, string name, string[] roles, string credentialType) =>
        new(id, string.IsNullOrWhiteSpace(name) ? id : name, tenantId,
            new HashSet<string>(roles, StringComparer.Ordinal), credentialType);

    /// <summary>
    /// Loads <see cref="ApiKeyOptions"/> from configuration and applies the
    /// fail-closed production rule: an empty key set in Production is a startup
    /// error, never an open door.
    /// </summary>
    public static ApiKeyOptions LoadOptions(IConfiguration configuration, bool isProduction)
    {
        var section = configuration.GetSection(ApiKeyOptions.SectionName);
        var options = new ApiKeyOptions
        {
            Keys = (section.GetSection("Keys").Get<ApiKeyEntry[]>() ?? []),
            // ARES_AUTH_PEPPER mirrors ARES_BOOTSTRAP_ADMIN_KEY: a root-level
            // environment variable, so it works without rewriting a config file.
            Pepper = configuration["ARES_AUTH_PEPPER"] ?? section.GetValue<string>("Pepper") ?? string.Empty,
            BootstrapAdminKey = configuration["ARES_BOOTSTRAP_ADMIN_KEY"] ?? string.Empty
        };
        if (isProduction)
        {
            if (options.Keys.Length == 0 && string.IsNullOrWhiteSpace(options.BootstrapAdminKey))
                throw new InvalidOperationException(
                    "No API keys configured. Production requires Ares:Auth:Keys or ARES_BOOTSTRAP_ADMIN_KEY.");
        }
        return options;
    }

    public static string DescribeAsJson(IEnumerable<ApiKeyDescriptor> descriptors) =>
        JsonSerializer.Serialize(descriptors, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
