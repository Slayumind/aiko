using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// The real disk, behind the narrow interface the core asks for.
sealed class RealFiles : IFileAccess
{
    public static readonly RealFiles Instance = new();

    public bool Exists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string text) => File.WriteAllText(path, text);

    public void Copy(string from, string to) => File.Copy(from, to);

    public void Move(string from, string to) => File.Move(from, to, overwrite: true);

    public void Delete(string path) => File.Delete(path);
}

/// Aiko's line in the settings file of one Claude Code environment.
///
/// The rules live in Aiko.Core.ClaudeSettingsEditor, where a test can reach them. What is left
/// here is the wiring: the real file system, the shell we detected, and the log.
static class ClaudeSettingsFile
{
    private static readonly ClaudeSettingsEditor Editor = new(RealFiles.Instance);

    public static string PathIn(string configDirectory) => ClaudeSettingsEditor.PathIn(configDirectory);

    public static string BackupPathIn(string configDirectory) =>
        ClaudeSettingsEditor.BackupPathIn(configDirectory);

    /// The exact text Aiko would add. The wizard shows it before asking, so nobody has to take our
    /// word for what we write into their file.
    public static string LineFor(string bridgeExePath) =>
        BridgeCommand.For(bridgeExePath, ShellDetect.Current());

    /// Whether our line is in this folder's settings. Without it Claude Code reports nothing and
    /// no number can ever arrive, which the card has to be able to say out loud.
    public static bool HasOurLine(string configDirectory)
    {
        try
        {
            var path = ClaudeSettingsEditor.PathIn(configDirectory);
            return File.Exists(path) && SettingsJsonPatch.HasOurLine(File.ReadAllText(path));
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static PatchOutcome AddBridge(string configDirectory, string bridgeExePath) =>
        Report(configDirectory, Editor.Add(configDirectory, LineFor(bridgeExePath)));

    /// The session start hook that reminds about folder bindings, written for the detected shell.
    public static PatchOutcome AddSessionHook(string configDirectory, string bridgeExePath) =>
        Report(configDirectory, Editor.AddSessionHook(configDirectory, BridgeCommand.HookFor(bridgeExePath, ShellDetect.Current())));

    public static PatchOutcome RemoveBridge(string configDirectory) =>
        Report(configDirectory, Editor.Remove(configDirectory));

    /// Called at startup for the folders Aiko already writes to. It never adds a line that is not
    /// there: a person who said "not now" keeps that answer.
    public static PatchOutcome RepairBridge(string configDirectory, string bridgeExePath) =>
        Report(configDirectory, Editor.RepairIfOurs(configDirectory, LineFor(bridgeExePath)));

    /// The reason in the user's language. The core names the reason; the words live here.
    public static string Words(PatchProblem problem) => problem switch
    {
        PatchProblem.BridgeUnknown => Strings.SettingsBridgeUnknown,
        PatchProblem.CouldNotWrite => Strings.SettingsWriteFailed,
        _ => string.Empty,
    };

    private static PatchOutcome Report(string configDirectory, PatchOutcome outcome)
    {
        var folder = Path.GetFileName(configDirectory.TrimEnd('\\', '/'));
        if (outcome.Changed)
        {
            Log.Write($"settings.json changed in {folder}");
        }
        else if (outcome.Problem != PatchProblem.None)
        {
            Log.Write($"could not change settings.json in {folder}: {outcome.Problem}");
        }

        return outcome;
    }
}
