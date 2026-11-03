using System.Globalization;
using System.Text;
using Npgsql;

namespace Ares.Infrastructure.Journal;

/// <summary>
/// Durable <see cref="IStateJournal"/> backed by PostgreSQL via raw Npgsql.
/// Provides append-only hash-chained journal with advisory locks for multi-instance safety.
/// </summary>
public sealed class PostgresStateJournal : IStateJournal, IAsyncDisposable
{
    private readonly string _connectionString;
    private bool _disposed;

    public PostgresStateJournal(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;

        // Ensure schema exists on first use (best-effort; admin should run migrations in production)
        using var conn = new NpgsqlConnection(_connectionString);
        conn.OpenAsync().GetAwaiter().GetResult();
        using var cmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS state_journal (
                sequence BIGINT PRIMARY KEY,
                stream TEXT NOT NULL,
                event_type TEXT NOT NULL,
                payload TEXT NOT NULL,
                actor TEXT NOT NULL,
                occurred_at TIMESTAMPTZ NOT NULL,
                prev_hash TEXT NOT NULL,
                hash TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_state_journal_stream ON state_journal (stream);
            CREATE INDEX IF NOT EXISTS idx_state_journal_seq ON state_journal (sequence);", conn);
        cmd.ExecuteNonQueryAsync().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        Dispose(false);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
    }

    #region IStateJournal Members

    JournalEntry IStateJournal.Append(string stream, string eventType, string payload, string? actor = null)
    {
        return AppendAsync(stream, eventType, payload, actor).GetAwaiter().GetResult();
    }

    IReadOnlyList<JournalEntry> IStateJournal.Read(string streamPrefix = "", long afterSequence = 0, int limit = 1000)
    {
        return ReadAsync(streamPrefix, afterSequence, limit).GetAwaiter().GetResult();
    }

    IReadOnlyList<string> IStateJournal.ListStreams(string prefix = "")
    {
        return ListStreamsAsync(prefix).GetAwaiter().GetResult();
    }

    ChainVerification IStateJournal.VerifyChain()
    {
        return VerifyChainAsync().GetAwaiter().GetResult();
    }

    long IStateJournal.Count
    {
        get
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM state_journal", conn);
            return (long)cmd.ExecuteScalar()!;
        }
    }

    #endregion

    #region IStateJournal Async Members

    public async ValueTask<JournalEntry> AppendAsync(string stream, string eventType, string payload, string? actor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(payload);

        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        long next;
        string prev;

        using (var lockCmd = conn.CreateCommand())
        {
            lockCmd.CommandText = "SELECT pg_advisory_xact_lock(12345);";
            await lockCmd.ExecuteNonQueryAsync();
        }

        using (var headCmd = conn.CreateCommand())
        {
            headCmd.CommandText = "SELECT sequence, hash FROM state_journal ORDER BY sequence DESC LIMIT 1 FOR UPDATE;";
            using var headReader = await headCmd.ExecuteReaderAsync();
            if (await headReader.ReadAsync())
            {
                next = headReader.GetInt64(0) + 1;
                prev = headReader.GetString(1);
            }
            else
            {
                next = 1;
                prev = JournalHashing.GenesisHash;
            }
        }

        var at = DateTimeOffset.UtcNow;
        var actorName = string.IsNullOrWhiteSpace(actor) ? "system" : actor;
        var hash = JournalHashing.Compute(next, stream, eventType, payload, actorName, at, prev);

        using (var insertCmd = conn.CreateCommand())
        {
            insertCmd.CommandText = @"
                INSERT INTO state_journal (sequence, stream, event_type, payload, actor, occurred_at, prev_hash, hash)
                VALUES ($seq, $stream, $type, $payload, $actor, $at, $prev, $hash)";
            insertCmd.Parameters.AddWithValue("$seq", next);
            insertCmd.Parameters.AddWithValue("$stream", stream);
            insertCmd.Parameters.AddWithValue("$type", eventType);
            insertCmd.Parameters.AddWithValue("$payload", payload);
            insertCmd.Parameters.AddWithValue("$actor", actorName);
            insertCmd.Parameters.AddWithValue("$at", at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            insertCmd.Parameters.AddWithValue("$prev", prev);
            insertCmd.Parameters.AddWithValue("$hash", hash);
            await insertCmd.ExecuteNonQueryAsync();
        }

        return new JournalEntry(next, stream, eventType, payload, actorName, at, prev, hash);
    }

    public async ValueTask<IReadOnlyList<JournalEntry>> ReadAsync(string streamPrefix = "", long afterSequence = 0, int limit = 1000)
    {
        if (limit <= 0) return Array.Empty<JournalEntry>();

        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var query = @"SELECT sequence, stream, event_type, payload, actor, occurred_at, prev_hash, hash FROM state_journal WHERE sequence > $after ORDER BY sequence LIMIT $limit";
        if (!string.IsNullOrEmpty(streamPrefix))
            query += @" AND substr(stream, 1, length($prefix)) = $prefix";

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("$after", afterSequence);
        cmd.Parameters.AddWithValue("$limit", limit);
        if (!string.IsNullOrEmpty(streamPrefix))
            cmd.Parameters.AddWithValue("$prefix", streamPrefix);

        using var reader = await cmd.ExecuteReaderAsync();
        var results = new List<JournalEntry>();
        while (await reader.ReadAsync())
        {
            results.Add(new JournalEntry(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.GetString(6),
                reader.GetString(7)));
        }
        return results;
    }

    public async ValueTask<IReadOnlyList<string>> ListStreamsAsync(string prefix = "")
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var query = @"SELECT DISTINCT stream FROM state_journal";
        if (!string.IsNullOrEmpty(prefix))
            query += @" WHERE substr(stream, 1, length($prefix)) = $prefix";

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("$prefix", prefix);

        using var reader = await cmd.ExecuteReaderAsync();
        var streams = new List<string>();
        while (await reader.ReadAsync())
            streams.Add(reader.GetString(0));
        return streams;
    }

    public async ValueTask<ChainVerification> VerifyChainAsync()
    {
        var entries = new List<JournalEntry>();
        long after = 0;

        while (true)
        {
            var page = await ReadAsync("", after, 5000);
            if (page.Count == 0) break;
            entries.AddRange(page);
            after = entries[^1].Sequence;
        }

        return JournalChain.Verify(entries);
    }

    #endregion
}