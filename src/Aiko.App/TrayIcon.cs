using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;
using Aiko.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Aiko.App;

/// The icon in the tray and everything Windows tells it: hovering, clicking, the taskbar
/// restarting, the screen scaling changing.
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
    private const uint MenuSettings = 1;
    private const uint MenuRefresh = 2;
    private const uint MenuUpdates = 3;
    private const uint MenuExit = 4;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmNoNotify = 0x0080;
    private const uint TpmReturnCommand = 0x0100;

    private readonly Application _application;
    private readonly SnapshotWatcher _watcher = new();
    private readonly uint _taskbarCreatedMessage;
    private HwndSource? _source;
    private HICON _icon;
    private uint _iconDpi;
    private int _iconSize;

    /// Which environment the ring shows. The click swaps it; settings will feed it later.
    private string? _ringEnvironment;

    public TrayIcon(Application application)
    {
        _application = application;
        _taskbarCreatedMessage = PInvoke.RegisterWindowMessage("TaskbarCreated");
    }

    private HWND Hwnd => (HWND)_source!.Handle;

    public void Show()
    {
        // A hidden top level window, not a message only one: broadcasts such as TaskbarCreated
        // never reach message only windows (spike 2026-09-11).
        _source = new HwndSource(new HwndSourceParameters("AikoTray") { Width = 0, Height = 0, WindowStyle = 0 });
        _source.AddHook(WndProc);

        _watcher.Updated += OnSnapshotsChanged;
        AddIcon();
    }

    public void Dispose()
    {
        _watcher.Updated -= OnSnapshotsChanged;
        _watcher.Dispose();

        var data = NewData();
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in data);
        if (!_icon.IsNull)
        {
            PInvoke.DestroyIcon(_icon);
        }
        _source?.Dispose();
    }

    private void OnSnapshotsChanged() =>
        _application.Dispatcher.BeginInvoke(UpdateIcon);

    private void AddIcon()
    {
        ReplaceIcon(TaskbarDpi());

        var data = NewData();
        data.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP;
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = _icon;
        "Aiko".AsSpan().CopyTo(data.szTip.AsSpan());
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, in data);

        data.Anonymous.uVersion = PInvoke.NOTIFYICON_VERSION_4;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, in data);
    }

    private void UpdateIcon()
    {
        var old = ReplaceIcon(TaskbarDpi());
        var data = NewData();
        data.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_ICON;
        data.hIcon = _icon;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in data);

        if (!old.IsNull)
        {
            PInvoke.DestroyIcon(old);
        }
    }

    private HICON ReplaceIcon(uint dpi)
    {
        var old = _icon;
        _iconSize = PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, dpi);
        _iconDpi = dpi;

        var (ring, dot) = CurrentRows();
        _icon = RingIcon.Render(_iconSize, ring, dot);
        return old;
    }

    /// The ring shows one environment and the dot the other. With nothing reported yet both are
    /// empty and the icon draws the dashed ring.
    private (CardRow? Ring, CardRow? Dot) CurrentRows()
    {
        var now = DateTimeOffset.Now;
        var cards = _watcher.Snapshots
            .Select(snapshot => CardState.From(snapshot, now))
            .Where(card => card.HasData)
            .OrderBy(card => card.Environment, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cards.Count == 0)
        {
            return (null, null);
        }

        var ringCard = cards.FirstOrDefault(c => c.Environment == _ringEnvironment) ?? cards[0];
        _ringEnvironment = ringCard.Environment;

        var dotCard = cards.FirstOrDefault(c => c.Environment != ringCard.Environment);
        return (ringCard.IconRow, dotCard?.IconRow);
    }

    private void SwapRing()
    {
        var others = _watcher.Snapshots
            .Select(s => s.Environment)
            .Where(name => !string.Equals(name, _ringEnvironment, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (others.Count == 0)
        {
            return;
        }

        _ringEnvironment = others[0];
        UpdateIcon();
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
        hWnd = Hwnd,
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
            AddIcon();
        }
        else if (message is PInvoke.WM_DPICHANGED or PInvoke.WM_DISPLAYCHANGE or PInvoke.WM_SETTINGCHANGE)
        {
            // WM_DPICHANGED never arrives at a hidden window, so the other two carry the news.
            if (TaskbarDpi() != _iconDpi)
            {
                UpdateIcon();
            }
        }
        return IntPtr.Zero;
    }

    private void OnIconMessage(uint iconEvent, IntPtr anchor)
    {
        switch (iconEvent)
        {
            case NinSelect:
                SwapRing();
                break;
            case PInvoke.WM_CONTEXTMENU:
                ShowMenu(anchor);
                break;
        }
    }

    private unsafe void ShowMenu(IntPtr anchor)
    {
        var x = (short)(anchor.ToInt64() & 0xFFFF);
        var y = (short)((anchor.ToInt64() >> 16) & 0xFFFF);

        var menu = PInvoke.CreatePopupMenu();
        fixed (char* settings = "Settings", refresh = "Refresh limits", updates = "Check for updates", exit = "Exit")
        {
            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, MenuSettings, settings);
            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, MenuRefresh, refresh);
            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, MenuUpdates, updates);
            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, MenuExit, exit);
        }

        // Without this the menu stays open after a click somewhere else.
        PInvoke.SetForegroundWindow(Hwnd);
        var command = (uint)PInvoke.TrackPopupMenuEx(
            menu, TpmReturnCommand | TpmNoNotify | TpmRightButton, x, y, Hwnd, null).Value;
        PInvoke.DestroyMenu(menu);

        switch (command)
        {
            case MenuRefresh:
                UpdateIcon();
                break;
            case MenuExit:
                _application.Shutdown();
                break;
        }
    }
}
