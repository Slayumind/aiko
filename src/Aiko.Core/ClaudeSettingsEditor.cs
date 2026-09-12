namespace Aiko.Core;

/// Why a change to the settings file did not happen. A reason, not a sentence: the words belong
/// to whichever layer is talking to a person, and the core has no language.
public enum PatchProblem
{
    None,

    /// Aiko cannot say where its own bridge program is, so there is no command to write.
    BridgeUnknown,

    /// The file is there and Aiko may not write it.
    CouldNotWrite,
}

/// What happened to the Claude Code settings file. The wizard shows this to the user, so a failure
/// has to name what went wrong rather than throw.
public sealed record PatchOutcome(bool Changed, PatchProblem Problem)
{
    public static readonly PatchOutcome NothingToDo = new(false, PatchProblem.None);

    public static readonly PatchOutcome Done = new(true, PatchProblem.None);

    public static PatchOutcome Failed(PatchProblem problem) => new(false, problem);
}

/// Adding and removing Aiko's line in the settings file of one Claude Code environment.
///
/// This is the most dangerous code in the product: it writes to a file that belongs to another
/// program, and removal rewrites one in every Claude Code folder on the machine. It lives here,
/// behind a narrow file interface, so every one of those paths can be tested without a real disk.
///
/// Three rules. Copy the file once, before the first change, because a second copy would save our
/// own edit and lose what the user had. Write through a temporary file, because a half written
/// settings file breaks Claude Code, not just Aiko. And never write at all when the patch changed
/// nothing.
public sealed class ClaudeSettingsEditor(IFileAccess files)
{
    public const string FileName = "settings.json";
    public const string BackupName = "settings.json.aiko-backup";

    public static string PathIn(string configDirectory) => Path.Combine(configDirectory, FileName);

    public static string BackupPathIn(string configDirectory) => Path.Combine(configDirectory, BackupName);

    public PatchOutcome Add(string configDirectory, string command)
    {
        if (string.IsNullOrEmpty(command))
        {
            return PatchOutcome.Failed(PatchProblem.BridgeUnknown);
        }

        return Change(configDirectory, json =>
            SettingsJsonPatch.TryAddBridge(json, command, out var patched) ? patched : null);
    }

    public PatchOutcome Remove(string configDirectory)
    {
        var outcome = Change(configDirectory, json =>
            SettingsJsonPatch.TryRemoveBridge(json, out var restored) ? restored : null);

        if (outcome.Changed)
        {
            // The copy was insurance while Aiko was in the file. Their own status line is back now,
            // and leaving the copy behind would be litter in someone else's folder.
            DropBackup(configDirectory);
        }

        return outcome;
    }

    /// Put our line right again, and only when it is already there.
    ///
    /// Aiko's own path changes on a reinstall, and the shell changes the day Git arrives. Either
    /// leaves a line of ours pointing somewhere useless. This fixes that without ever installing
    /// the line: somebody who answered "not now" in the wizard has to keep that answer, and a file
    /// with no line of ours in it is not ours to write to.
    public PatchOutcome RepairIfOurs(string configDirectory, string command)
    {
        if (string.IsNullOrEmpty(command))
        {
            return PatchOutcome.NothingToDo;
        }

        return Change(configDirectory, json =>
        {
            if (!SettingsJsonPatch.HasOurLine(json))
            {
                return null;
            }

            return SettingsJsonPatch.TryAddBridge(json, command, out var patched) ? patched : null;
        });
    }

    private void DropBackup(string configDirectory)
    {
        try
        {
            var backup = BackupPathIn(configDirectory);
            if (files.Exists(backup))
            {
                files.Delete(backup);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private PatchOutcome Change(string configDirectory, Func<string, string?> patch)
    {
        var path = PathIn(configDirectory);

        try
        {
            // Claude Code writes this file itself, but it may not exist yet on a fresh account.
            var json = files.Exists(path) ? files.ReadAllText(path) : "{}";

            var patched = patch(json);
            if (patched is null)
            {
                // Nothing to change: our line is already right, or there was nothing to take out.
                return PatchOutcome.NothingToDo;
            }

            Backup(path);
            WriteAtomically(path, patched);
            return PatchOutcome.Done;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return PatchOutcome.Failed(PatchProblem.CouldNotWrite);
        }
    }

    /// Made once, before the first change. A later copy would save our own edit over the original
    /// and lose what the user had.
    private void Backup(string path)
    {
        if (!files.Exists(path))
        {
            return;
        }

        var backup = Path.Combine(Path.GetDirectoryName(path)!, BackupName);
        if (!files.Exists(backup))
        {
            files.Copy(path, backup);
        }
    }

    private void WriteAtomically(string path, string text)
    {
        var temporary = path + ".aiko.tmp";
        files.WriteAllText(temporary, text);
        files.Move(temporary, path);
    }
}
