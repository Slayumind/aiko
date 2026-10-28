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
    private readonly Func<Key, bool, bool> _pressed;

    /// pressed gets the key and whether Shift is held, with Ctrl held, and says whether it took
    /// the key. Keys reach it only while a window of the terminal window has them.
    public WindowKeys(IntPtr window, Func<Key, bool, bool> pressed)
    {
        _window = window;
        _pressed = pressed;
        ComponentDispatcher.ThreadPreprocessMessage += OnMessage;
    }

    public void Dispose() => ComponentDispatcher.ThreadPreprocessMessage -= OnMessage;

    /// A mouse button came up in a window inside the terminal window: a real terminal (D-369).
    public event Action? ClickedInside;

    private const int LeftButtonUp = 0x0202;
    private const int RightButtonUp = 0x0205;

    private void OnMessage(ref MSG message, ref bool handled)
    {
        if (message.message is LeftButtonUp or RightButtonUp && message.hwnd != _window && IsChild(_window, message.hwnd))
        {
            ClickedInside?.Invoke();
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
        handled = _pressed(key, Held(VirtualShift));
    }

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
