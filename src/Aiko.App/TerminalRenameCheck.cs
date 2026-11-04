using System.IO;
using System.Windows;
using System.Windows.Input;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// --try-rename: a change of a session name never freezes the list (D-389). A window off the screen
/// holds the list on made up sessions; the check starts a change of a name the ways a person can
/// and sees the list follow afterwards. No program runs and nothing is read or written.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class TerminalRenameCheck
{
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
            var panel = new TerminalPanel { Width = 1000, Height = 600 };
            var window = new Window
            {
                Left = -10000,
                Top = -10000,
                Width = 1000,
                Height = 600,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                Content = panel,
            };
            window.Show();

            panel.Show(View("Work"));
            Check("the list of Work is drawn", panel.RowNamesForCheck.SequenceEqual(["Report", "Deploy"]));

            // The field takes the keys, so leaving it ends the change; without them the change ends by
            // itself. Either way it cannot hang on.
            panel.StartRenameForCheck();
            Check("a change of a name starts", panel.RenamingForCheck);
            await Task.Delay(300);
            Check("the field has the keys or the change ended", panel.RenameFieldFocusedForCheck || !panel.RenamingForCheck);
            PressAway(panel);

            // Another environment while a name changes: its list comes.
            panel.StartRenameForCheck();
            panel.Show(View("Aiko"));
            Check("another environment brings its list during a change", panel.RowNamesForCheck.SequenceEqual(["Voice", "Menu"]) && !panel.RenamingForCheck);

            // A press away from the field, on something that takes no keys.
            panel.StartRenameForCheck();
            PressAway(panel);
            Check("a press away from the field ends the change", !panel.RenamingForCheck);

            // The mouse over a row while the list is drawn anew: the new row starts lit, no blink.
            panel.FirstRowForCheck!.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
            panel.Show(View("Aiko"));
            Check("a row drawn anew under the mouse starts lit", panel.FirstRowLightForCheck == 1);

            window.Close();
            File.WriteAllLines(reportPath, report);
            application.Shutdown();
        };
        application.Run();
        return failed ? 1 : 0;
    }

    /// A press of the mouse on the panel itself, away from the field, as on a pill or a row.
    private static void PressAway(TerminalPanel panel) =>
        panel.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseDownEvent, Source = panel });

    private static TerminalView View(string environment)
    {
        var now = DateTimeOffset.Now;
        var seconds = now.ToUnixTimeSeconds();
        var names = environment == "Work" ? ("Report", "Deploy") : ("Voice", "Menu");
        return new TerminalView(
            [new TabView("Aiko", environment == "Aiko", 0, 40, LimitTone.Normal), new TabView("Work", environment == "Work", 0, 20, LimitTone.Normal)],
            new HeaderView(false, false, []),
            [
                new RowGroup("C:\\work\\" + environment, environment.ToLowerInvariant(),
                [
                    new SessionRow(environment + "1", names.Item1, RowState.Read, "", seconds),
                    new SessionRow(environment + "2", names.Item2, RowState.Read, "", seconds),
                ]),
            ],
            environment,
            environment + "1",
            names.Item1,
            MainView.NoSessions,
            null,
            null,
            now);
    }
}
