using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Accessibility;

namespace Aiko.App;

/// Says when a window goes full screen and when it stops. The island hides while a game, a video
/// or a presentation owns the screen.
///
/// It listens for the event instead of asking every second: Aiko must not wake up when nothing is
/// happening.
[SupportedOSPlatform("windows5.1.2600")]
sealed class FullScreenWatch : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventSystemMinimizeEnd = 0x0017;
    private const uint OutOfContext = 0x0000;

    /// The desktop itself covers the whole screen and is not a full screen window.
    private static readonly string[] ShellClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Windows.UI.Core.CoreWindow"];

    /// Kept in a field on purpose: Windows holds only a raw pointer to it, and a collected
    /// delegate would crash the process when the next event arrives.
    private readonly WINEVENTPROC _callback;

    private readonly UnhookWinEventSafeHandle _foreground;
    private readonly UnhookWinEventSafeHandle _restored;

    public FullScreenWatch()
    {
        _callback = OnEvent;

        _foreground = PInvoke.SetWinEventHook(
            EventSystemForeground, EventSystemForeground, null, _callback, 0, 0, OutOfContext);

        // Leaving a full screen game often restores a window rather than changing the foreground.
        _restored = PInvoke.SetWinEventHook(
            EventSystemMinimizeEnd, EventSystemMinimizeEnd, null, _callback, 0, 0, OutOfContext);
    }

    /// True while a full screen window is in front.
    public event Action<bool>? Changed;

    private void OnEvent(HWINEVENTHOOK hook, uint id, HWND window, int objectId, int childId, uint thread, uint time) =>
        Changed?.Invoke(IsFullScreenInFront());

    public static bool IsFullScreenInFront()
    {
        var front = PInvoke.GetForegroundWindow();
        if (front.IsNull)
        {
            return false;
        }

        var name = ClassName(front);
        if (ShellClasses.Contains(name))
        {
            return false;
        }

        if (!PInvoke.GetWindowRect(front, out var window))
        {
            return false;
        }

        var monitor = PInvoke.MonitorFromWindow(front, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!PInvoke.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        var screen = info.rcMonitor;
        return window.left <= screen.left
            && window.top <= screen.top
            && window.right >= screen.right
            && window.bottom >= screen.bottom;
    }

    private static unsafe string ClassName(HWND window)
    {
        Span<char> name = stackalloc char[128];
        fixed (char* buffer = name)
        {
            var length = PInvoke.GetClassName(window, buffer, name.Length);
            return length > 0 ? new string(buffer, 0, length) : string.Empty;
        }
    }

    public void Dispose()
    {
        _foreground.Dispose();
        _restored.Dispose();
    }
}
