namespace Aiko.Core;

/// Puts Aiko's command folder at the front of the user PATH and takes it out again.
///
/// Only the one entry is ever touched. Everything else in the value keeps its order, its case and
/// its spelling, including variables such as %USERPROFILE% the person wrote there.
public static class UserPathList
{
    public static string AddToFront(PlatformConventions platform, string? value, string folder)
    {
        var entries = Without(platform, value, folder);
        entries.Insert(0, folder);
        return string.Join(platform.PathListSeparator, entries);
    }

    public static string Remove(PlatformConventions platform, string? value, string folder) =>
        string.Join(platform.PathListSeparator, Without(platform, value, folder));

    public static bool Contains(PlatformConventions platform, string? value, string folder) =>
        Split(platform, value).Any(entry => RealClaude.SameFolder(entry, folder));

    private static List<string> Without(PlatformConventions platform, string? value, string folder) =>
        Split(platform, value).Where(entry => !RealClaude.SameFolder(entry, folder)).ToList();

    private static IEnumerable<string> Split(PlatformConventions platform, string? value) =>
        (value ?? "").Split(platform.PathListSeparator).Where(entry => entry.Trim().Length > 0);
}
