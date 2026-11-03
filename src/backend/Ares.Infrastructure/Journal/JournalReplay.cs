using Ares.Contracts;

namespace Ares.Infrastructure.Journal;

/// <summary>
/// Rebuilds an aggregate projection from the journal. Every service projects
/// the latest journaled state per stream, so a process restart resumes with the
/// full authorization/approval/execution history intact.
/// </summary>
public static class JournalReplay
{
    public static Dictionary<Guid, T> LatestById<T>(IStateJournal? journal, string streamPrefix)
        where T : class, IEntity
    {
        var map = new Dictionary<Guid, T>();
        if (journal is null) return map;

        long after = 0;
        while (true)
        {
            var page = journal.Read(streamPrefix, after, 500);
            if (page.Count == 0) break;
            foreach (var entry in page)
            {
                var entity = JournalJson.Deserialize<T>(entry.Payload);
                map[entity.Id] = entity;
            }
            after = page[^1].Sequence;
        }
        return map;
    }
}
