using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Aiko.Core;
using Velopack;

namespace Aiko.App;

[SupportedOSPlatform("windows10.0.17763")]
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // First thing, before anything is drawn: the installer starts Aiko with its own arguments
        // for installing, updating and removing, and this handles them and exits.
        //
        // The hook before uninstalling is the reason this is here at all: it is where the status
        // line goes back into the Claude Code settings, as promised on the first run.
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => Uninstall.Cleanup())
            .Run();

        // Software rendering: the spike measured 33 MB against 92 MB with the DirectX stack, and
        // Aiko draws a small card a few times an hour. Nobody needs the GPU for that.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        // Before any window is made: the words are picked when a control is created, so a late
        // change would leave half a window in the other language.
        LanguageChoice.Apply(SettingsStore.Load().Language);

        // A snapshot draws a window at once and reads its pixels. Anything still fading in would be
        // caught half there.
        if (args is [var first, ..] && first.StartsWith("--snapshot", StringComparison.Ordinal))
        {
            Motion.TurnOff();
        }

        // Draw the icon into a file and stop. Windows hides new tray icons in the overflow area,
        // so this is the only way to look at the drawing itself.
        if (args is ["--snapshot-icon", var iconPath, ..])
        {
            IconSheet.Write(iconPath);
            return;
        }

        // The same for the card: draw it on test numbers and stop.
        if (args is ["--snapshot", var cardPath, ..])
        {
            CardSnapshot.Write(cardPath);
            return;
        }

        // Not test numbers but the real ones, to see what Aiko shows on this machine right now.
        if (args is ["--snapshot-live", var livePath, ..])
        {
            CardSnapshot.WriteLive(livePath);
            return;
        }

        // Adds the status line to one folder and says in the log what happened. For the question
        // "why can Aiko not write to my settings file", and for trying the change on a copy.
        if (args is ["--try-access", var folder, ..])
        {
            var bridge = BridgePath.Current();
            var outcome = bridge is null
                ? PatchOutcome.Failed(PatchProblem.BridgeUnknown)
                : ClaudeSettingsFile.AddBridge(folder, bridge);

            // The name, not the path. PRIVACY.md says the log holds a Claude Code folder name and
            // not the path to it, and a promise in that file has to hold for every code path.
            Log.Write($"--try-access {FolderName(folder)}: changed={outcome.Changed} problem={outcome.Problem}");
            Environment.Exit(outcome.Changed ? 0 : 1);
            return;
        }

        // The other half of --try-access: puts the status line back for one folder. Uninstalling
        // does this for every folder, and that is not something to try out on a live machine.
        if (args is ["--try-restore", var restoreFolder, ..])
        {
            var outcome = ClaudeSettingsFile.RemoveBridge(restoreFolder);
            Log.Write($"--try-restore {FolderName(restoreFolder)}: changed={outcome.Changed} problem={outcome.Problem}");
            Environment.Exit(outcome.Changed ? 0 : 1);
            return;
        }

        // Puts the saved environments into effect outside Aiko, the way the wizard's Finish does:
        // status lines and the reminder hook, the command folder in PATH, profile functions off.
        if (args is ["--apply-environments", ..])
        {
            Environment.Exit(EnvironmentSetup.ApplySaved() ? 0 : 1);
            return;
        }

        // Takes back the command folder, the PATH entry and the profile changes.
        if (args is ["--undo-environment-setup", ..])
        {
            EnvironmentSetup.Undo();
            Environment.Exit(0);
            return;
        }

        // Opens a drop-down in a small window and clicks it with the real mouse.
        if (args is ["--try-dropdown", ..])
        {
            Environment.Exit(DropDownCheck.Run());
            return;
        }

        // Drags the island with the real mouse to the left edge and checks where it lands.
        if (args is ["--try-island", ..])
        {
            Environment.Exit(IslandCheck.Run());
            return;
        }

        // Uses the settings window with the real mouse and keyboard, then puts the environments back.
        if (args is ["--try-settings", ..])
        {
            Environment.Exit(SettingsCheck.Run());
            return;
        }

        // Where Aiko would find Claude Code, read from a fresh PATH. Opens nothing.
        if (args is ["--try-claude", ..])
        {
            var claude = ClaudeLauncher.FindClaude();
            Log.Write($"--try-claude: {(claude is null ? "not found" : Path.GetFileName(Path.GetDirectoryName(claude)) + "\\" + Path.GetFileName(claude))}");
            Environment.Exit(claude is null ? 1 : 0);
            return;
        }

        // Sets the command folder up from the installed shim, reports what is in it and removes it
        // again. PATH is not touched: that part is checked in Windows Sandbox.
        if (args is ["--try-commands", ..])
        {
            var ok = CommandFolder.Sync(SettingsStore.LoadEnvironments());
            var files = ok ? Directory.EnumerateFiles(CommandFolder.Folder).Select(Path.GetFileName) : [];
            Log.Write($"--try-commands: set up={ok}, files: {string.Join(", ", files)}");
            if (ok)
            {
                Directory.Delete(CommandFolder.Folder, recursive: true);
            }

            Environment.Exit(ok ? 0 : 1);
            return;
        }

        // Turns the account switching functions off and on again in a copy of a profile, and
        // checks that the copy came back byte for byte. For trying it without touching the real one.
        if (args is ["--try-profile", var profileCopy, ..])
        {
            var before = File.ReadAllBytes(profileCopy);
            var found = PowerShellProfile.FindClaudeSwitchers(System.Text.Encoding.Latin1.GetString(before));
            var turnedOff = PowerShellProfiles.TurnOff(profileCopy, found);
            var remaining = PowerShellProfile.FindClaudeSwitchers(File.ReadAllText(profileCopy, System.Text.Encoding.Latin1)).Count;
            var backupThere = File.Exists(profileCopy + PowerShellProfiles.BackupSuffix);

            var text = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(profileCopy));
            File.WriteAllBytes(profileCopy, System.Text.Encoding.Latin1.GetBytes(PowerShellProfile.TurnOn(text)));
            File.Delete(profileCopy + PowerShellProfiles.BackupSuffix);
            var same = before.AsSpan().SequenceEqual(File.ReadAllBytes(profileCopy));

            Log.Write($"--try-profile: found {found.Count} ({string.Join(", ", found.Select(f => f.Name))}), turned off={turnedOff}, left after={remaining}, backup={backupThere}, same bytes after on={same}");
            Environment.Exit(same && turnedOff && remaining == 0 ? 0 : 1);
            return;
        }

        if (args is ["--snapshot-island", var islandPath, ..])
        {
            var edge = args.Length > 2 && Enum.TryParse<ScreenEdge>(args[2], true, out var asked)
                ? asked
                : ScreenEdge.Top;

            var panel = new IslandPanel();
            panel.Show(CardSnapshot.Example(), edge);
            Snapshot.Write(panel, islandPath);
            return;
        }

        if (args is ["--snapshot-wizard", var wizardPath, ..])
        {
            // The item to open, by name or number: --snapshot-wizard out.png Commands
            ChecklistItem? item = args.Length > 2 && Enum.TryParse<ChecklistItem>(args[2], true, out var asked) ? asked : null;
            var checklist = new SettingsPanel(SettingsPanel.ChecklistPageKey);
            if (item is { } open)
            {
                checklist.OpenChecklist(open);
            }

            Snapshot.Write(checklist, wizardPath);
            return;
        }

        if (args is ["--snapshot-settings", var settingsPath, ..])
        {
            // The page to draw: --snapshot-settings out.png general | folders | env1 | env2 [tall]
            // "tall" draws the whole page, not only the part that fits the window.
            var page = args.Length > 2 ? args[2] : null;
            var panel = new SettingsPanel(page switch
            {
                "env1" => new SettingsPanel().EnvironmentPage(0),
                "env2" => new SettingsPanel().EnvironmentPage(1),
                _ => page,
            });
            if (args is [.., "tall"])
            {
                panel.GrowToPage();
            }
            Snapshot.Write(panel, settingsPath);
            return;
        }

        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        AikoShell? shell = null;

        application.Startup += (_, _) =>
        {
            shell = new AikoShell(application);
            shell.Show();
        };
        application.Exit += (_, _) => shell?.Dispose();

        application.Run();
    }

    /// The last part of a folder path, whichever separator it ends with.
    private static string FolderName(string path) =>
        Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
}
