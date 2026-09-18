namespace Aiko.Core;

/// Finds the real Claude Code program further along PATH, never another copy of the shim.
///
/// The spike found the trap: with two shim folders in PATH, say an old install and a new one,
/// each shim took the other for Claude Code and they started each other until there were
/// thousands of processes. So every shim adds its own folder to a variable that its children
/// inherit, and no shim ever runs Claude Code from a folder already in that list.
public static class RealClaude
{
    public const string SeenVariable = "AIKO_SHIM_SEEN";

    /// More shims than this in one chain is a loop that got past the list somehow. Stop.
    public const int MaxChain = 8;

    /// Claude Code's program file: claude.exe on Windows.
    public static string ExecutableName(PlatformConventions platform) => platform.ExecutableName(ShimLaunch.ClaudeName);

    public static IReadOnlyList<string> ParseSeen(PlatformConventions platform, string? value) =>
        (value ?? "")
            .Split(platform.PathListSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    public static string FormatSeen(PlatformConventions platform, IEnumerable<string> folders) =>
        string.Join(platform.PathListSeparator, folders);

    public static string? Find(
        PlatformConventions platform,
        string? pathVariable,
        IReadOnlyCollection<string> seenFolders,
        Func<string, bool> fileExists)
    {
        if (seenFolders.Count > MaxChain)
        {
            return null;
        }

        foreach (var entry in (pathVariable ?? "").Split(platform.PathListSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var folder = entry.Trim().Trim('"');
            if (folder.Length == 0 || seenFolders.Any(seen => SameFolder(seen, folder)))
            {
                continue;
            }

            var candidate = platform.Join(folder, ExecutableName(platform));
            if (fileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// Both separators are trimmed, whatever system the path was written for: a folder list can
    /// hold either, and the answer must not depend on the machine reading it.
    public static bool SameFolder(string a, string b) =>
        string.Equals(TrimEnd(a), TrimEnd(b), StringComparison.OrdinalIgnoreCase);

    private static string TrimEnd(string path) => path.Trim().TrimEnd('\\', '/');
}
