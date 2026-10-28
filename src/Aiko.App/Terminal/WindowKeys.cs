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

        // Tab is taken earlier than that: WPF listens to the messages of the thread before this
        // class does, and its navigation marks Tab as handled before any other listener sees it
        // (D-370). A hook of this thread sees a message when it is taken from the queue.
        _hookProc = OnGetMessage;
        _hook = SetWindowsHookEx(GetMessageHook, _hookProc, IntPtr.Zero, GetCurrentThreadId());
    }

    public void Dispose()
    {
        ComponentDispatcher.ThreadPreprocessMessage -= OnMessage;
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    /// A mouse button came up in a window inside the terminal window: a real terminal (D-369).
    public event Action? ClickedInside;

    /// Files were dropped on a terminal (D-370).
    public event Action<IReadOnlyList<string>>? Dropped;

    /// Tab in a terminal, with Shift or without: true when it went to the program.
    public Func<bool, bool>? TerminalTab { get; set; }

    private const int VirtualTab = 0x09;

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

    // ---- Tab, before WPF ----

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    private const int GetMessageHook = 3;
    private const int Remove = 1;
    private const int NoMessage = 0;

    /// Kept here: the system calls it, and a collected delegate would crash the hook.
    private readonly HookProc _hookProc;
    private IntPtr _hook;

    /// Tab and Shift+Tab in a terminal belong to the program: WPF would move the focus over the
    /// buttons of the window with them (D-370). The terminal has the keys when the message comes to
    /// its window, as the log of the owner showed. The key goes to the program, and the message
    /// becomes an empty one, so WPF never sees it.
    private IntPtr OnGetMessage(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam == Remove)
        {
            var message = Marshal.PtrToStructure<MSG>(lParam);
            if (message.message == KeyDown && (int)message.wParam == VirtualTab && !Held(VirtualControl) && !Held(VirtualAlt)
                && message.hwnd != _window && IsChild(_window, message.hwnd)
                && (TerminalTab?.Invoke(Held(VirtualShift)) ?? false))
            {
                message.message = NoMessage;
                Marshal.StructureToPtr(message, lParam, false);
            }
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hook, HookProc proc, IntPtr module, uint thread);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

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
