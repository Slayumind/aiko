using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// The environments while the settings window is open.
///
/// There is no Save button (D-179): every change comes through Commit, which writes the file and
/// puts the change into effect outside Aiko right away. What a change means for the list is decided
/// in Aiko.Core.EnvironmentEdits; this class only does the writing.
public sealed class EnvironmentsEditor
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public EnvironmentSettings Current { get; private set; } = SettingsStore.LoadEnvironments();

    /// Raised after every change that was saved.
    public event Action? Changed;

    /// Installed commands are known to terminals and scripts, so a rename leaves them alone (D-180).
    public bool CommandsInstalled => CommandFolder.IsSetUp;

    /// The line in the log names the change, never the environment: names are the person's own words.
    public void Commit(EnvironmentSettings next, string what)
    {
        if (ReferenceEquals(next, Current))
        {
            return;
        }

        var before = Current;
        Current = next;
        SettingsStore.SaveEnvironments(next);

        if (CommandFolder.IsSetUp && !Commands(before).SetEquals(Commands(next)))
        {
            CommandFolder.Sync(next);
        }

        if (!HasBindings(before) && HasBindings(next))
        {
            AddReminderHooks(next);
        }

        Log.Write($"settings: {what}");
        Changed?.Invoke();
    }

    /// Takes Aiko's line out of the environment's folder first, then the environment out of the
    /// list. Returns whether the folder went to the Recycle Bin.
    public bool Remove(string environment, bool toRecycleBin)
    {
        if (Current.Environments.FirstOrDefault(e => e.Name == environment) is not { } removed
            || !EnvironmentEdits.CanRemove(Current, environment, Home))
        {
            return false;
        }

        foreach (var folder in removed.ConfigDirectories)
        {
            ClaudeSettingsFile.RemoveBridge(folder);
        }

        Commit(EnvironmentEdits.Remove(Current, environment, Home), "removed an environment");

        return toRecycleBin && removed.ConfigDirectories.All(RecycleBin.Send);
    }

    private static HashSet<string> Commands(EnvironmentSettings settings) =>
        settings.Environments.Select(e => e.Command).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool HasBindings(EnvironmentSettings settings) =>
        settings.Environments.Any(e => e.ProjectFolders.Count > 0);

    /// The reminder goes only where the person already let Aiko write its line: a folder they said
    /// "not now" to is not ours to change.
    private static void AddReminderHooks(EnvironmentSettings settings)
    {
        if (BridgePath.Current() is not { } bridge)
        {
            return;
        }

        foreach (var folder in settings.Environments.SelectMany(e => e.ConfigDirectories).Where(ClaudeSettingsFile.HasOurLine))
        {
            ClaudeSettingsFile.AddSessionHook(folder, bridge);
        }
    }
}

/// Sends a folder to the Recycle Bin instead of deleting it, so the person can take it back.
static class RecycleBin
{
    private const uint Delete = 3;
    private const ushort Silent = 0x0004;
    private const ushort NoConfirmation = 0x0010;
    private const ushort AllowUndo = 0x0040;
    private const ushort NoErrorUi = 0x0400;

    public static bool Send(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return false;
        }

        var operation = new FileOperation
        {
            Function = Delete,
            // The list of paths ends with two zeros; the marshaller adds the second one.
            From = folder + '\0',
            Flags = (ushort)(AllowUndo | NoConfirmation | Silent | NoErrorUi),
        };

        var result = SHFileOperation(ref operation);
        var sent = result == 0 && !operation.AnyAborted && !Directory.Exists(folder);
        Log.Write($"recycle bin: {Path.GetFileName(folder)} {(sent ? "sent" : $"not sent ({result})")}");
        return sent;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct FileOperation
    {
        public nint Window;
        public uint Function;
        public string From;
        public string? To;
        public ushort Flags;
        public bool AnyAborted;
        public nint NameMappings;
        public string? ProgressTitle;
    }

#pragma warning disable SYSLIB1054
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    private static extern int SHFileOperation(ref FileOperation operation);
#pragma warning restore SYSLIB1054
}
