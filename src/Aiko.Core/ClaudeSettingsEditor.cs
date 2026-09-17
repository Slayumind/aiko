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
public sealed class ClaudeSettingsEditor(IFileAccess files, PlatformConventions platform)
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
            SettingsJsonPatch.TryAddBridge(platform, json, command, out var patched) ? patched : null);
    }

    /// The session start hook that reminds about folder bindings. Added with the first binding,
    /// and taken out by Remove together with the status line.
    public PatchOutcome AddSessionHook(string configDirectory, string hookCommand)
    {
        if (string.IsNullOrEmpty(hookCommand))
        {
            return PatchOutcome.Failed(PatchProblem.BridgeUnknown);
        }

        return Change(configDirectory, json =>
            SettingsJsonPatch.TryAddSessionHook(platform, json, hookCommand, out var patched) ? patched : null);
    }

    /// Everything of Aiko's in the file: the status line, the hook, the plugins and the marketplace.
    public PatchOutcome Remove(string configDirectory)
    {
        // A copy is only ever made of a file that was already there. No copy means Aiko made this
        // file itself, from nothing, the day it added its line.
        var aikoMadeTheFile = !files.Exists(BackupPathIn(configDirectory));

        var outcome = Change(configDirectory, json =>
        {
            var bridge = SettingsJsonPatch.TryRemoveBridge(platform, json, out var withoutBridge);
            var plugins = SettingsJsonPatch.TryRemovePlugins(withoutBridge, out var withoutPlugins);
            return bridge || plugins ? withoutPlugins : null;
        });

        if (outcome.Changed)
        {
            // The copy was insurance while Aiko was in the file. Once nothing of ours is left,
            // leaving the copy behind would be litter in someone else's folder.
            if (!HasAnyAikoEntries(configDirectory))
            {
                DropBackup(configDirectory);
            }

            // A file Aiko made and that now says nothing is litter too. Windows Sandbox showed it: a
            // folder with no settings.json before Aiko had one holding "{}" after it. Anything the
            // user or Claude Code wrote into it since keeps it alive.
            if (aikoMadeTheFile)
            {
                DeleteIfEmpty(PathIn(configDirectory));
            }
        }

        return outcome;
    }

    /// After "claude plugin uninstall": the empty keys it leaves. The backup and the file stay.
    public PatchOutcome TidyAfterPluginRemoval(string configDirectory) =>
        Change(configDirectory, json =>
            SettingsJsonPatch.TryDropEmptyPluginKeys(json, out var tidy) ? tidy : null);

    private bool HasAnyAikoEntries(string configDirectory)
    {
        try
        {
            var path = PathIn(configDirectory);
            return files.Exists(path) && SettingsJsonPatch.HasAnyAikoEntries(platform, files.ReadAllText(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Unreadable now: keeping the copy costs nothing.
            return true;
        }
    }

    private void DeleteIfEmpty(string path)
    {
        try
        {
            if (files.Exists(path)
                && System.Text.Json.Nodes.JsonNode.Parse(files.ReadAllText(path)) is System.Text.Json.Nodes.JsonObject { Count: 0 })
            {
                files.Delete(path);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
        }
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
            if (!SettingsJsonPatch.HasOurLine(platform, json))
            {
                return null;
            }

            return SettingsJsonPatch.TryAddBridge(platform, json, command, out var patched) ? patched : null;
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
