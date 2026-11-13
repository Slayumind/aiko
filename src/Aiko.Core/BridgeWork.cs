namespace Aiko.Core;

/// What one run of the bridge was asked to do. Claude Code starts the bridge for several jobs and
/// tells them apart by the arguments.
public enum BridgeJob
{
    /// "plugin aiko-persona": write the persona plugin and print its folder.
    PersonaPlugin,

    /// "memory rules|facts|prompt|serve": the three memory hooks and the memory server.
    Memory,

    /// "--session-start": the hook that warns when a command overrode a folder binding.
    SessionStart,

    /// "terminal-event": one event of a session in the terminal of Aiko (D-360). Windows only until
    /// the terminal comes to the Mac.
    TerminalHook,

    /// "hook": one session event for the face in the tray.
    Hook,

    /// No argument we know: the status line.
    StatusLine,
}

/// A file to write in one step: the bridge writes it beside the old one and moves it over.
public sealed record FileWrite(string Path, string Text);

/// What a status line run has to do: a snapshot to write, and the command the user had before,
/// which the bridge runs and prints so nothing they built is lost.
public sealed record StatusLinePlan(FileWrite? Snapshot, string? WrappedCommand);

/// What a hook event does to the activity file of its session: nothing, remove it, or write it.
public sealed record ActivityStep(string? Remove, FileWrite? Write)
{
    public static readonly ActivityStep Nothing = new(null, null);
}

/// The decisions of the bridge, with no disk and no processes. The executable reads stdin, the
/// environment and the files, and does what these answers say. The twin of BridgeWork.swift.
public static class BridgeWork
{
    /// The plugin job is decided before anything is read from stdin: Claude Code sends nothing
    /// there, and waiting for input that never comes would hang the install (D-201). The memory
    /// server is decided that early too, for the opposite reason: its input does not end until the
    /// session does.
    public static BridgeJob JobFor(IReadOnlyList<string> arguments)
    {
        if (PersonaPluginOutput.IsRequest(arguments))
        {
            return BridgeJob.PersonaPlugin;
        }

        if (MemoryBridge.ModeFor(arguments) is not null)
        {
            return BridgeJob.Memory;
        }

        return arguments.Count == 0 ? BridgeJob.StatusLine : arguments[0] switch
        {
            SessionReminder.Argument => BridgeJob.SessionStart,
            Terminal.TerminalEvent.HookArgument => BridgeJob.TerminalHook,
            PersonaPlugin.HookArgument => BridgeJob.Hook,
            _ => BridgeJob.StatusLine,
        };
    }

    /// The status line. Never throws and never refuses: anything unexpected reads as "no data",
    /// and the user's own line still runs.
    public static StatusLinePlan StatusLine(AikoFolders folders, string configDirectory, string? settingsJson, string input, DateTimeOffset now)
    {
        var environment = SnapshotName.For(configDirectory);
        var report = StatusLineReport.FromJson(input);
        var snapshot = report.HasData
            ? new FileWrite(folders.SnapshotFile(environment), SnapshotFile.ToJson(LimitSnapshot.FromStatusLine(environment, now, report)))
            : null;
        var wrapped = SettingsJsonPatch.ReadWrappedCommand(settingsJson ?? "");
        return new StatusLinePlan(snapshot, string.IsNullOrWhiteSpace(wrapped) ? null : wrapped);
    }

    /// One session event. The file of a session is read back first, because the same activity is
    /// not written again every few seconds: PostToolUse comes after every tool (D-206).
    public static ActivityStep Activity(AikoFolders folders, string configDirectory, string input, Func<string, string?> readFile, DateTimeOffset now)
    {
        if (HookEvent.FromJson(input) is not { } hook)
        {
            return ActivityStep.Nothing;
        }

        var environment = SnapshotName.For(configDirectory);
        var path = folders.Platform.Join(folders.ActivityFolder, ActivityRecord.FileName(environment, hook.SessionId));
        if (hook.Activity == SessionActivity.Ended)
        {
            return new ActivityStep(path, null);
        }

        var written = readFile(path) is { } text ? ActivityRecord.FromJson(text) : null;
        return ActivityRecord.NeedsWrite(written, hook.Activity, now)
            ? new ActivityStep(null, new FileWrite(path, new ActivityRecord(environment, hook.Activity, now).ToJson()))
            : ActivityStep.Nothing;
    }

    /// The session start hook. The answer is null far more often than not: it only speaks when a
    /// command started an environment other than the one this folder is bound to (D-163).
    public static string? SessionStart(
        string input,
        string? launch,
        string? runningEnvironment,
        string? environmentsJson,
        string? settingsJson,
        Func<AikoLanguage, bool> russian)
    {
        if (SessionReminder.WorkingDirectoryIn(input) is not { } folder)
        {
            return null;
        }

        var language = AppSettings.FromJson(settingsJson ?? "").Language;
        var message = SessionReminder.MessageFor(launch, runningEnvironment, folder, EnvironmentSettings.FromJson(environmentsJson ?? ""), russian(language));
        return message is null ? null : SessionReminder.HookOutput(message);
    }
}
