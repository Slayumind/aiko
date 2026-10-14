using System.Windows;
using Aiko.Core.Terminal;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace Aiko.App;

/// The working areas of all screens, in the units a window is placed in. The main screen is first.
///
/// SystemParameters.WorkArea only knows the main screen, so a window closed on a second screen
/// would always come back to the first one.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
static class WorkAreas
{
    public static IReadOnlyList<ScreenArea> All()
    {
        var scale = MainScale();
        var main = new List<ScreenArea>();
        var others = new List<ScreenArea>();

        unsafe
        {
            PInvoke.EnumDisplayMonitors(HDC.Null, (RECT?)null, (monitor, _, _, _) =>
            {
                var info = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
                if (PInvoke.GetMonitorInfo(monitor, ref info))
                {
                    var work = info.rcWork;
                    var area = new ScreenArea(
                        work.left / scale, work.top / scale, (work.right - work.left) / scale, (work.bottom - work.top) / scale);
                    ((info.dwFlags & PInvoke.MONITORINFOF_PRIMARY) != 0 ? main : others).Add(area);
                }

                return true;
            }, 0);
        }

        if (main.Count + others.Count == 0)
        {
            var fallback = SystemParameters.WorkArea;
            return [new ScreenArea(fallback.Left, fallback.Top, fallback.Width, fallback.Height)];
        }

        return [.. main, .. others];
    }

    /// WPF places windows in the units of the main screen.
    private static double MainScale()
    {
        var monitor = PInvoke.MonitorFromPoint(default, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
        return PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpi, out _).Succeeded && dpi > 0
            ? dpi / 96.0
            : 1.0;
    }
}
