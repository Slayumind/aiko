using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Aiko.App;

/// --try-shell-view report.txt [picture.png]: the checkpoint of shell blocks (D-412) in a real window off
/// the screen. Git Bash with Aiko's marks prints ten thousand lines, the view scrolls over them two
/// hundred steps and every frame is timed as the window would draw it, in software; then less takes
/// the whole screen and gives it back. Nothing outside a temporary folder is touched.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class ShellViewCheck
{
    /// One frame of 60 a second.
    private const double FrameMilliseconds = 16.7;

    public static int Run(string reportPath, string? picturePath)
    {
        var report = new List<string>();
        var failed = false;
        void Check(string what, bool ok, string detail = "")
        {
            failed |= !ok;
            report.Add($"{(ok ? "PASS" : "FAIL")} {what}{(detail.Length > 0 ? $" ({detail})" : "")}");
        }

        if (ShellLaunch.GitBash() is not { } bash)
        {
            File.WriteAllLines(reportPath, ["SKIP Git Bash is not found"]);
            return 0;
        }

        var work = Path.Combine(Path.GetTempPath(), "aiko-shell-view");
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(work, "note.txt"), "lantern\n");

        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Startup += async (_, _) =>
        {
            var terminal = new TerminalSurface(new PtyLaunch(ShellLaunch.Bash(bash).CommandLine, work, new Dictionary<string, string?>()), blocks: true);
            var fullScreen = new List<bool>();
            terminal.FullScreenChanged += fullScreen.Add;
            var window = new Window
            {
                Left = -10000,
                Top = -10000,
                Width = 900,
                Height = 560,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                Content = terminal,
            };
            window.Show();
            await Until(() => terminal.Drawn, 20);
            await Task.Delay(1000);

            terminal.Type("git status\r");
            await Task.Delay(1500);
            terminal.Type("ls nothing-here\r");
            await Task.Delay(1000);
            terminal.Type("seq 1 10000\r");
            await Task.Delay(4000);

            var view = terminal.BlocksForCheck!;
            Check("the blocks are shown and the terminal is hidden", view.IsVisible && !terminal.TerminalVisibleForCheck);
            Check("three blocks: git status, ls and seq", view.BlockCountForCheck == 3, $"{view.BlockCountForCheck}");

            if (picturePath is not null)
            {
                view.ScrollForCheck(-1e9);
                view.ScrollForCheck(view.RowsForCheck * 20);
                Save(window, picturePath);
            }

            view.UnfoldForCheck();
            Check("unfolded, the output of seq is ten thousand rows", view.RowsForCheck > 10_000, $"{view.RowsForCheck} rows");

            // Two hundred steps from the top down, each drawn into a bitmap as the window's software
            // renderer would draw it.
            view.ScrollForCheck(-1e9);
            var frames = new List<double>();
            var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            for (var step = 0; step < 200; step++)
            {
                view.ScrollForCheck(view.RowsForCheck * 20.0 / 200);
                var watch = Stopwatch.StartNew();
                bitmap.Clear();
                bitmap.Render(view);
                frames.Add(watch.Elapsed.TotalMilliseconds);
            }

            frames.Sort();
            var median = frames[frames.Count / 2];
            var worst = frames[^1];
            Check("a frame of ten thousand lines draws within a frame of 60 a second", frames[(int)(frames.Count * 0.95)] < FrameMilliseconds, $"median {median:0.0} ms, 95th {frames[(int)(frames.Count * 0.95)]:0.0} ms, worst {worst:0.0} ms");

            terminal.Type("less note.txt\r");
            await Until(() => fullScreen.Contains(true), 10);
            Check("less takes the whole screen: the terminal is shown", terminal.TerminalVisibleForCheck && !view.IsVisible);
            terminal.Type("q");
            await Until(() => fullScreen.Count >= 2, 10);
            await Task.Delay(300);
            Check("less gives it back: the blocks are shown again", !terminal.TerminalVisibleForCheck && view.IsVisible && fullScreen is [true, false], string.Join(",", fullScreen));

            terminal.Close();
            window.Close();
            File.WriteAllLines(reportPath, report);
            application.Shutdown();
        };
        application.Run();
        return failed ? 1 : 0;
    }

    private static void Save(Window window, string path)
    {
        var content = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private static async Task Until(Func<bool> done, int seconds)
    {
        var watch = Stopwatch.StartNew();
        while (!done() && watch.Elapsed.TotalSeconds < seconds)
        {
            await Task.Delay(100);
        }
    }
}
