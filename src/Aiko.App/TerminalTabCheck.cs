using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Aiko.App;

/// --try-tab: whether Tab in a terminal reaches the program before WPF takes it (D-370). A window
/// off the screen holds a real terminal with cmd.exe; a press of Tab and of Shift+Tab is put into the
/// queue of the window of the terminal, as the keyboard would put it, and the check sees whether the
/// hook of WindowKeys sent it to the program and emptied the message. Nothing reaches other windows
/// or the desktop.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class TerminalTabCheck
{
    private const int KeyDown = 0x0100;
    private const int Tab = 0x09;

    public static int Run(string reportPath)
    {
        var report = new List<string>();
        var failed = false;
        void Check(string what, bool ok)
        {
            failed |= !ok;
            report.Add($"{(ok ? "PASS" : "FAIL")} {what}");
        }

        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Startup += async (_, _) =>
        {
            var terminal = new TerminalSurface(new PtyLaunch("cmd.exe /k", Path.GetTempPath(), new Dictionary<string, string?>()));
            var window = new Window
            {
                Left = -10000,
                Top = -10000,
                Width = 600,
                Height = 300,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                Content = terminal,
            };
            window.Show();
            await Task.Delay(1500);

            var sent = new List<bool>();
            using var keys = new WindowKeys(new WindowInteropHelper(window).Handle, (_, _, _) => false)
            {
                TerminalTab = shift =>
                {
                    sent.Add(shift);
                    return true;
                },
            };

            var child = Descendants(terminal).OfType<HwndHost>().Select(host => host.Handle).FirstOrDefault(handle => handle != IntPtr.Zero);
            Check("the terminal has a window of its own", child != IntPtr.Zero);

            PostMessage(child, KeyDown, Tab, IntPtr.Zero);
            await Task.Delay(500);
            Check("Tab put into the window of the terminal goes to the program", sent.Count == 1 && !sent[0]);

            terminal.Close();
            window.Close();
            File.WriteAllLines(reportPath, report);
            application.Shutdown();
        };
        application.Run();
        return failed ? 1 : 0;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var at = 0; at < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); at++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, at);
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, int message, int wParam, IntPtr lParam);
}
