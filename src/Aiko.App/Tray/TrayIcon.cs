using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Aiko.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Aiko.App;

/// The icon in the tray, and the hidden window Windows talks to it through. It draws the ring, the
/// dot and a face, keeps the tooltip, and says what the mouse did. What the numbers are and what a
/// click means is not its business: it asks for the rows and the tooltip, and raises events.
///
/// Windows 10 1809 is the minimum, the same as in app.manifest: asking the taskbar monitor for
/// its scaling needs it. Saying so here instead of in the project file keeps the WinRT
/// projections, and their tens of megabytes, out of the build.
[SupportedOSPlatform("windows10.0.17763")]
sealed class TrayIcon : IDisposable
{
    private const uint IconId = 1;
    private const uint CallbackMessage = PInvoke.WM_APP + 1;
    private const uint NinSelect = PInvoke.WM_USER;

    private readonly Func<(CardRow? Ring, CardRow? Dot)> _rows;
    private readonly Func<string> _tooltip;
    private readonly TrayFaceAnimator _faceAnimator;
    private readonly uint _taskbarCreatedMessage;

    /// The rows the icon last drew. Frames of a face transition reuse them instead of reading the
    /// settings and the snapshots sixteen times in half a second.
    private (CardRow? Ring, CardRow? Dot) _lastRows;
    private HwndSource? _source;
    private HICON _icon;
    private uint _iconDpi;
    private int _iconSize;

    public TrayIcon(Dispatcher dispatcher, Func<(CardRow? Ring, CardRow? Dot)> rows, Func<string> tooltip)
    {
        _rows = rows;
        _tooltip = tooltip;
        _taskbarCreatedMessage = PInvoke.RegisterWindowMessage("TaskbarCreated");
        _faceAnimator = new TrayFaceAnimator(dispatcher);
        _faceAnimator.Changed += Redraw;
    }

    /// A left click on the icon.
    public event Action? Clicked;

    /// The mouse is over the icon. It arrives many times a second.
    public event Action? MouseMoved;

    /// A right click, with the point Windows sent for the menu.
    public event Action<IntPtr>? MenuRequested;

    public bool IsShown { get; private set; }

    public bool HasFace => _faceAnimator.Face is not null;

    /// The window that owns the icon. The menu needs it to close after a click somewhere else.
    public HWND Window => (HWND)_source!.Handle;

    /// Makes the hidden window. The icon itself comes with Add.
    public void Open()
    {
        // A hidden top level window, not a message only one: broadcasts such as TaskbarCreated
        // never reach message only windows (spike 2026-09-11).
        _source = new HwndSource(new HwndSourceParameters("AikoTray") { Width = 0, Height = 0, WindowStyle = 0 });
        _source.AddHook(WndProc);
    }

    public void Add()
    {
        ReplacePicture(TaskbarDpi());

        var data = NewData();
        data.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP;
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = _icon;
        WriteTooltip(ref data);
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, in data);

        data.Anonymous.uVersion = PInvoke.NOTIFYICON_VERSION_4;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, in data);

        IsShown = true;
    }

    public void Remove()
    {
        if (!IsShown)
        {
            return;
        }

        var data = NewData();
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in data);
        IsShown = false;
    }

    /// New numbers: the picture and the tooltip are both made again.
    public void Update()
    {
        if (!IsShown)
        {
            return;
        }

        var old = ReplacePicture(TaskbarDpi());
        var data = NewData();
        data.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP;
        data.hIcon = _icon;
        WriteTooltip(ref data);
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in data);

        if (!old.IsNull)
        {
            PInvoke.DestroyIcon(old);
        }
    }

    /// A face comes onto the icon or goes (D-211). Nothing happens while the icon is not shown.
    public void ShowFace(AikoFace? face, FaceStyle style)
    {
        if (!IsShown)
        {
            return;
        }

        if (face is { } shown)
        {
            _faceAnimator.Show(FaceDrawing.For(style, shown, TaskbarTheme.Ground(), _iconSize));
        }
        else
        {
            _faceAnimator.Hide();
        }
    }

    /// Where Windows put our icon, in real pixels. It answers even when the icon sits in the
    /// overflow area, which is where Windows 11 puts a new app by default.
    public Rect? Rect
    {
        get
        {
            if (!IsShown)
            {
                return null;
            }

            var id = new NOTIFYICONIDENTIFIER
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
                hWnd = Window,
                uID = IconId,
            };

            return PInvoke.Shell_NotifyIconGetRect(in id, out var rect).Succeeded
                ? new Rect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top)
                : null;
        }
    }

    public bool CursorOver
    {
        get
        {
            if (Rect is not { } rect || !PInvoke.GetCursorPos(out var point))
            {
                return false;
            }

            return rect.Contains(point.X, point.Y);
        }
    }

    /// A message for the hidden window, from a check. A key sent this way reaches the open menu
    /// of the icon and never another program.
    public void PostForCheck(uint message, nuint wParam) => PInvoke.PostMessage(Window, message, wParam, 0);

    public void Dispose()
    {
        Remove();
        if (!_icon.IsNull)
        {
            PInvoke.DestroyIcon(_icon);
        }

        _source?.Dispose();
    }

    /// One frame of a face transition: only the picture changes, not the tooltip.
    private void Redraw()
    {
        if (!IsShown)
        {
            return;
        }

        var old = ReplacePicture(TaskbarDpi(), sameRows: true);
        var data = NewData();
        data.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_ICON;
        data.hIcon = _icon;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in data);

        if (!old.IsNull)
        {
            PInvoke.DestroyIcon(old);
        }
    }

    /// The numbers go in the tooltip as well as in the ring. Under the Windows 11 overflow arrow
    /// the hover card never opens, so for some people this is the whole product, and a screen
    /// reader has nothing else to read.
    private void WriteTooltip(ref NOTIFYICONDATAW data)
    {
        var text = _tooltip();
        var room = data.szTip.AsSpan();
        room.Clear();
        text.AsSpan(0, Math.Min(text.Length, room.Length - 1)).CopyTo(room);
    }

    private HICON ReplacePicture(uint dpi, bool sameRows = false)
    {
        var old = _icon;
        _iconSize = PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, dpi);
        _iconDpi = dpi;

        if (!sameRows)
        {
            _lastRows = _rows();
        }

        _icon = RingIcon.Render(_iconSize, _lastRows.Ring, _lastRows.Dot, _faceAnimator.Frame, _faceAnimator.Face);
        return old;
    }

    private static uint TaskbarDpi()
    {
        var taskbar = PInvoke.FindWindow("Shell_TrayWnd", null);
        var monitor = PInvoke.MonitorFromWindow(taskbar, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
        PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _);
        return dpiX;
    }

    private NOTIFYICONDATAW NewData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
        hWnd = Window,
        uID = IconId,
    };

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var message = (uint)msg;
        if (message == CallbackMessage)
        {
            OnIconMessage((uint)(lParam.ToInt64() & 0xFFFF), wParam);
            handled = true;
        }
        else if (message == _taskbarCreatedMessage)
        {
            // Explorer restarted and forgot every icon; ours has to introduce itself again.
            if (IsShown)
            {
                IsShown = false;
                Add();
            }
        }
        else if (message is PInvoke.WM_DPICHANGED or PInvoke.WM_DISPLAYCHANGE or PInvoke.WM_SETTINGCHANGE)
        {
            // WM_DPICHANGED never arrives at a hidden window, so the other two carry the news.
            if (TaskbarDpi() != _iconDpi)
            {
                Update();
            }
        }

        return IntPtr.Zero;
    }

    private void OnIconMessage(uint iconEvent, IntPtr anchor)
    {
        // Mouse moves arrive many times a second; the rest are rare and worth a line each.
        if (iconEvent != PInvoke.WM_MOUSEMOVE)
        {
            Log.Write($"icon event {iconEvent}");
        }

        switch (iconEvent)
        {
            case NinSelect:
                Clicked?.Invoke();
                break;
            case PInvoke.WM_MOUSEMOVE:
                MouseMoved?.Invoke();
                break;
            case PInvoke.WM_CONTEXTMENU:
                MenuRequested?.Invoke(anchor);
                break;
        }
    }
}
