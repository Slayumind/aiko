using System.Windows;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Aiko.App;

/// Where Aiko's always-on-top windows sit next to the taskbar.
static class TaskbarOrder
{
    private const SET_WINDOW_POS_FLAGS KeepPlaceAndFocus =
        SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;

    /// Still above every ordinary window, but just below the taskbar. Over the bottom edge, whatever
    /// reaches past the edge then goes under the taskbar, the way it goes off the screen at the
    /// other edges: the glass strip's corners, and the island's small overshoot as it lands.
    public static void PutUnder(Window window)
    {
        var taskbar = PInvoke.FindWindow("Shell_TrayWnd", null);
        if (!taskbar.IsNull)
        {
            PInvoke.SetWindowPos((HWND)new WindowInteropHelper(window).Handle, taskbar, 0, 0, 0, 0, KeepPlaceAndFocus);
        }
    }

    /// Back on top of every always-on-top window, the taskbar and the glass strip among them.
    public static void PutOnTop(Window window) =>
        PInvoke.SetWindowPos((HWND)new WindowInteropHelper(window).Handle, HWND.HWND_TOPMOST, 0, 0, 0, 0, KeepPlaceAndFocus);
}
