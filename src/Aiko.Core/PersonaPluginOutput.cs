namespace Aiko.Core;

/// Where the bridge puts the persona plugin it builds, and which old copies it clears (D-201).
///
/// Claude Code runs "Aiko.Bridge.exe plugin aiko-persona", copies the folder the bridge prints into
/// its own cache and never reads the folder again. Each content gets its own folder named after
/// its hash, so a changed persona never half-overwrites a folder Claude Code may be copying.
public static class PersonaPluginOutput
{
    public const string Verb = "plugin";

    /// How long an old folder stays. Another session of the same environment may be copying it
    /// right now; an hour is far longer than any copy takes.
    public static readonly TimeSpan KeepOldFor = TimeSpan.FromHours(1);

    /// One folder per environment, named like its limit snapshot, so the two never disagree.
    public static string EnvironmentFolder(AikoFolders folders, string configDirectory) =>
        Path.Combine(folders.PersonaPluginsFolder, SnapshotName.For(configDirectory));

    public static string VersionFolder(AikoFolders folders, string configDirectory, string contentHash) =>
        Path.Combine(EnvironmentFolder(folders, configDirectory), contentHash);

    /// Whether the command asks for a plugin this bridge can build.
    public static bool IsRequest(IReadOnlyList<string> args) =>
        args is [Verb, PersonaPlugin.Name];

    /// Old folders to delete: every other version older than KeepOldFor. Folders that are not a
    /// version at all, such as a temporary folder of a build still running, are left alone.
    public static IReadOnlyList<string> StaleFolders(
        IEnumerable<(string Name, DateTimeOffset Written)> folders, string currentHash, DateTimeOffset now) =>
        folders
            .Where(f => f.Name != currentHash && IsHash(f.Name) && now - f.Written > KeepOldFor)
            .Select(f => f.Name)
            .ToList();

    private static bool IsHash(string name) =>
        name.Length == 12 && name.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
