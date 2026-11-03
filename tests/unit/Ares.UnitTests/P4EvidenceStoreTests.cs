// P4 evidence-store tests (blueprint §6.4, MEC §13).
// Requirements: (a) SHA-256 hash computed over the canonical bytes at ingest
// and re-verifiable on demand; (b) hash-chained append (each record commits to
// the previous record's hash, per engagement chain) so deletion/reorder/tamper
// is detectable; (c) strict tenant isolation — a read with the wrong tenant
// fails, never leaks; (d) export manifest with per-record hashes + chain head
// for independent verification. Written FIRST (RED): no such service exists.
using Ares.Domain;
using Ares.Evidence.Services;
using Xunit;

namespace Ares.UnitTests;

public sealed class EvidenceStoreTests
{
    private static (EvidenceService svc, Guid tenant, Guid engagement) Setup()
    {
        var svc = new EvidenceService();
        return (svc, Guid.NewGuid(), Guid.NewGuid());
    }

    [Fact]
    public async Task Ingest_computes_sha256_and_verify_passes()
    {
        var (svc, tenant, engagement) = Setup();
        var rec = await svc.IngestAsync(tenant, engagement, Guid.NewGuid(),
            "test_result", "module http.security.headers v1.2.0 observation",
            "Internal", "worker w-1");
        Assert.NotEqual(Guid.Empty, rec.Id);
        Assert.Equal(64, rec.SHA256.Length); // hex SHA-256
        var ok = await svc.VerifyAsync(rec.Id, tenant);
        Assert.True(ok);
    }

    [Fact]
    public async Task Records_form_a_hash_chain_second_commits_to_first()
    {
        var (svc, tenant, engagement) = Setup();
        var first = await svc.IngestAsync(tenant, engagement, Guid.NewGuid(),
            "test_result", "observation-one", "Internal", "worker w-1");
        var second = await svc.IngestAsync(tenant, engagement, Guid.NewGuid(),
            "test_result", "observation-two", "Internal", "worker w-1");
        Assert.NotNull(second.PreviousHash);
        Assert.Equal(first.RecordHash, second.PreviousHash);
        Assert.True(await svc.VerifyChainAsync(engagement, tenant));
    }

    [Fact]
    public async Task Tamper_is_detected_chain_verification_fails()
    {
        var (svc, tenant, engagement) = Setup();
        var first = await svc.IngestAsync(tenant, engagement, Guid.NewGuid(),
            "test_result", "untampered", "Internal", "worker w-1");
        await svc.IngestAsync(tenant, engagement, Guid.NewGuid(),
            "test_result", "second", "Internal", "worker w-1");
        svc.CorruptForTest(first.Id, "TAMPERED-CONTENT"); // test-only backdoor
        Assert.False(await svc.VerifyAsync(first.Id, tenant));
        Assert.False(await svc.VerifyChainAsync(engagement, tenant));
    }

    [Fact]
    public async Task Wrong_tenant_cannot_read_or_verify()
    {
        var (svc, tenant, engagement) = Setup();
        var rec = await svc.IngestAsync(tenant, engagement, Guid.NewGuid(),
            "test_result", "secret-observation", "Internal", "worker w-1");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetAsync(rec.Id, Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.VerifyAsync(rec.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task Export_manifest_verifies_independently()
    {
        var (svc, tenant, engagement) = Setup();
        await svc.IngestAsync(tenant, engagement, Guid.NewGuid(),
            "test_result", "one", "Internal", "worker w-1");
        await svc.IngestAsync(tenant, engagement, Guid.NewGuid(),
            "test_result", "two", "Internal", "worker w-1");
        var manifest = await svc.ExportManifestAsync(engagement, tenant);
        Assert.Equal(2, manifest.Records.Count);
        Assert.False(string.IsNullOrWhiteSpace(manifest.ChainHead));
        // Independent re-verification from the manifest alone (no service trust).
        Assert.True(EvidenceService.VerifyManifest(manifest));
    }

    [Fact]
    public async Task Empty_content_and_missing_record_fail_closed()
    {
        var (svc, tenant, engagement) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.IngestAsync(tenant, engagement, Guid.NewGuid(), "test_result", "", "Internal", "w"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.GetAsync(Guid.NewGuid(), tenant));
    }
}
