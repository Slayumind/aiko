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

/// Everything Aiko shows: the icon in the tray or the island at the edge of the screen, the card,
/// the settings window and the first run wizard. One owner, because all of them show the same
/// numbers and the user swaps between the two places at will.
///
/// Windows 10 1809 is the minimum, the same as in app.manifest: asking the taskbar monitor for
/// its scaling needs it. Saying so here instead of in the project file keeps the WinRT
/// projections, and their tens of megabytes, out of the build.
[SupportedOSPlatform("windows10.0.17763")]
sealed class AikoShell : IDisposable
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

    /// Long enough that running the mouse along the taskbar does not open the card.
    private static readonly TimeSpan HoverDelay = TimeSpan.FromSeconds(1.5);

    private readonly Application _application;
    private readonly SnapshotWatcher _watcher = new();
    private readonly uint _taskbarCreatedMessage;
    private HwndSource? _source;
    private DispatcherTimer? _hover;
    private CardWindow? _card;
    private SettingsWindow? _settings;
    private WizardWindow? _wizard;
    private IslandWindow? _island;
    private FullScreenWatch? _fullScreen;
    private DirectPoller? _direct;
    private HICON _icon;
    private uint _iconDpi;
    private int _iconSize;
    private bool _iconShown;

    /// Which environment the ring shows. The click swaps it; settings will feed it later.
    private string? _ringEnvironment;

    public AikoShell(Application application)
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

        _hover = new DispatcherTimer(DispatcherPriority.Normal, _application.Dispatcher) { Interval = HoverDelay };
        _hover.Tick += OnHoverFinished;

        _watcher.Updated += OnSnapshotsChanged;

        var poller = new DirectPoller(_application.Dispatcher);
        poller.Reported += OnSnapshotsChanged;
        _direct = poller;

        ApplyPlace();
        FollowDirectMode();

        // Worth a line each: if the limits never arrive, the shell we guessed and the folders we
        // found are the first things to check, and both are invisible otherwise.
        var folders = ClaudeFolders.Find();
        Log.Write($"started, status line shell: {ShellDetect.Current()}");
        Log.Write($"found {folders.Count} claude folders, {EnvironmentScan.Pick(folders).Count} usable");

        // Nothing set up yet: this is a first run, so the wizard opens itself.
        if (!SettingsStore.LoadEnvironments().HasEnvironments)
        {
            OpenWizard();
        }
    }

    public void Dispose()
    {
        _watcher.Updated -= OnSnapshotsChanged;
        _watcher.Dispose();
        _direct?.Dispose();

        CancelHover();
        _card?.Close();
        _settings?.Close();
        _wizard?.Close();
        CloseIsland();

        RemoveIcon();
        if (!_icon.IsNull)
        {
            PInvoke.DestroyIcon(_icon);
        }
        _source?.Dispose();
    }

    /// Direct mode is switched per environment, and the settings window and the wizard both
    /// change it, so this is called again whenever either of them closes.
    private void FollowDirectMode() => _direct?.Follow(SettingsStore.LoadEnvironments());

    /// Tray or island, whichever the settings say. Called again after the settings window or the
    /// wizard closes, so a change takes effect at once.
    private void ApplyPlace()
    {
        var settings = SettingsStore.Load();

        if (settings.Place == AikoPlace.Island)
        {
            RemoveIcon();
            ShowIsland(settings.Island);
        }
        else
        {
            CloseIsland();
            AddIcon();
        }
    }

    private void ShowIsland(IslandPosition position)
    {
        if (_island is null)
        {
            var window = new IslandWindow();
            window.CardRequested += OpenCard;
            window.Moved += SaveIslandPosition;
            _island = window;
        }

        _island.Show(Cards(), position);

        // Only watched while the island is on screen: the tray icon has nothing to hide from.
        if (_fullScreen is null)
        {
            var watch = new FullScreenWatch();
            watch.Changed += OnFullScreenChanged;
            _fullScreen = watch;
        }

        OnFullScreenChanged(FullScreenWatch.IsFullScreenInFront());
    }

    private void OnFullScreenChanged(bool fullScreen) =>
        _application.Dispatcher.BeginInvoke(() =>
        {
            if (_island is not { } island)
            {
                return;
            }

            var hide = fullScreen && SettingsStore.Load().HideIslandInFullScreen;
            var wanted = hide ? Visibility.Hidden : Visibility.Visible;

            // Windows says which window came to the front on every switch, which is many times a
            // minute. Only a real change is worth a line.
            if (island.Visibility == wanted)
            {
                return;
            }

            island.Visibility = wanted;
            Log.Write($"island {(hide ? "hidden behind a full screen window" : "shown again")}");
        });

    private void CloseIsland()
    {
        _fullScreen?.Dispose();
        _fullScreen = null;

        _island?.Close();
        _island = null;
    }

    private void SaveIslandPosition(IslandPosition position)
    {
        SettingsStore.Save(SettingsStore.Load() with { Island = position });
        Log.Write($"island moved to {position.Edge} at {position.Along:0.00}");
    }

    private void OnSnapshotsChanged() =>
        _application.Dispatcher.BeginInvoke(() =>
        {
            var cards = Cards();
            UpdateIcon();
            _island?.Update(cards);
            _card?.Update(CurrentCard());
        });

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

        _iconShown = true;
    }

    private void RemoveIcon()
    {
        if (!_iconShown)
        {
            return;
        }

        var data = NewData();
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in data);
        _iconShown = false;
    }

    private void UpdateIcon()
    {
        if (!_iconShown)
        {
            return;
        }

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

    /// Every environment we know about, in the order the settings hold them. The card shows all of
    /// them, including the ones with nothing reported yet: an empty block says so in words.
    private IReadOnlyList<CardState> Cards()
    {
        var now = DateTimeOffset.Now;

        // Read every time: the settings window can rename an environment while Aiko runs, and the
        // file is small.
        var environments = SettingsStore.LoadEnvironments();

        var direct = _direct?.Latest;

        return EnvironmentSnapshots.Combine(environments, _watcher.ByFile)
            // Two sources for one environment: the fresher answer wins. Direct mode is asked
            // rarely, the status line speaks on every reply, so neither is always ahead.
            .Select(snapshot => direct is not null && direct.TryGetValue(snapshot.Environment, out var fromApi)
                ? LimitSnapshot.Newer(snapshot, fromApi)
                : snapshot)
            .Select(snapshot => CardState.From(snapshot, now))
            .ToList();
    }

    private CardModel CurrentCard() => CardModel.From(Cards(), DateTimeOffset.Now);

    /// The ring shows one environment and the dot the other. With nothing reported yet both are
    /// empty and the icon draws the dashed ring.
    private (CardRow? Ring, CardRow? Dot) CurrentRows()
    {
        var cards = Cards().Where(card => card.HasData).ToList();
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
        // Environments as the user named them, not the folders behind them.
        var others = Cards()
            .Select(card => card.Environment)
            .Where(name => !string.Equals(name, _ringEnvironment, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (others.Count == 0)
        {
            return;
        }

        _ringEnvironment = others[0];
        UpdateIcon();
    }

    private void StartHover()
    {
        // Already counting, or the card is up: nothing to start.
        if (_card is not null || _hover is null || _hover.IsEnabled)
        {
            return;
        }
        Log.Write("hover started");
        _hover.Start();
    }

    private void CancelHover() => _hover?.Stop();

    private void OnHoverFinished(object? sender, EventArgs e)
    {
        CancelHover();

        // Windows does not promise to say when the mouse left the icon, so the pointer is checked
        // here. Without this the card would appear long after the user walked away.
        var over = CursorOverIcon();
        Log.Write($"hover finished, cursor over icon: {over}, icon at {IconRect()}");
        if (over)
        {
            OpenCard();
        }
    }

    private void OpenCard()
    {
        if (_card is not null)
        {
            _card.Update(CurrentCard());
            return;
        }

        var card = new CardWindow();
        card.SettingsRequested += OpenSettings;
        card.Closed += (_, _) => _card = null;
        card.Update(CurrentCard());
        _card = card;

        if (IconRect() is { } rect)
        {
            card.ShowAt(rect);
        }
        else if (_island is { } island)
        {
            // From the island the card opens right under it.
            card.ShowAt(new Rect(island.Left, island.Top, island.ActualWidth, island.ActualHeight));
        }
        else
        {
            card.Show();
        }

        Log.Write($"card opened at {card.Left},{card.Top} size {card.ActualWidth}x{card.ActualHeight}");
    }

    private void OpenWizard()
    {
        if (_wizard is not null)
        {
            _wizard.Activate();
            return;
        }

        var window = new WizardWindow();
        window.Closed += (_, _) =>
        {
            _wizard = null;
            ApplyPlace();
            FollowDirectMode();
            UpdateIcon();
            _card?.Update(CurrentCard());
        };
        _wizard = window;
        window.Show();

        Log.Write("wizard opened");
    }

    /// One settings window at a time: asking twice brings the open one forward instead of
    /// stacking a second copy on top of it.
    private void OpenSettings()
    {
        if (_settings is not null)
        {
            _settings.Activate();
            return;
        }

        var window = new SettingsWindow();
        window.QuitRequested += () => _application.Shutdown();
        window.Closed += (_, _) =>
        {
            _settings = null;
            // The place and direct mode may both have changed while the window was open.
            ApplyPlace();
            FollowDirectMode();
        };
        _settings = window;
        window.Show();

        Log.Write("settings opened");
    }

    /// Where Windows put our icon, in real pixels. It answers even when the icon sits in the
    /// overflow area, which is where Windows 11 puts a new app by default.
    private Rect? IconRect()
    {
        if (!_iconShown)
        {
            return null;
        }

        var id = new NOTIFYICONIDENTIFIER
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
            hWnd = Hwnd,
            uID = IconId,
        };

        return PInvoke.Shell_NotifyIconGetRect(in id, out var rect).Succeeded
            ? new Rect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top)
            : null;
    }

    private bool CursorOverIcon()
    {
        if (IconRect() is not { } rect || !PInvoke.GetCursorPos(out var point))
        {
            return false;
        }
        return rect.Contains(point.X, point.Y);
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
            if (_iconShown)
            {
                _iconShown = false;
                AddIcon();
            }
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
        // Mouse moves arrive many times a second; the rest are rare and worth a line each.
        if (iconEvent != PInvoke.WM_MOUSEMOVE)
        {
            Log.Write($"icon event {iconEvent}");
        }

        switch (iconEvent)
        {
            case NinSelect:
                SwapRing();
                break;

            // Windows sends nothing that plainly means "the mouse left the icon". The two popup
            // messages are about its own tooltip and arrive in pairs on every move, so cancelling
            // on them killed the count every time. The wait simply runs out, and then we ask where
            // the pointer is.
            case PInvoke.WM_MOUSEMOVE:
                StartHover();
                break;
            case PInvoke.WM_CONTEXTMENU:
                CancelHover();
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
            case MenuSettings:
                OpenSettings();
                break;
            case MenuRefresh:
                UpdateIcon();
                _island?.Update(Cards());
                _card?.Update(CurrentCard());
                break;
            case MenuExit:
                _application.Shutdown();
                break;
        }
    }
}
