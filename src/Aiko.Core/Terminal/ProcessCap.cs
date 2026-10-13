namespace Aiko.Core.Terminal;

/// A conversation with a running Claude Code. Busy covers a turn, a background task and a request
/// that waits for the person.
public sealed record RunningTalk(string Id, bool Busy, long LastUsed);

/// No more than a few Claude Code processes at once (D-304). Each one holds memory, and a session
/// that only sits in the list does not need one: its conversation comes back from its file.
public static class ProcessCap
{
    public const int Most = 4;

    /// The session to stop before one more process starts, or null. It is the one that does
    /// nothing and was used longest ago. When every process is busy none is stopped, and the new
    /// one starts over the cap: the work of the person is not ended for a number.
    public static string? ToStop(IReadOnlyList<RunningTalk> running, string starting)
    {
        var others = running.Where(talk => talk.Id != starting).ToList();
        if (others.Count < Most)
        {
            return null;
        }

        return others
            .Where(talk => !talk.Busy)
            .OrderBy(talk => talk.LastUsed)
            .Select(talk => talk.Id)
            .FirstOrDefault();
    }
}
