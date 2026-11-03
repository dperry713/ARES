namespace Ares.Infrastructure.Journal;

public static class JournalChain
{
    /// <summary>
    /// Recompute every hash and re-link every entry. Detects payload edits,
    /// deletions, and reordering — the three tamper shapes that matter for an
    /// audit trail.
    /// </summary>
    public static ChainVerification Verify(IReadOnlyList<JournalEntry> entries)
    {
        var prev = JournalHashing.GenesisHash;
        long expectedSequence = 1;
        foreach (var entry in entries)
        {
            if (entry.Sequence != expectedSequence)
                return new ChainVerification(false, expectedSequence - 1, entry.Sequence,
                    $"sequence gap: expected {expectedSequence}, found {entry.Sequence}");
            if (!string.Equals(entry.PrevHash, prev, StringComparison.Ordinal))
                return new ChainVerification(false, expectedSequence - 1, entry.Sequence,
                    "prev-hash link broken (entry inserted, removed, or reordered)");
            var recomputed = JournalHashing.Compute(entry.Sequence, entry.Stream, entry.EventType,
                entry.Payload, entry.Actor, entry.OccurredAt, entry.PrevHash);
            if (!string.Equals(recomputed, entry.Hash, StringComparison.Ordinal))
                return new ChainVerification(false, expectedSequence - 1, entry.Sequence,
                    "entry hash mismatch (payload modified after append)");
            prev = entry.Hash;
            expectedSequence++;
        }
        return new ChainVerification(true, entries.Count, null, "chain intact");
    }
}
