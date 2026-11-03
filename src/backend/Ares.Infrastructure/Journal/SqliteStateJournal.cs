using System.Data;
using Microsoft.Data.Sqlite;

namespace Ares.Infrastructure.Journal;

/// <summary>
/// Durable <see cref="IStateJournal"/> backed by a single SQLite database file.
///
/// Durability choices: WAL journal mode with <c>synchronous=FULL</c> — a
/// committed append survives process crash and power loss. Sequence allocation
/// and hash chaining happen inside a single IMMEDIATE transaction, so two
/// writers can never fork the chain.
/// </summary>
public sealed class SqliteStateJournal : IStateJournal, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly object _lock = new();
    private bool _disposed;

    public SqliteStateJournal(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        };
        _connection = new SqliteConnection(builder.ToString());
        _connection.Open();

        Execute("PRAGMA journal_mode=WAL;");
        Execute("PRAGMA synchronous=FULL;");
        Execute("PRAGMA foreign_keys=ON;");
        Execute("""
            CREATE TABLE IF NOT EXISTS state_journal (
                sequence    INTEGER PRIMARY KEY,
                stream      TEXT    NOT NULL,
                event_type  TEXT    NOT NULL,
                payload     TEXT    NOT NULL,
                actor       TEXT    NOT NULL,
                occurred_at TEXT    NOT NULL,
                prev_hash   TEXT    NOT NULL,
                hash        TEXT    NOT NULL
            );
            """);
        Execute("CREATE INDEX IF NOT EXISTS ix_state_journal_stream ON state_journal(stream, sequence);");
    }

    public long Count
    {
        get
        {
            lock (_lock)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(1) FROM state_journal;";
                return Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }

    public JournalEntry Append(string stream, string eventType, string payload, string? actor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(payload);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            using var tx = _connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);

            long next;
            string prev;
            using (var head = _connection.CreateCommand())
            {
                head.Transaction = tx;
                head.CommandText = "SELECT sequence, hash FROM state_journal ORDER BY sequence DESC LIMIT 1;";
                using var reader = head.ExecuteReader();
                if (reader.Read())
                {
                    next = reader.GetInt64(0) + 1;
                    prev = reader.GetString(1);
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

            using (var insert = _connection.CreateCommand())
            {
                insert.Transaction = tx;
                insert.CommandText = """
                    INSERT INTO state_journal (sequence, stream, event_type, payload, actor, occurred_at, prev_hash, hash)
                    VALUES ($seq, $stream, $type, $payload, $actor, $at, $prev, $hash);
                    """;
                insert.Parameters.AddWithValue("$seq", next);
                insert.Parameters.AddWithValue("$stream", stream);
                insert.Parameters.AddWithValue("$type", eventType);
                insert.Parameters.AddWithValue("$payload", payload);
                insert.Parameters.AddWithValue("$actor", actorName);
                insert.Parameters.AddWithValue("$at", at.ToUniversalTime().ToString("O"));
                insert.Parameters.AddWithValue("$prev", prev);
                insert.Parameters.AddWithValue("$hash", hash);
                insert.ExecuteNonQuery();
            }

            tx.Commit();
            return new JournalEntry(next, stream, eventType, payload, actorName, at, prev, hash);
        }
    }

    public IReadOnlyList<JournalEntry> Read(string streamPrefix = "", long afterSequence = 0, int limit = 1000)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (limit <= 0) return Array.Empty<JournalEntry>();

        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = string.IsNullOrEmpty(streamPrefix)
                ? "SELECT sequence, stream, event_type, payload, actor, occurred_at, prev_hash, hash FROM state_journal WHERE sequence > $after ORDER BY sequence LIMIT $limit;"
                : "SELECT sequence, stream, event_type, payload, actor, occurred_at, prev_hash, hash FROM state_journal WHERE sequence > $after AND substr(stream, 1, length($prefix)) = $prefix ORDER BY sequence LIMIT $limit;";
            cmd.Parameters.AddWithValue("$after", afterSequence);
            cmd.Parameters.AddWithValue("$limit", limit);
            if (!string.IsNullOrEmpty(streamPrefix))
                cmd.Parameters.AddWithValue("$prefix", streamPrefix);

            using var reader = cmd.ExecuteReader();
            var results = new List<JournalEntry>();
            while (reader.Read())
            {
                results.Add(new JournalEntry(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind),
                    reader.GetString(6),
                    reader.GetString(7)));
            }
            return results;
        }
    }

    public IReadOnlyList<string> ListStreams(string prefix = "")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lock)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT stream FROM state_journal WHERE substr(stream, 1, length($prefix)) = $prefix ORDER BY stream;";
            cmd.Parameters.AddWithValue("$prefix", prefix);
            using var reader = cmd.ExecuteReader();
            var streams = new List<string>();
            while (reader.Read()) streams.Add(reader.GetString(0));
            return streams;
        }
    }

    public ChainVerification VerifyChain()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var entries = new List<JournalEntry>();
        long after = 0;
        while (true)
        {
            var page = Read("", after, 5000);
            if (page.Count == 0) break;
            entries.AddRange(page);
            after = page[^1].Sequence;
        }
        return JournalChain.Verify(entries);
    }

    private void Execute(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _connection.Dispose();
        }
    }
}
