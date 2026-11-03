// Durable, tamper-evident state journal tests.
//
// These prove the guarantees the API depends on at boot:
//  (a) a committed append survives process exit (SQLite, WAL + synchronous=FULL),
//  (b) the in-memory projection is rebuilt from the journal on restart,
//  (c) any mutation/deletion/reordering of journal rows is detected by VerifyChain,
//  (d) every entry commits to its predecessor (hash chain, not per-row hashes).
using Ares.Engagements;
using Ares.Infrastructure.Journal;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Ares.UnitTests;

public sealed class JournalDurabilityTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "ares-journal-tests", Guid.NewGuid().ToString("N"));

    private string JournalPath => Path.Combine(_directory, "ares.journal.db");

    public JournalDurabilityTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A lingering SQLite handle must not fail the suite.
        }
    }

    [Fact]
    public void Appends_survive_process_restart_and_chain_stays_intact()
    {
        long countAfterWrites;
        using (var writer = new SqliteStateJournal(JournalPath))
        {
            writer.Append("engagement/1", "state", "{\"v\":1}");
            writer.Append("engagement/1", "state", "{\"v\":2}");
            writer.Append("execution/2", "state", "{\"v\":3}");
            countAfterWrites = writer.Count;

            var verification = writer.VerifyChain();
            Assert.True(verification.IsIntact, verification.Detail);
            Assert.Equal(3, verification.EntriesVerified);
        }

        using var reader = new SqliteStateJournal(JournalPath);
        Assert.Equal(countAfterWrites, reader.Count);
        Assert.Equal(3, reader.Read(limit: 100).Count);
        Assert.Equal(2, reader.Read("engagement/", limit: 100).Count);
        Assert.True(reader.VerifyChain().IsIntact);
    }

    [Fact]
    public void Each_entry_commits_to_its_predecessor()
    {
        using var journal = new SqliteStateJournal(JournalPath);
        var first = journal.Append("stream/a", "state", "{}");
        var second = journal.Append("stream/a", "state", "{}");

        Assert.Equal(JournalHashing.GenesisHash, first.PrevHash);
        Assert.Equal(first.Hash, second.PrevHash);
        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void Mutating_a_committed_payload_is_detected()
    {
        using (var writer = new SqliteStateJournal(JournalPath))
        {
            writer.Append("engagement/1", "state", "{\"name\":\"original\"}");
            writer.Append("engagement/1", "state", "{\"name\":\"original-2\"}");
        }

        // Simulate an operator (or attacker) editing the database directly.
        using (var connection = new SqliteConnection($"Data Source={JournalPath}"))
        {
            connection.Open();
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE state_journal SET payload = '{\"name\":\"tampered\"}' WHERE sequence = 1;";
            Assert.Equal(1, update.ExecuteNonQuery());
        }

        using var verifier = new SqliteStateJournal(JournalPath);
        var verification = verifier.VerifyChain();
        Assert.False(verification.IsIntact);
        Assert.Equal(1, verification.FirstInvalidSequence);
    }

    [Fact]
    public void Deleting_a_committed_entry_is_detected()
    {
        using (var writer = new SqliteStateJournal(JournalPath))
        {
            writer.Append("a", "state", "{}");
            writer.Append("b", "state", "{}");
            writer.Append("c", "state", "{}");
        }

        using (var connection = new SqliteConnection($"Data Source={JournalPath}"))
        {
            connection.Open();
            using var delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM state_journal WHERE sequence = 2;";
            Assert.Equal(1, delete.ExecuteNonQuery());
        }

        using var verifier = new SqliteStateJournal(JournalPath);
        var verification = verifier.VerifyChain();
        Assert.False(verification.IsIntact);
    }

    [Fact]
    public async Task Aggregate_state_is_replayed_from_the_journal_after_restart()
    {
        var tenant = Guid.NewGuid();
        Guid engagementId;

        using (var journal = new SqliteStateJournal(JournalPath))
        {
            var engagements = new EngagementService(journal);
            var engagement = await engagements.CreateAsync(tenant, "durable", "d", "AR-DUR-1",
                DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), "staging.example.test",
                ["safe_http_configuration_checks"], []);
            engagementId = engagement.Id;
            await engagements.AuthorizeAsync(engagementId);
            await engagements.StartAsync(engagementId);
        }

        // New process: only the journal file survives.
        using (var journal = new SqliteStateJournal(JournalPath))
        {
            var engagements = new EngagementService(journal);
            var replayed = await engagements.GetByIdAsync(engagementId);

            Assert.Equal("Active", replayed.Status);
            Assert.Equal(tenant, replayed.TenantId);
            Assert.Equal("AR-DUR-1", replayed.AuthorizationReference);
            Assert.True(journal.VerifyChain().IsIntact);
        }
    }

    [Fact]
    public void In_memory_journal_uses_the_same_chain_semantics()
    {
        var journal = new InMemoryStateJournal();
        journal.Append("x", "state", "{\"v\":1}");
        journal.Append("x", "state", "{\"v\":2}");
        journal.Append("y", "state", "{\"v\":3}");

        Assert.Equal(3, journal.Count);
        Assert.True(journal.VerifyChain().IsIntact);
        Assert.Equal(new[] { "x", "y" }, journal.ListStreams());
        Assert.Equal(2, journal.Read("x", limit: 100).Count);
        Assert.Empty(journal.Read("missing"));
    }

    [Fact]
    public void Chain_verification_rejects_a_broken_link_even_with_valid_row_hashes()
    {
        // Hand-build a chain whose second entry claims a foreign predecessor hash.
        var payload = "{}";
        var at = DateTimeOffset.UtcNow;
        var first = new JournalEntry(1, "s", "state", payload, "tester", at,
            JournalHashing.GenesisHash,
            JournalHashing.Compute(1, "s", "state", payload, "tester", at, JournalHashing.GenesisHash));
        var second = new JournalEntry(2, "s", "state", payload, "tester", at,
            "deadbeef",
            JournalHashing.Compute(2, "s", "state", payload, "tester", at, "deadbeef"));

        var verification = JournalChain.Verify([first, second]);
        Assert.False(verification.IsIntact);
    }
}
