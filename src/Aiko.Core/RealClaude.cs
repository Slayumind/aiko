namespace Aiko.Core;

/// Finds the real claude.exe further along PATH, never another copy of the shim.
///
/// The spike found the trap: with two shim folders in PATH, say an old install and a new one,
/// each shim took the other for Claude Code and they started each other until there were
/// thousands of processes. So every shim adds its own folder to a variable that its children
/// inherit, and no shim ever runs claude.exe from a folder already in that list.
public static class RealClaude
{
    public const string SeenVariable = "AIKO_SHIM_SEEN";
    public const string ExecutableName = "claude.exe";

    /// More shims than this in one chain is a loop that got past the list somehow. Stop.
    public const int MaxChain = 8;

    public static IReadOnlyList<string> ParseSeen(string? value) =>
        (value ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    public static string FormatSeen(IEnumerable<string> folders) => string.Join(';', folders);

    public static string? Find(
        string? pathVariable,
        IReadOnlyCollection<string> seenFolders,
        Func<string, bool> fileExists)
    {
        if (seenFolders.Count > MaxChain)
        {
            return null;
        }

        foreach (var entry in (pathVariable ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var folder = entry.Trim().Trim('"');
            if (folder.Length == 0 || seenFolders.Any(seen => SameFolder(seen, folder)))
            {
                continue;
            }

            var candidate = Path.Combine(folder, ExecutableName);
            if (fileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static bool SameFolder(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(a.Trim()),
            Path.TrimEndingDirectorySeparator(b.Trim()),
            StringComparison.OrdinalIgnoreCase);
}
