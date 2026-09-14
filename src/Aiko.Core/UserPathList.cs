namespace Aiko.Core;

/// Puts Aiko's command folder at the front of the user PATH and takes it out again.
///
/// Only the one entry is ever touched. Everything else in the value keeps its order, its case and
/// its spelling, including %VARIABLES% the person wrote there.
public static class UserPathList
{
    public static string AddToFront(string? value, string folder)
    {
        var entries = Without(value, folder);
        entries.Insert(0, folder);
        return string.Join(';', entries);
    }

    public static string Remove(string? value, string folder) => string.Join(';', Without(value, folder));

    public static bool Contains(string? value, string folder) =>
        Split(value).Any(entry => RealClaude.SameFolder(entry, folder));

    private static List<string> Without(string? value, string folder) =>
        Split(value).Where(entry => !RealClaude.SameFolder(entry, folder)).ToList();

    private static IEnumerable<string> Split(string? value) =>
        (value ?? "").Split(';').Where(entry => entry.Trim().Length > 0);
}
