/// <summary>
/// Tamper-evident, tenant-isolated evidence store contract.
/// Blueprint §6.4, MEC §13.
/// Implementations: EvidenceService (in-memory, milestone-bounded),
/// PostgresEvidenceService (PostgreSQL, durable, multi-instance safe).
/// </summary>
namespace Ares.Evidence.Services;

using Ares.Domain;

/// <summary>Tamper-evident, tenant-isolated evidence record.</summary>
public sealed record EvidenceRecord(
    Guid Id, Guid TenantId, Guid EngagementId, Guid ExecutionId,
    string EvidenceType, string SHA256, string RecordHash,
    string? PreviousHash, DateTime CollectedAt, long Sequence);

/// <summary>Tamper-evident, tenant-isolated evidence store contract.</summary>
public interface IEvidenceService
{
    /// <summary>Ingest a new evidence record with SHA-256 hash chaining and tenant isolation.</summary>
    Task<Ares.Domain.Evidence> IngestAsync(Guid tenantId, Guid engagementId, Guid executionId,
        string evidenceType, string content, string classification, string provenance);

    /// <summary>Retrieve an evidence record by ID, enforcing tenant isolation (fail-closed).</summary>
    Task<Ares.Domain.Evidence> GetAsync(Guid evidenceId, Guid tenantId);

    /// <summary>Recompute and verify the SHA-256 record hash for a single evidence record.</summary>
    Task<bool> VerifyAsync(Guid evidenceId, Guid tenantId);

    /// <summary>Verify the full hash chain for an engagement, rejecting cross-tenant access.</summary>
    Task<bool> VerifyChainAsync(Guid engagementId, Guid tenantId);

    /// <summary>Export evidence records for an engagement, tenant-scoped.</summary>
    Task<EvidenceManifest> ExportManifestAsync(Guid engagementId, Guid tenantId);
}