using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Aiko.App;

/// --try-language: picks English on the General page of the real settings window with the real
/// mouse, then puts the settings back. The owner could open the language list but a click on an
/// item changed nothing; --try-dropdown passes because it shows the list in a bare window.
static class LanguageCheck
{
    public static int Run()
    {
        var original = SettingsStore.Load();
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new SettingsWindow(SettingsPanel.GeneralPageKey) { Topmost = true };
        var page = (GeneralPage)window.Panel.PageHost.Content;
        var drop = page.LanguageChoiceDrop;

        var picked = -1;
        drop.Picked += index => picked = index;
        var result = 1;
        var started = false;

        window.ContentRendered += async (_, _) =>
        {
            if (started)
            {
                return;
            }

            started = true;
            try
            {
                drop.BringIntoView();
                await Task.Delay(400);

                Click(drop.PointToScreen(new Point(100, 15)));
                await Task.Delay(500);
                var opened = drop.IsOpen;

                var english = ItemOf(drop, 2);
                var target = english.PointToScreen(new Point(40, english.ActualHeight / 2));
                var listWindow = ((System.Windows.Interop.HwndSource)PresentationSource.FromVisual(english)).Handle;
                var settingsWindow = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                var under = WindowFromPoint(new NativePoint { X = (int)target.X, Y = (int)target.Y });
                Log.Write($"--try-language: list opened={opened}, English item at {target}, under the cursor: "
                    + (under == listWindow ? "the list" : under == settingsWindow ? "the settings window" : $"another window {under}"));

                SetCursorPos((int)target.X, (int)target.Y);
                await Task.Delay(200);
                mouse_event(LeftDown, 0, 0, 0, 0);
                await Task.Delay(100);
                var openAfterPress = drop.IsOpen;
                var focusAfterPress = Keyboard.FocusedElement?.GetType().Name ?? "none";
                mouse_event(LeftUp, 0, 0, 0, 0);
                await Task.Delay(500);

                Log.Write($"--try-language: open after press={openAfterPress}, focus after press={focusAfterPress}, picked={picked}");
                result = opened && picked == 2 ? 0 : 1;
            }
            catch (Exception failed)
            {
                Log.Write($"--try-language: {failed.GetType().Name}: {failed.Message}");
            }
            finally
            {
                SettingsStore.Save(original);
                application.Shutdown();
            }
        };

        window.Show();
        application.Run();
        return result;
    }

    /// The list is private to the drop-down on purpose; the check reaches in to find where a row is.
    private static FrameworkElement ItemOf(DropDown drop, int index)
    {
        var list = (StackPanel)typeof(DropDown)
            .GetField("_list", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(drop)!;
        return (FrameworkElement)list.Children[index];
    }

    private static void Click(Point point)
    {
        SetCursorPos((int)point.X, (int)point.Y);
        mouse_event(LeftDown, 0, 0, 0, 0);
        mouse_event(LeftUp, 0, 0, 0, 0);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private const uint LeftDown = 0x0002;
    private const uint LeftUp = 0x0004;

#pragma warning disable SYSLIB1054
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nint extraInfo);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint WindowFromPoint(NativePoint point);
#pragma warning restore SYSLIB1054
}
