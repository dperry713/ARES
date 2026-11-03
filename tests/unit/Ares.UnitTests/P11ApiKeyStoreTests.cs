// F-01 (authentication half) tests: credential store and key handling.
//
// The production rule is fail-closed: no configured keys in Production is a
// startup error. Keys are only ever stored as SHA-256 hashes, comparison is
// constant-time, and malformed key sets are rejected at construction rather
// than silently ignored.
using Ares.Identity.Auth;
using Ares.Identity.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ares.UnitTests;

public sealed class ApiKeyStoreTests
{
    private static ApiKeyEntry Entry(string id, string key, Guid tenantId, params string[] roles) => new()
    {
        Id = id,
        Name = id,
        TenantId = tenantId,
        Roles = roles,
        Sha256 = ApiKeyHasher.Hash(key),
        Enabled = true
    };

    [Fact]
    public void Production_without_any_key_fails_closed()
    {
        var configuration = new ConfigurationBuilder().Build();
        var error = Assert.Throws<InvalidOperationException>(
            () => ConfiguredApiKeyStore.LoadOptions(configuration, isProduction: true));
        Assert.Contains("Production requires", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Non_production_without_keys_is_allowed_for_local_development()
    {
        var configuration = new ConfigurationBuilder().Build();
        var options = ConfiguredApiKeyStore.LoadOptions(configuration, isProduction: false);
        Assert.Empty(options.Keys);
    }

    [Fact]
    public void Bootstrap_key_authenticates_as_platform_admin()
    {
        const string key = "ares_bootstrap_secret_value";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ARES_BOOTSTRAP_ADMIN_KEY"] = key
            })
            .Build();

        var options = ConfiguredApiKeyStore.LoadOptions(configuration, isProduction: true);
        var store = new ConfiguredApiKeyStore(options);

        Assert.True(store.TryAuthenticate(key, out var identity));
        Assert.True(identity.IsPlatformAdmin);
        Assert.Equal("bootstrap", identity.CredentialType);

        Assert.False(store.TryAuthenticate("ares_wrong", out _));
        Assert.False(store.TryAuthenticate(null, out _));
        Assert.False(store.TryAuthenticate("   ", out _));
    }

    [Fact]
    public void Pepper_is_read_from_the_root_environment_variable()
    {
        var tenant = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ARES_AUTH_PEPPER"] = "pepper-in-env",
                ["Ares:Auth:Keys:0:Id"] = "svc",
                ["Ares:Auth:Keys:0:Name"] = "svc",
                ["Ares:Auth:Keys:0:TenantId"] = tenant.ToString("D"),
                ["Ares:Auth:Keys:0:Roles:0"] = AresRoles.Worker,
                ["Ares:Auth:Keys:0:Sha256"] = ApiKeyHasher.Hash("ares_secret", "pepper-in-env")
            })
            .Build();

        var options = ConfiguredApiKeyStore.LoadOptions(configuration, isProduction: true);
        Assert.Equal("pepper-in-env", options.Pepper);

        var store = new ConfiguredApiKeyStore(options);
        Assert.True(store.TryAuthenticate("ares_secret", out var identity));
        Assert.Equal(tenant, identity.TenantId);

        // The peppered hash must not verify when the pepper is absent: a stolen
        // configuration cannot be replayed without the deployment's pepper.
        var unpeppered = new ConfiguredApiKeyStore(new ApiKeyOptions
        {
            Keys =
            [
                new ApiKeyEntry
                {
                    Id = "svc",
                    Name = "svc",
                    TenantId = tenant,
                    Roles = [AresRoles.Worker],
                    Sha256 = options.Keys[0].Sha256
                }
            ]
        });
        Assert.False(unpeppered.TryAuthenticate("ares_secret", out _));
    }

    [Fact]
    public void Configured_key_carries_its_tenant_and_roles()
    {
        var tenant = Guid.NewGuid();
        const string key = "ares_tenant_key_value";
        var store = new ConfiguredApiKeyStore(new ApiKeyOptions
        {
            Keys = [Entry("svc-worker", key, tenant, AresRoles.Worker)]
        });

        Assert.True(store.TryAuthenticate(key, out var identity));
        Assert.Equal(tenant, identity.TenantId);
        Assert.True(identity.HasRole(AresRoles.Worker));
        Assert.False(identity.IsPlatformAdmin);
        Assert.True(identity.CanAccessTenant(tenant));
        Assert.False(identity.CanAccessTenant(Guid.NewGuid()));
    }

    [Fact]
    public void Malformed_key_sets_are_rejected_at_construction()
    {
        var tenant = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => new ConfiguredApiKeyStore(new ApiKeyOptions
        {
            Keys = [Entry("k1", "ares_a", tenant, "superuser")]
        }));

        Assert.Throws<InvalidOperationException>(() => new ConfiguredApiKeyStore(new ApiKeyOptions
        {
            Keys = [Entry("k1", "ares_a", tenant)]
        }));

        Assert.Throws<InvalidOperationException>(() => new ConfiguredApiKeyStore(new ApiKeyOptions
        {
            Keys = [Entry("k1", "ares_a", tenant, AresRoles.Worker), Entry("k1", "ares_b", tenant, AresRoles.Worker)]
        }));

        Assert.Throws<InvalidOperationException>(() => new ConfiguredApiKeyStore(new ApiKeyOptions
        {
            Keys = [Entry("k1", "ares_a", Guid.Empty, AresRoles.Worker)]
        }));

        // Duplicate hash: two ids pointing at the same secret.
        var duplicateHash = ApiKeyHasher.Hash("ares_same");
        Assert.Throws<InvalidOperationException>(() => new ConfiguredApiKeyStore(new ApiKeyOptions
        {
            Keys =
            [
                new ApiKeyEntry { Id = "a", TenantId = tenant, Roles = [AresRoles.Worker], Sha256 = duplicateHash },
                new ApiKeyEntry { Id = "b", TenantId = tenant, Roles = [AresRoles.Worker], Sha256 = duplicateHash }
            ]
        }));
    }

    [Fact]
    public void Descriptors_never_contain_the_presented_secret()
    {
        const string key = "ares_never_log_me";
        var store = new ConfiguredApiKeyStore(new ApiKeyOptions
        {
            Keys = [Entry("k1", key, Guid.NewGuid(), AresRoles.Auditor)]
        });

        var json = ConfiguredApiKeyStore.DescribeAsJson(store.Describe());
        Assert.DoesNotContain(key, json, StringComparison.Ordinal);
        Assert.Equal(ApiKeyHasher.Hash(key)[..12], store.Describe()[0].Fingerprint);
    }

    [Fact]
    public void Hasher_verifies_only_the_exact_key_and_honours_the_pepper()
    {
        var key = ApiKeyHasher.GenerateKey();
        Assert.StartsWith("ares_", key, StringComparison.Ordinal);

        var hash = ApiKeyHasher.Hash(key, "pepper");
        Assert.True(ApiKeyHasher.Verify(key, hash, "pepper"));
        Assert.False(ApiKeyHasher.Verify(key, hash, "other-pepper"));
        Assert.False(ApiKeyHasher.Verify(key + "x", hash, "pepper"));
        Assert.False(ApiKeyHasher.Verify("", hash, "pepper"));
    }

    [Fact]
    public async Task Suspending_a_tenant_is_recorded_and_terminal()
    {
        var tenantService = new TenantService();
        var tenant = await tenantService.CreateAsync("Acme", "d", "{}", "default");
        Assert.Equal("Active", tenant.Status);

        var suspended = await tenantService.SuspendAsync(tenant.Id, "contract ended");
        Assert.Equal("Suspended", suspended.Status);
        Assert.Equal("Suspended", (await tenantService.GetByIdAsync(tenant.Id)).Status);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => tenantService.SuspendAsync(tenant.Id, "again"));
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => tenantService.GetByIdAsync(Guid.NewGuid()));
    }
}
