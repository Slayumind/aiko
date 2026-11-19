using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Aiko.Checks;

/// Whether the terminal window stays still when nothing changes: --try-idle report.txt [part].
/// The window of the snapshot, with its made up sessions, is shown off the screen. After 3 s to
/// settle, the check counts layout passes and the processor time of this process for 10 s. A
/// window at rest has no layout pass and almost no processor time. No program runs in it, so the
/// terminal itself is not part of this check (D-292). The part is one of --snapshot-terminal, or
/// "probe" for a window that moves on purpose.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class IdleCheck
{
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan Watch = TimeSpan.FromSeconds(10);

    /// More than this over the 10 s means something keeps drawing.
    private static readonly TimeSpan MostProcessorTime = TimeSpan.FromMilliseconds(150);

    public static int Run(string report, string? part)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new Window
        {
            // "probe" shows the loader, whose dots move all the time: the check must say MOVES on it,
            // which proves it can see a window that draws.
            Content = part == "probe"
                ? new System.Windows.Controls.Border { Width = 480, Height = 240, Child = new Aiko.App.TerminalLoader() }
                : TerminalSnapshot.Window(part),
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            // Off the screen: the window still lays out and draws, and nobody sees it.
            Left = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth + 100,
            Top = 0,
        };

        var layouts = 0;
        var watching = false;
        window.LayoutUpdated += (_, _) =>
        {
            if (watching)
            {
                layouts++;
            }
        };

        var result = 1;
        window.Show();
        var process = Process.GetCurrentProcess();
        var before = TimeSpan.Zero;
        var settle = new DispatcherTimer { Interval = Settle };
        settle.Tick += (_, _) =>
        {
            settle.Stop();
            process.Refresh();
            before = process.TotalProcessorTime;
            watching = true;
            var watch = new DispatcherTimer { Interval = Watch };
            watch.Tick += (_, _) =>
            {
                watch.Stop();
                watching = false;
                process.Refresh();
                var spent = process.TotalProcessorTime - before;
                var still = layouts == 0 && spent <= MostProcessorTime;
                File.WriteAllText(report,
                    $"--try-idle {part ?? "list"}: {layouts} layout passes, {spent.TotalMilliseconds:0} ms of processor time in {Watch.TotalSeconds:0} s: {(still ? "still" : "MOVES")}{Environment.NewLine}");
                result = still ? 0 : 1;
                window.Close();
                app.Shutdown();
            };
            watch.Start();
        };
        settle.Start();
        app.Run();
        return result;
    }
}
