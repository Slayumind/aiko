using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Aiko.App;

/// --try-dropdown: opens a small window with a DropDown and clicks it with the real mouse, the way
/// a person would. The drop-down had only ever been drawn in snapshots, and a snapshot cannot tell
/// whether a click on the list picks anything.
static class DropDownCheck
{
    public static int Run()
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var dropDown = new DropDown { Width = 200, HorizontalAlignment = HorizontalAlignment.Left, Items = ["First", "Second"], SelectedIndex = 0 };
        var window = new Window
        {
            Left = 200,
            Top = 200,
            Width = 320,
            Height = 200,
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Background = Tokens.Brush("Surface"),
            Content = new StackPanel { Margin = new Thickness(20), Children = { dropDown } },
        };

        var picked = -1;
        dropDown.Picked += index => picked = index;
        var result = 1;

        window.ContentRendered += async (_, _) =>
        {
            var button = dropDown.PointToScreen(new Point(100, 15));
            Click(button);
            await Delay(500);

            var open = dropDown.IsOpen;

            // The second item: under the button, past the list's margin, border, padding and first row.
            var second = dropDown.PointToScreen(new Point(100, 30 + 4 + 1 + 4 + 28 + 14));
            Click(second);
            await Delay(500);

            Log.Write($"--try-dropdown: opened={open}, picked={picked}, selected={dropDown.SelectedIndex}, closed={!dropDown.IsOpen}");
            result = open && picked == 1 && dropDown.SelectedIndex == 1 && !dropDown.IsOpen ? 0 : 1;
            application.Shutdown();
        };

        window.Show();
        application.Run();
        return result;
    }

    private static Task Delay(int milliseconds) => Task.Delay(milliseconds);

    /// Screen points from WPF are in real pixels, which is what the cursor wants.
    private static void Click(Point point)
    {
        SetCursorPos((int)point.X, (int)point.Y);
        mouse_event(LeftDown, 0, 0, 0, 0);
        mouse_event(LeftUp, 0, 0, 0, 0);
    }

    private const uint LeftDown = 0x0002;
    private const uint LeftUp = 0x0004;

#pragma warning disable SYSLIB1054
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nint extraInfo);
#pragma warning restore SYSLIB1054
}
