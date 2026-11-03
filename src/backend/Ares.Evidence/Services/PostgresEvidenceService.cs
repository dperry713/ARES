using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DomainEvidence = Ares.Domain.Evidence;
using Npgsql;

namespace Ares.Evidence.Services;

/// <summary>
/// Tamper-evident, tenant-isolated evidence store with PostgreSQL persistence.
/// Survives restarts and supports multi-instance deployments.
/// Blueprint §6.4, MEC §13 compliance.
/// </summary>
public sealed class PostgresEvidenceService
{
    private readonly string _connectionString;

    public PostgresEvidenceService(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task<DomainEvidence> IngestAsync(Guid tenantId, Guid engagementId, Guid executionId,
        string evidenceType, string content, string classification, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceType);
        if (string.IsNullOrEmpty(content)) throw new ArgumentException("Content must not be empty.", nameof(content));

        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        using var lockCmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(12345);", conn);
        await lockCmd.ExecuteNonQueryAsync();

        using var headCmd = new NpgsqlCommand(
            "SELECT sequence, record_hash FROM evidence_records WHERE engagement_id = $eng ORDER BY sequence DESC LIMIT 1 FOR UPDATE;", conn);
        headCmd.Parameters.AddWithValue("$eng", engagementId);
        using var headReader = await headCmd.ExecuteReaderAsync();
        long nextSeq;
        string? previousHash;
        if (await headReader.ReadAsync())
        {
            nextSeq = headReader.GetInt64(0) + 1;
            previousHash = headReader.GetString(1);
        }
        else
        {
            nextSeq = 1;
            previousHash = null;
        }

        var now = DateTime.UtcNow;
        var contentHash = Sha256Hex(content);
        var id = Guid.NewGuid();
        var recordHash = Sha256Hex(CanonicalBytes(id, tenantId, engagementId, executionId,
            evidenceType, contentHash, classification, provenance, now, previousHash, nextSeq - 1));

        using var insertCmd = new NpgsqlCommand(@"
            INSERT INTO evidence_records (id, tenant_id, engagement_id, execution_id, evidence_type, sha256, record_hash, previous_hash, collected_at, sequence)
            VALUES ($id, $tenant, $eng, $exec, $etype, $sha256, $rhash, $prev, $at, $seq)", conn);
        insertCmd.Parameters.AddWithValue("$id", id);
        insertCmd.Parameters.AddWithValue("$tenant", tenantId);
        insertCmd.Parameters.AddWithValue("$eng", engagementId);
        insertCmd.Parameters.AddWithValue("$exec", (object?)executionId ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$etype", evidenceType);
        insertCmd.Parameters.AddWithValue("$sha256", contentHash);
        insertCmd.Parameters.AddWithValue("$rhash", recordHash);
        insertCmd.Parameters.AddWithValue("$prev", (object?)previousHash ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("$at", now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        insertCmd.Parameters.AddWithValue("$seq", nextSeq);
        await insertCmd.ExecuteNonQueryAsync();

        return new DomainEvidence
        {
            Id = id,
            TenantId = tenantId,
            EngagementId = engagementId,
            ExecutionId = executionId,
            EvidenceType = evidenceType,
            SHA256 = contentHash,
            RecordHash = recordHash,
            PreviousHash = previousHash,
            Classification = string.IsNullOrWhiteSpace(classification) ? "Internal" : classification,
            Provenance = provenance ?? string.Empty,
            CollectedAt = now,
            Sequence = nextSeq
        };
    }

    public async Task<DomainEvidence> GetAsync(Guid evidenceId, Guid tenantId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand(@"
            SELECT id, tenant_id, engagement_id, execution_id, evidence_type, sha256, record_hash, previous_hash, collected_at, sequence
            FROM evidence_records WHERE id = $id", conn);
        cmd.Parameters.AddWithValue("$id", evidenceId);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new KeyNotFoundException($"Evidence {evidenceId} not found.");

        if (reader.GetGuid(1) != tenantId) throw new UnauthorizedAccessException("Cross-tenant evidence read denied.");

        return new DomainEvidence
        {
            Id = reader.GetGuid(0),
            TenantId = reader.GetGuid(1),
            EngagementId = reader.GetGuid(2),
            ExecutionId = reader.IsDBNull(3) ? Guid.Empty : reader.GetGuid(3),
            EvidenceType = reader.GetString(4),
            SHA256 = reader.GetString(5),
            RecordHash = reader.GetString(6),
            PreviousHash = reader.IsDBNull(7) ? null : reader.GetString(7),
            Classification = reader.GetString(8),
            Provenance = reader.GetString(9),
            CollectedAt = reader.GetDateTime(10),
            Sequence = reader.GetInt64(11)
        };
    }

    public async Task<bool> VerifyAsync(Guid evidenceId, Guid tenantId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand(@"
            SELECT tenant_id, sha256, record_hash, previous_hash, collected_at, sequence
            FROM evidence_records WHERE id = $id", conn);
        cmd.Parameters.AddWithValue("$id", evidenceId);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return false;
        if (reader.GetGuid(0) != tenantId) throw new UnauthorizedAccessException("Cross-tenant evidence verify denied.");

        // Recompute record hash
        var recomputed = Sha256Hex(CanonicalBytes(
            reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3),
            reader.GetGuid(4), reader.GetString(5), reader.GetString(6),
            reader.GetString(7), reader.GetString(8),
            reader.GetDateTime(9), reader.IsDBNull(10) ? null : reader.GetString(10), reader.GetInt64(11)));

        return string.Equals(recomputed, reader.GetString(6), StringComparison.Ordinal);
    }

    public async Task<bool> VerifyChainAsync(Guid engagementId, Guid tenantId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand(@"
            SELECT id, tenant_id, engagement_id, execution_id, evidence_type, sha256, record_hash, previous_hash, collected_at, sequence
            FROM evidence_records WHERE engagement_id = $eng AND tenant_id = $ten ORDER BY sequence", conn);
        cmd.Parameters.AddWithValue("$eng", engagementId);
        cmd.Parameters.AddWithValue("$ten", tenantId);
        using var reader = await cmd.ExecuteReaderAsync();

        var records = new List<(Guid id, Guid tenant, Guid eng, Guid exec, string type, string sha, string rhash, string prev, DateTime collected, long seq)>();
        while (await reader.ReadAsync())
        {
            records.Add((
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.IsDBNull(3) ? Guid.Empty : reader.GetGuid(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetDateTime(8),
                reader.GetInt64(9)
            ));
        }

        if (records.Count == 0) return true;

        string? expectedPrevious = null;
        foreach (var r in records)
        {
            var recomputed = Sha256Hex(CanonicalBytes(
                r.id, r.tenant, r.eng, r.exec,
                r.type, r.sha, r.rhash, r.prev,
                r.collected, r.prev, r.seq));

            if (!string.Equals(recomputed, r.rhash, StringComparison.Ordinal)) return false;
            if (!string.Equals(r.prev, expectedPrevious, StringComparison.Ordinal)) return false;
            expectedPrevious = r.rhash;
        }
        return true;
    }

    public async Task<EvidenceManifest> ExportManifestAsync(Guid engagementId, Guid tenantId)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new NpgsqlCommand(@"
            SELECT id, sequence, evidence_type, sha256, record_hash, previous_hash, collected_at
            FROM evidence_records WHERE engagement_id = $eng AND tenant_id = $ten ORDER BY sequence", conn);
        cmd.Parameters.AddWithValue("$eng", engagementId);
        cmd.Parameters.AddWithValue("$ten", tenantId);
        using var reader = await cmd.ExecuteReaderAsync();

        var manifestRecords = new List<EvidenceManifestRecord>();
        while (await reader.ReadAsync())
        {
            manifestRecords.Add(new EvidenceManifestRecord(
                reader.GetGuid(0),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetDateTime(6)));
        }

        var head = manifestRecords.Count > 0 ? manifestRecords[^1].RecordHash : string.Empty;
        return new EvidenceManifest(engagementId, tenantId, DateTimeOffset.UtcNow, head, manifestRecords);
    }

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

    private static string Sha256Hex(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string Sha256Hex(string content)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static byte[] CanonicalBytes(Guid id, Guid tenant, Guid engagement, Guid execution,
        string type, string contentHash, string classification, string provenance,
        DateTime collectedAt, string? previousHash, long sequence)
    {
        var canonical = string.Join("\n",
            id.ToString("D"), tenant.ToString("D"), engagement.ToString("D"), execution.ToString("D"),
            type, contentHash, classification, provenance,
            collectedAt.ToUniversalTime().ToString("O"), previousHash ?? "-", sequence.ToString());
        return Encoding.UTF8.GetBytes(canonical);
    }
}