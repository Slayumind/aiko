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
