using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// What happened to the Claude Code settings file. The wizard shows this to the user, so a
/// failure has to say what went wrong in words, not as an exception.
public sealed record PatchOutcome(bool Changed, string? Problem)
{
    public static readonly PatchOutcome NothingToDo = new(false, null);

    public static PatchOutcome Failed(string problem) => new(false, problem);

    public static readonly PatchOutcome Done = new(true, null);
}

/// Adding and removing Aiko's line in the settings file of one Claude Code environment.
///
/// The rules for the JSON itself live in the core; this is the part that touches the disk: a
/// backup first, then an atomic write, and never a half written settings file.
static class ClaudeSettingsFile
{
    private const string FileName = "settings.json";
    private const string BackupName = "settings.json.aiko-backup";

    public static string PathIn(string configDirectory) => Path.Combine(configDirectory, FileName);

    public static string BackupPathIn(string configDirectory) => Path.Combine(configDirectory, BackupName);

    /// The exact text Aiko would add. The wizard shows it before asking, so nobody has to take our
    /// word for what we write into their file.
    public static string LineFor(string bridgeExePath) =>
        BridgeCommand.For(bridgeExePath, ShellDetect.Current());

    public static PatchOutcome AddBridge(string configDirectory, string bridgeExePath)
    {
        var command = LineFor(bridgeExePath);
        if (command.Length == 0)
        {
            return PatchOutcome.Failed("Aiko could not work out where its bridge is.");
        }

        return Change(configDirectory, json =>
            SettingsJsonPatch.TryAddBridge(json, command, out var patched) ? patched : null);
    }

    public static PatchOutcome RemoveBridge(string configDirectory) =>
        Change(configDirectory, json =>
            SettingsJsonPatch.TryRemoveBridge(json, out var restored) ? restored : null);

    private static PatchOutcome Change(string configDirectory, Func<string, string?> patch)
    {
        var path = PathIn(configDirectory);

        try
        {
            // Claude Code writes this file itself, but it may not exist yet on a fresh account.
            var json = File.Exists(path) ? File.ReadAllText(path) : "{}";

            var patched = patch(json);
            if (patched is null)
            {
                // Nothing to change: our line is already there, or there was nothing to take out.
                return PatchOutcome.NothingToDo;
            }

            Backup(path);
            WriteAtomically(path, patched);
            Log.Write($"settings.json changed in {Path.GetFileName(configDirectory)}");
            return PatchOutcome.Done;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"could not change settings.json: {e.GetType().Name}");
            return PatchOutcome.Failed("Aiko could not write the Claude Code settings file.");
        }
    }

    /// Made once, before the first change. A later backup would copy our own edit over the
    /// original and lose what the user had.
    private static void Backup(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var backup = Path.Combine(Path.GetDirectoryName(path)!, BackupName);
        if (!File.Exists(backup))
        {
            File.Copy(path, backup);
        }
    }

    private static void WriteAtomically(string path, string text)
    {
        var temporary = path + ".aiko.tmp";
        File.WriteAllText(temporary, text);
        File.Move(temporary, path, overwrite: true);
    }
}
