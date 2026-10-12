using System.Windows;
using System.Windows.Media;
using Windows.Win32;

namespace Aiko.App;

/// Where the mouse is, for the windows that stay open while it rests on them.
static class Pointer
{
    public static bool IsOver(Window window)
    {
        if (!PInvoke.GetCursorPos(out var point))
        {
            // Without an answer, assume the mouse is still there: closing a card somebody is
            // reading is worse than leaving one up a moment longer.
            return true;
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        var bounds = new Rect(
            window.Left * dpi.DpiScaleX,
            window.Top * dpi.DpiScaleY,
            window.ActualWidth * dpi.DpiScaleX,
            window.ActualHeight * dpi.DpiScaleY);

        return bounds.Contains(point.X, point.Y);
    }
}
