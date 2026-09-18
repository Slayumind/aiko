namespace Aiko.Core;

/// The name of the file one environment reports into.
///
/// The bridge only knows the config folder it was started for, so the file is named after that
/// folder. The tray has to work out the same name to match a file with the environment the user
/// named, and two copies of this rule would drift apart. So it lives here, and the bridge calls
/// it too.
public static class SnapshotName
{
    /// Claude Code started without CLAUDE_CONFIG_DIR uses its own default folder.
    public const string Default = "default";

    public static string For(string? configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            return Default;
        }

        return Clean(LastFolderName(configDirectory));
    }

    /// The last part of the path, cut by hand. Path.GetFileName follows the separators of the
    /// computer it runs on, so the same Windows path would give another name on a Mac. A drive
    /// such as "C:" counts as a separator too, the way Windows reads it.
    private static string LastFolderName(string path)
    {
        var trimmed = path.TrimEnd(Separators);
        return trimmed[(trimmed.LastIndexOfAny(SeparatorsAndDrive) + 1)..];
    }

    private static readonly char[] Separators = ['\\', '/'];

    private static readonly char[] SeparatorsAndDrive = ['\\', '/', ':'];

    /// A file name, not a folder name: the leading dot goes, and anything that does not belong in
    /// a file name goes with it.
    public static string Clean(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return Default;
        }

        var kept = new string(folderName.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.').ToArray())
            .Trim('.');

        return kept.Length > 0 ? kept : Default;
    }
}
