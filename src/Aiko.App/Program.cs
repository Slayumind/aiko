using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Aiko.App;

[SupportedOSPlatform("windows10.0.17763")]
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Software rendering: the spike measured 33 MB against 92 MB with the DirectX stack, and
        // Aiko draws a small card a few times an hour. Nobody needs the GPU for that.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

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
                ? PatchOutcome.Failed("the bridge program was not found")
                : ClaudeSettingsFile.AddBridge(folder, bridge);

            Log.Write($"--try-access {folder}: changed={outcome.Changed} problem={outcome.Problem ?? "none"}");
            Environment.Exit(outcome.Changed ? 0 : 1);
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
        TrayIcon? tray = null;

        application.Startup += (_, _) =>
        {
            tray = new TrayIcon(application);
            tray.Show();
        };
        application.Exit += (_, _) => tray?.Dispose();

        application.Run();
    }
}
