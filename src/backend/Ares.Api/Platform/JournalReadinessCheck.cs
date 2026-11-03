using Ares.Infrastructure.Journal;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ares.Api.Platform;

/// <summary>
/// Readiness: the journal must be writable and its hash chain intact. Chain
/// verification is cached briefly so a readiness probe in a tight loop cannot
/// become a denial-of-service vector on a large journal.
/// </summary>
public sealed class JournalReadinessCheck(IStateJournal journal) : IHealthCheck
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ChainVerification? _cached;
    private DateTimeOffset _cachedAt;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cached is null || DateTimeOffset.UtcNow - _cachedAt > CacheTtl)
            {
                _cached = journal.VerifyChain();
                _cachedAt = DateTimeOffset.UtcNow;
            }

            var data = new Dictionary<string, object>
            {
                ["entries"] = journal.Count,
                ["chainIntact"] = _cached.IsIntact,
                ["verified"] = _cached.EntriesVerified
            };

            return _cached.IsIntact
                ? HealthCheckResult.Healthy($"Journal chain intact across {_cached.EntriesVerified} entries.", data)
                : HealthCheckResult.Unhealthy($"Journal chain broken at sequence {_cached.FirstInvalidSequence}: {_cached.Detail}", data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Journal is not readable.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }
}
