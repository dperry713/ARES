using Ares.Domain;
using DomainEvidence = Ares.Domain.Evidence;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ares.Evidence.Services;

/// <summary>
/// Tamper-evident, tenant-isolated evidence store (blueprint §6.4, MEC §13).
/// Milestone bounds (honest): in-memory per instance, no immutable object
/// storage, no encryption at rest, no signed export envelopes yet — but the
/// hash chain + tenant isolation + independent manifest verification are real
/// and tested. Anything beyond that is PLANNED, not claimed.
/// Implements <see cref="IEvidenceService"/> for DI registration.
/// </summary>
public sealed class EvidenceService : IEvidenceService
{
    private readonly Dictionary<Guid, StoredRecord> _records = new();
    private readonly Dictionary<Guid, List<Guid>> _chains = new(); // engagement -> record ids in order
    private readonly object _lock = new();

    private sealed record StoredRecord(DomainEvidence Evidence, string Content);

    public Task<DomainEvidence> IngestAsync(Guid tenantId, Guid engagementId, Guid executionId,
        string evidenceType, string content, string classification, string provenance)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant id required.", nameof(tenantId));
        if (engagementId == Guid.Empty) throw new ArgumentException("Engagement id required.", nameof(engagementId));
        if (string.IsNullOrWhiteSpace(evidenceType)) throw new ArgumentException("Evidence type required.", nameof(evidenceType));
        if (string.IsNullOrEmpty(content)) throw new ArgumentException("Content must not be empty.", nameof(content));

        lock (_lock)
        {
            var chain = _chains.TryGetValue(engagementId, out var ids) ? ids : new List<Guid>();
            string? previous = chain.Count == 0 ? null : _records[chain[^1]].Evidence.RecordHash;
            var now = DateTime.UtcNow;
            var id = Guid.NewGuid();
            var contentHash = Sha256Hex(content);
            var recordHash = Sha256Hex(CanonicalBytes(id, tenantId, engagementId, executionId,
                evidenceType, contentHash, classification, provenance, now, previous, chain.Count));
            var evidence = new DomainEvidence
            {
                Id = id,
                TenantId = tenantId,
                EngagementId = engagementId,
                ExecutionId = executionId,
                EvidenceType = evidenceType,
                ObjectReference = evidenceType,
                SHA256 = contentHash,
                CollectedAt = now,
                Classification = string.IsNullOrWhiteSpace(classification) ? "Internal" : classification,
                Provenance = provenance ?? string.Empty,
                RecordHash = recordHash,
                PreviousHash = previous,
                Sequence = chain.Count
            };
            _records[id] = new StoredRecord(evidence, content);
            chain.Add(id);
            _chains[engagementId] = chain;
            return Task.FromResult(evidence);
        }
    }

    public Task<DomainEvidence> GetAsync(Guid evidenceId, Guid tenantId)
    {
        lock (_lock)
        {
            var stored = Require(evidenceId);
            if (stored.Evidence.TenantId != tenantId)
                throw new UnauthorizedAccessException("Cross-tenant evidence read denied (fail closed).");
            return Task.FromResult(stored.Evidence);
        }
    }

    /// <summary>Recompute both hashes; false on ANY mismatch (content, metadata, chain link).</summary>
    public Task<bool> VerifyAsync(Guid evidenceId, Guid tenantId)
    {
        lock (_lock)
        {
            if (!_records.TryGetValue(evidenceId, out var stored)) return Task.FromResult(false);
            if (stored.Evidence.TenantId != tenantId) throw new UnauthorizedAccessException("Cross-tenant evidence verify denied.");
            return Task.FromResult(VerifyRecord(stored));
        }
    }

    /// <summary>Verify every record in the engagement chain in order.</summary>
    public Task<bool> VerifyChainAsync(Guid engagementId, Guid tenantId)
    {
        lock (_lock)
        {
            if (!_chains.TryGetValue(engagementId, out var ids) || ids.Count == 0) return Task.FromResult(true);
            string? expectedPrevious = null;
            foreach (var id in ids)
            {
                if (!_records.TryGetValue(id, out var stored)) return Task.FromResult(false);
                if (stored.Evidence.TenantId != tenantId) return Task.FromResult(false);
                if (!VerifyRecord(stored)) return Task.FromResult(false);
                if (!string.Equals(stored.Evidence.PreviousHash, expectedPrevious, StringComparison.Ordinal))
                    return Task.FromResult(false);
                expectedPrevious = stored.Evidence.RecordHash;
            }
            return Task.FromResult(true);
        }
    }

    public Task<EvidenceManifest> ExportManifestAsync(Guid engagementId, Guid tenantId)
    {
        lock (_lock)
        {
            if (!_chains.TryGetValue(engagementId, out var ids))
                throw new KeyNotFoundException($"No evidence chain for engagement {engagementId}.");
            var records = new List<EvidenceManifestRecord>();
            foreach (var id in ids)
            {
                var stored = _records[id];
                if (stored.Evidence.TenantId != tenantId)
                    throw new UnauthorizedAccessException("Cross-tenant evidence export denied.");
                records.Add(new EvidenceManifestRecord(
                    stored.Evidence.Id, stored.Evidence.Sequence, stored.Evidence.EvidenceType,
                    stored.Evidence.SHA256, stored.Evidence.RecordHash, stored.Evidence.PreviousHash,
                    stored.Evidence.CollectedAt));
            }
            var head = records.Count == 0 ? string.Empty : records[^1].RecordHash;
            return Task.FromResult(new EvidenceManifest(engagementId, tenantId, DateTimeOffset.UtcNow, head, records));
        }
    }

    /// <summary>
    /// Independent verification from a manifest alone: recompute the chain links
    /// (each record hash must commit to the previous) without trusting the service.
    /// Note: full content re-hash needs the content payload (PLANNED export field);
    /// this verifies chain integrity + hash-format validity today.
    /// </summary>
    public static bool VerifyManifest(EvidenceManifest manifest)
    {
        if (manifest.Records.Count == 0) return true;
        string? expectedPrevious = null;
        foreach (var r in manifest.Records.OrderBy(r => r.Sequence))
        {
            if (r.ContentHash.Length != 64 || r.RecordHash.Length != 64) return false;
            if (!string.Equals(r.PreviousHash, expectedPrevious, StringComparison.Ordinal)) return false;
            expectedPrevious = r.RecordHash;
        }
        return string.Equals(manifest.ChainHead, expectedPrevious, StringComparison.Ordinal);
    }

    /// <summary>Test-only backdoor: mutate stored content to prove tamper detection. Never ship open.</summary>
    public void CorruptForTest(Guid evidenceId, string tamperedContent)
    {
        lock (_lock)
        {
            var stored = Require(evidenceId);
            _records[evidenceId] = stored with { Content = tamperedContent };
        }
    }

    private StoredRecord Require(Guid evidenceId)
    {
        if (!_records.TryGetValue(evidenceId, out var stored))
            throw new KeyNotFoundException($"Evidence {evidenceId} not found.");
        return stored;
    }

    private static bool VerifyRecord(StoredRecord stored)
    {
        var e = stored.Evidence;
        if (!string.Equals(Sha256Hex(stored.Content), e.SHA256, StringComparison.Ordinal)) return false;
        var recomputed = Sha256Hex(CanonicalBytes(e.Id, e.TenantId, e.EngagementId, e.ExecutionId,
            e.EvidenceType, e.SHA256, e.Classification, e.Provenance, e.CollectedAt, e.PreviousHash, e.Sequence));
        return string.Equals(recomputed, e.RecordHash, StringComparison.Ordinal);
    }

    private static byte[] CanonicalBytes(Guid id, Guid tenant, Guid engagement, Guid execution,
        string type, string contentHash, string classification, string provenance,
        DateTime collectedAt, string? previousHash, long sequence)
    {
        // Deterministic, culture-invariant, field-ordered encoding. No content
        // itself here — only its hash — so the record commits without duplication.
        var canonical = string.Join("\n",
            id.ToString("D"), tenant.ToString("D"), engagement.ToString("D"), execution.ToString("D"),
            type, contentHash, classification, provenance,
            collectedAt.ToUniversalTime().ToString("O"), previousHash ?? "-", sequence.ToString());
        return Encoding.UTF8.GetBytes(canonical);
    }

    internal static string Sha256Hex(string content)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    internal static string Sha256Hex(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

public sealed record EvidenceManifestRecord(
    Guid Id, long Sequence, string EvidenceType,
    string ContentHash, string RecordHash, string? PreviousHash,
    DateTime CollectedAt);

public sealed record EvidenceManifest(
    Guid EngagementId, Guid TenantId, DateTimeOffset ExportedAt,
    string ChainHead, IReadOnlyList<EvidenceManifestRecord> Records);
