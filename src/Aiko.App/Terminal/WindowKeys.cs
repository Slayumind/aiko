using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Aiko.App;

/// The keys of the terminal window itself (D-353): only Ctrl+Shift+… and Ctrl+Tab, everything else
/// goes to the program in the terminal. A real terminal is a window of Win32 inside the WPF one, and
/// the key events of WPF never see what goes to it. So the keys are taken from the messages of the
/// thread, before any window gets them.
sealed class WindowKeys : IDisposable
{
    private const int KeyDown = 0x0100;
    private const int SystemKeyDown = 0x0104;

    private readonly IntPtr _window;
    private readonly Func<Key, bool, bool, bool> _pressed;

    /// pressed gets the key, whether Shift is held and whether a terminal has the keys, with Ctrl
    /// held, and says whether it took the key. Keys reach it only while a window of the terminal
    /// window has them.
    public WindowKeys(IntPtr window, Func<Key, bool, bool, bool> pressed)
    {
        _window = window;
        _pressed = pressed;
        ComponentDispatcher.ThreadPreprocessMessage += OnMessage;
    }

    public void Dispose() => ComponentDispatcher.ThreadPreprocessMessage -= OnMessage;

    /// A mouse button came up in a window inside the terminal window: a real terminal (D-369).
    public event Action? ClickedInside;

    /// Files were dropped on a terminal (D-370).
    public event Action<IReadOnlyList<string>>? Dropped;

    private const int DropFiles = 0x0233;

    private const int LeftButtonUp = 0x0202;
    private const int RightButtonUp = 0x0205;

    private void OnMessage(ref MSG message, ref bool handled)
    {
        if (message.message is LeftButtonUp or RightButtonUp && message.hwnd != _window && IsChild(_window, message.hwnd))
        {
            ClickedInside?.Invoke();
            return;
        }

        if (message.message == DropFiles && IsChild(_window, message.hwnd))
        {
            handled = true;
            Dropped?.Invoke(DroppedPaths(message.wParam));
            return;
        }

        if (handled || message.message is not (KeyDown or SystemKeyDown)
            || (message.hwnd != _window && !IsChild(_window, message.hwnd)))
        {
            return;
        }

        // The state of the keys from the system: WPF has not seen the keys a child window got.
        if (!Held(VirtualControl) || Held(VirtualAlt))
        {
            return;
        }

        var key = KeyInterop.KeyFromVirtualKey((int)message.wParam);
        handled = _pressed(key, Held(VirtualShift), message.hwnd != _window);
    }

    /// The paths of a drop, read once; the drop is let go after it.
    private static List<string> DroppedPaths(IntPtr drop)
    {
        var paths = new List<string>();
        var count = DragQueryFile(drop, 0xFFFFFFFF, null, 0);
        for (uint at = 0; at < count; at++)
        {
            var length = DragQueryFile(drop, at, null, 0);
            var name = new System.Text.StringBuilder((int)length + 1);
            if (DragQueryFile(drop, at, name, (uint)name.Capacity) > 0)
            {
                paths.Add(name.ToString());
            }
        }

        DragFinish(drop);
        return paths;
    }

    /// A window that takes files dropped on it: the terminal, whose window is not WPF's.
    public static void AcceptDrops(IntPtr window) => DragAcceptFiles(window, true);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(IntPtr drop, uint index, System.Text.StringBuilder? file, uint length);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr drop);

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool accept);

    private const int VirtualShift = 0x10;
    private const int VirtualControl = 0x11;
    private const int VirtualAlt = 0x12;

    private static bool Held(int virtualKey) => (GetKeyState(virtualKey) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsChild(IntPtr parent, IntPtr window);
}
