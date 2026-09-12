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

            Log.Write($"--try-access {folder}: changed={outcome.Changed} problem={outcome.Problem}");
            Environment.Exit(outcome.Changed ? 0 : 1);
            return;
        }

        // The other half of --try-access: puts the status line back for one folder. Uninstalling
        // does this for every folder, and that is not something to try out on a live machine.
        if (args is ["--try-restore", var restoreFolder, ..])
        {
            var outcome = ClaudeSettingsFile.RemoveBridge(restoreFolder);
            Log.Write($"--try-restore {restoreFolder}: changed={outcome.Changed} problem={outcome.Problem}");
            Environment.Exit(outcome.Changed ? 0 : 1);
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
            var step = args.Length > 2 && int.TryParse(args[2], out var asked) ? asked : 0;
            Snapshot.Write(new WizardPanel(step), wizardPath);
            return;
        }

        if (args is ["--snapshot-settings", var settingsPath, ..])
        {
            Snapshot.Write(new SettingsPanel(), settingsPath);
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
}
