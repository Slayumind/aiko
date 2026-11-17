using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Aiko.App;

namespace Aiko.Checks;

/// --try-surface-leak report.txt: a closed session gives its terminal back (review 2026-11-17). A
/// window off the screen opens a command line in a terminal and lets it go the way a closed session
/// does, twenty times; the windows and other USER objects of the process are counted before and
/// after. A terminal kept after its session closed would add its window every time.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class SurfaceLeakCheck
{
    private const int Rounds = 20;

    public static int Run(string reportPath)
    {
        var report = new List<string>();
        var failed = false;
        var work = Path.Combine(Path.GetTempPath(), "aiko-surface-leak");
        Directory.CreateDirectory(work);

        var application = Tips.Install(new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown });
        application.Startup += async (_, _) =>
        {
            var holder = new Grid();
            var window = new Window
            {
                Left = -10000,
                Top = -10000,
                Width = 700,
                Height = 400,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                Content = holder,
            };
            window.Show();

            // One round first, so what WPF and the terminal load once is not counted as a leak.
            await Round(holder, work);
            var before = UserObjects();
            for (var round = 0; round < Rounds; round++)
            {
                await Round(holder, work);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(500);
            var after = UserObjects();

            // A few objects may come and go with the tips and the caret; a kept terminal is several
            // objects for every round.
            var grew = (long)after - before;
            failed = grew >= Rounds;
            report.Add($"{(failed ? "FAIL" : "PASS")} {Rounds} closed terminals give their windows back (USER objects {before} → {after})");

            window.Close();
            File.WriteAllLines(reportPath, report);
            application.Shutdown();
        };
        application.Run();
        return failed ? 1 : 0;
    }

    /// A command line opens, draws, and goes as CloseSession lets it go.
    private static async Task Round(Grid holder, string work)
    {
        var terminal = new TerminalSurface(new PtyLaunch("cmd.exe", work, new Dictionary<string, string?>()), blocks: false);
        holder.Children.Add(terminal);
        for (var waited = 0; waited < 5000 && !terminal.Drawn; waited += 50)
        {
            await Task.Delay(50);
        }

        holder.Children.Remove(terminal);
        terminal.Close();
        await Task.Delay(100);
    }

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process, uint flags);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    private static uint UserObjects() => GetGuiResources(GetCurrentProcess(), 1);
}
