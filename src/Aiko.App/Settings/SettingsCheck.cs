using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Aiko.App;

/// --try-settings: opens the settings window and uses it with the real mouse and keyboard, the way
/// the live run that found the bugs did. It picks the other environment for the first bound folder,
/// renames environment 2 with Enter, and finishes the checklist again, which must change nothing.
/// The environments file is put back afterwards.
static class SettingsCheck
{
    public static int Run()
    {
        var original = SettingsStore.LoadEnvironments();
        if (original.Environments.Count < 2 || Core.EnvironmentEdits.Bindings(original).Count == 0)
        {
            Log.Write("--try-settings: needs two environments and a bound folder");
            return 1;
        }

        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // The real window, not a stand-in: a drop-down list is a window of its own, and whether it
        // shows above its owner depends on the owner.
        var window = new SettingsWindow(SettingsPanel.FoldersPageKey);
        var panel = window.Panel;

        // On top, so every click and key of the check lands in this window and nowhere else.
        window.Topmost = true;

        var result = 1;
        var started = false;
        window.ContentRendered += async (_, _) =>
        {
            // Rendered again when the check puts the second page in; the walk runs once.
            if (started)
            {
                return;
            }

            started = true;
            try
            {
                result = await Walk(panel, original) ? 0 : 1;
            }
            catch (Exception failed)
            {
                Log.Write($"--try-settings: {failed.GetType().Name}: {failed.Message} {failed.StackTrace}");
            }
            finally
            {
                SettingsStore.SaveEnvironments(original);
                application.Shutdown();
            }
        };

        window.Show();
        application.Run();
        return result;
    }

    private static async Task<bool> Walk(SettingsPanel panel, Core.EnvironmentSettings original)
    {
        // ---- a folder moves to the other environment ----
        var first = Core.EnvironmentEdits.Bindings(original)[0];
        var names = original.Environments.Select(e => e.Name).ToList();
        var other = names.First(n => n != first.Environment);

        // A click on the header brings the window to the front, as a person's first click would.
        Click(panel.HeaderRow.PointToScreen(new Point(300, 20)));
        await Task.Delay(300);

        var folders = (FoldersPage)panel.PageHost.Content;
        var row = (Border)folders.Rows.Children[0];
        var choice = ((Grid)row.Child).Children.OfType<DropDown>().Single();

        // The keyboard, not the mouse: over a topmost window the list opens underneath it, and a
        // click meant for the list would land on the page. The mouse part is --try-dropdown.
        var picked = -1;
        choice.Picked += index => picked = index;
        choice.Focus();
        Press(Key.Down);
        await Task.Delay(300);
        var steps = names.IndexOf(other) - choice.SelectedIndex;
        for (var i = 0; i < Math.Abs(steps); i++)
        {
            Press(steps > 0 ? Key.Down : Key.Up);
        }

        Press(Key.Enter);
        await Task.Delay(500);

        var moved = picked == names.IndexOf(other) && Core.EnvironmentEdits.Bindings(SettingsStore.LoadEnvironments())
            .Any(b => Core.RealClaude.SameFolder(b.Folder, first.Folder) && b.Environment == other);

        // ---- environment 2 is renamed with Enter, and its command stays ----
        SettingsStore.SaveEnvironments(original);
        var second = original.Second(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))!;
        var fresh = new SettingsPanel(panel.EnvironmentPage(1));
        ((Window)panel.Parent).Content = fresh;
        await Task.Delay(400);

        var page = (EnvironmentPage)fresh.PageHost.Content;
        Click(page.NameBox.PointToScreen(new Point(40, 15)));
        await Task.Delay(200);
        page.NameBox.Text = second.Name + " Check";
        page.NameBox.CaretIndex = page.NameBox.Text.Length;
        Press(Key.Enter);
        await Task.Delay(500);

        var renamed = SettingsStore.LoadEnvironments().Environments.FirstOrDefault(e => e.Holds(second.ConfigDirectories[0]));
        var kept = renamed?.Name == second.Name + " Check" && renamed.Command == second.Command;
        var suggested = page.SuggestionPanel.IsOpen == (Core.LaunchCommand.FromEnvironmentName(second.Name + " Check") != second.Command && CommandFolder.IsSetUp);

        // ---- the checklist, gone through again and finished, changes nothing ----
        SettingsStore.SaveEnvironments(original);

        // Finish writes the status line, the reminder hook and the startup entry for the program that
        // runs it. From a development build they would point into bin\, so this part needs the
        // installed Aiko, which is the one build that carries the shim.
        if (CommandFolder.ShimInInstall() is null)
        {
            Log.Write($"--try-settings: folder moved={moved}, renamed with the command kept={kept}, suggestion shown right={suggested}, checklist skipped: run the installed Aiko for it");
            return moved && kept && suggested;
        }

        var third = new SettingsPanel(panel.EnvironmentPage(0));
        ((Window)fresh.Parent).Content = third;
        third.OpenChecklist(Core.ChecklistItem.Commands);
        await Task.Delay(600);

        var checklist = (ChecklistPage)third.PageHost.Content;
        checklist.FinishButton.BringIntoView();
        await Task.Delay(300);
        Click(checklist.FinishButton.PointToScreen(new Point(20, 14)));
        await Task.Delay(1000);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var after = SettingsStore.LoadEnvironments();
        var unchanged = Describe(after, home) == Describe(original, home);
        var noteShown = third.PageHost.Content is EnvironmentPage { NotePanel.IsOpen: true };

        Log.Write($"--try-settings: folder moved={moved}, renamed with the command kept={kept}, suggestion shown right={suggested}, checklist kept everything={unchanged}, note shown={noteShown}");
        return moved && kept && suggested && unchanged && noteShown;
    }

    /// Everything a person would notice about the environments, in one line to compare.
    private static string Describe(Core.EnvironmentSettings settings, string home) =>
        string.Join(" | ", settings.Environments.Select(e => $"{e.Name}/{e.Command}/{e.DirectMode}/{string.Join(";", e.ConfigDirectories)}/{string.Join(";", e.ProjectFolders)}"))
        + $" | ring {settings.Ring?.Name} | default {settings.Default(home)?.Name}";

    private static void Click(Point point)
    {
        SetCursorPos((int)point.X, (int)point.Y);
        mouse_event(LeftDown, 0, 0, 0, 0);
        mouse_event(LeftUp, 0, 0, 0, 0);
    }

    private static void Press(Key key)
    {
        var code = (byte)KeyInterop.VirtualKeyFromKey(key);
        keybd_event(code, 0, 0, 0);
        keybd_event(code, 0, KeyUp, 0);
    }

    private const uint LeftDown = 0x0002;
    private const uint LeftUp = 0x0004;
    private const uint KeyUp = 0x0002;

#pragma warning disable SYSLIB1054
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nint extraInfo);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void keybd_event(byte key, byte scan, uint flags, nint extraInfo);
#pragma warning restore SYSLIB1054
}
