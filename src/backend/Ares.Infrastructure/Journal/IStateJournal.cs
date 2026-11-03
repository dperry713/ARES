using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ares.Infrastructure.Journal;

/// <summary>
/// One immutable record in the append-only state journal.
/// <see cref="Hash"/> is SHA-256 over the canonical record bytes including
/// <see cref="PrevHash"/>, so any mutation, deletion, or reordering is
/// detectable (see <see cref="IStateJournal.VerifyChain"/>).
/// </summary>
public sealed record JournalEntry(
    long Sequence,
    string Stream,
    string EventType,
    string Payload,
    string Actor,
    DateTimeOffset OccurredAt,
    string PrevHash,
    string Hash);

public sealed record ChainVerification(
    bool IsIntact,
    long EntriesVerified,
    long? FirstInvalidSequence,
    string Detail);

/// <summary>
/// Append-only, tamper-evident journal of aggregate state transitions.
/// The platform projection (in-memory dictionaries) is rebuilt from this
/// journal at startup, so a restart never loses authorizations, approvals,
/// executions, or their history.
/// </summary>
public interface IStateJournal
{
    JournalEntry Append(string stream, string eventType, string payload, string? actor = null);

    IReadOnlyList<JournalEntry> Read(string streamPrefix = "", long afterSequence = 0, int limit = 1000);

    IReadOnlyList<string> ListStreams(string prefix = "");

    ChainVerification VerifyChain();

    long Count { get; }
}

/// <summary>Shared canonicalization so SQLite and in-memory journals hash identically.</summary>
public static class JournalHashing
{
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

    public static string Compute(long sequence, string stream, string eventType, string payload,
        string actor, DateTimeOffset occurredAt, string prevHash)
    {
        var canonical = string.Join('\n',
            sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            stream,
            eventType,
            payload,
            actor,
            occurredAt.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            prevHash);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

/// <summary>JSON contract shared by every journaled aggregate.</summary>
public static class JournalJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string payload) =>
        JsonSerializer.Deserialize<T>(payload, Options)
        ?? throw new InvalidOperationException($"Journal payload could not be deserialized to {typeof(T).Name}.");
}

/// <summary>No-op journal for unit tests and ephemeral runs. Never used by the API.</summary>
public sealed class NullStateJournal : IStateJournal
{
    public static readonly NullStateJournal Instance = new();
    private long _count;

    public long Count => Interlocked.Read(ref _count);

    public JournalEntry Append(string stream, string eventType, string payload, string? actor = null)
    {
        var seq = Interlocked.Increment(ref _count);
        var at = DateTimeOffset.UtcNow;
        var hash = JournalHashing.Compute(seq, stream, eventType, payload, actor ?? "system", at, JournalHashing.GenesisHash);
        return new JournalEntry(seq, stream, eventType, payload, actor ?? "system", at, JournalHashing.GenesisHash, hash);
    }

    public IReadOnlyList<JournalEntry> Read(string streamPrefix = "", long afterSequence = 0, int limit = 1000) =>
        Array.Empty<JournalEntry>();

    public IReadOnlyList<string> ListStreams(string prefix = "") => Array.Empty<string>();

    public ChainVerification VerifyChain() => new(true, 0, null, "no-op journal");
}

/// <summary>In-memory journal with the exact hashing/verification semantics of the SQLite journal.</summary>
public sealed class InMemoryStateJournal : IStateJournal
{
    private readonly List<JournalEntry> _entries = new();
    private readonly object _lock = new();

    public long Count { get { lock (_lock) return _entries.Count; } }

    public JournalEntry Append(string stream, string eventType, string payload, string? actor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        lock (_lock)
        {
            var seq = _entries.Count + 1L;
            var at = DateTimeOffset.UtcNow;
            var prev = _entries.Count == 0 ? JournalHashing.GenesisHash : _entries[^1].Hash;
            var hash = JournalHashing.Compute(seq, stream, eventType, payload, actor ?? "system", at, prev);
            var entry = new JournalEntry(seq, stream, eventType, payload, actor ?? "system", at, prev, hash);
            _entries.Add(entry);
            return entry;
        }
    }

    public IReadOnlyList<JournalEntry> Read(string streamPrefix = "", long afterSequence = 0, int limit = 1000)
    {
        lock (_lock)
        {
            return _entries
                .Where(e => e.Sequence > afterSequence)
                .Where(e => string.IsNullOrEmpty(streamPrefix) || e.Stream.StartsWith(streamPrefix, StringComparison.Ordinal))
                .Take(limit)
                .ToList();
        }
    }

    public IReadOnlyList<string> ListStreams(string prefix = "")
    {
        lock (_lock)
        {
            return _entries
                .Select(e => e.Stream)
                .Where(s => s.StartsWith(prefix, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }

    public ChainVerification VerifyChain()
    {
        lock (_lock) return JournalChain.Verify(_entries);
    }
}
