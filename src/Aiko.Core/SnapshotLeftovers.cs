namespace Aiko.Core;

/// The temporary files a bridge left in the folder of snapshots (AIKO-267).
///
/// A bridge writes "claude.json.1234.tmp" and moves it over "claude.json". A bridge that was
/// stopped in the middle, or a move Windows refused, left such a file behind, and nothing ever
/// removed it. Aiko removes them when it starts; a file younger than a minute may still belong to
/// a bridge that is writing right now, so it stays.
public static class SnapshotLeftovers
{
    public static readonly TimeSpan KeepFor = TimeSpan.FromMinutes(1);

    /// The names of the files to remove: "<name>.json.<process id>.tmp", older than KeepFor.
    public static IReadOnlyList<string> Stale(IEnumerable<(string Name, DateTimeOffset Written)> files, DateTimeOffset now) =>
        files
            .Where(file => IsLeftover(file.Name) && now - file.Written > KeepFor)
            .Select(file => file.Name)
            .ToList();

    private static bool IsLeftover(string name)
    {
        const string Tail = ".tmp";
        if (!name.EndsWith(Tail, StringComparison.Ordinal))
        {
            return false;
        }

        var stem = name[..^Tail.Length];
        var dot = stem.LastIndexOf('.');
        return dot > 0
            && dot < stem.Length - 1
            && stem[(dot + 1)..].All(char.IsAsciiDigit)
            && stem[..dot].EndsWith(".json", StringComparison.Ordinal)
            && stem[..dot].Length > ".json".Length;
    }
}
