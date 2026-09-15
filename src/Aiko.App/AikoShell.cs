using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
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
    private const uint MenuSwap = 5;
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
    private IslandWindow? _island;
    private FullScreenWatch? _fullScreen;
    private DirectPoller? _direct;
    private UpdateWatch? _updates;
    private DispatcherTimer? _cardWatch;
    private int _awayTurns;
    private HICON _icon;
    private uint _iconDpi;
    private int _iconSize;
    private bool _iconShown;


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

        var updates = new UpdateWatch(_application.Dispatcher);
        updates.Found += UpdateIcon;
        _updates = updates;

        ApplyPlace();
        FollowDirectMode();

        // Worth a line each: if the limits never arrive, the shell we guessed and the folders we
        // found are the first things to check, and both are invisible otherwise.
        var folders = ClaudeFolders.Find();
        Log.Write($"started, status line shell: {ShellDetect.Current()}");
        Log.Write($"found {folders.Count} claude folders, {EnvironmentScan.Pick(folders).Count} usable");

        // Nothing set up yet: this is a first run, so settings open themselves on the checklist.
        if (!SettingsStore.LoadEnvironments().HasEnvironments)
        {
            OpenSettings(page: SettingsPanel.ChecklistPageKey);
        }
        else
        {
            RepairOurStatusLines();

            // Once, for someone who set Aiko up before the persona existed (D-200).
            var app = SettingsStore.Load();
            if (WizardChecklist.OpensMeetAikoOnStart(app, SettingsStore.LoadEnvironments()))
            {
                OpenSettings(page: SettingsPanel.ChecklistPageKey);
                _settings?.OpenChecklist(ChecklistItem.MeetAiko);
                SettingsStore.Save(app with { MeetAikoShown = true });
            }

            // An update can bring a new persona text or move the marketplace. When the persona is
            // off everywhere this reads a few files and does nothing else.
            PluginSync.Request("startup");

            // An update brings a new shim. The copy in PATH is refreshed, and only when the person
            // set commands up: the folder never appears on its own.
            if (CommandFolder.IsSetUp)
            {
                CommandFolder.Sync(SettingsStore.LoadEnvironments());
            }
        }

        NoteWhereWeHaveNoAccess();
        ClearStaleActivity();
    }

    /// A session that never sent SessionEnd leaves its file behind (D-206). A day later it goes.
    private static void ClearStaleActivity()
    {
        var folder = ActivityRecord.Folder(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in new DirectoryInfo(folder).EnumerateFiles())
        {
            if (ActivityRecord.IsStale(file.LastWriteTimeUtc, DateTimeOffset.UtcNow))
            {
                try
                {
                    file.Delete();
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
    }

    /// Our own line goes stale on its own: reinstalling moves the bridge, and installing Git
    /// changes the shell the line has to be written for. A stale line runs nothing and says
    /// nothing about it, so it is put right at startup.
    ///
    /// Only lines that are already ours are touched. Somebody who answered "not now" in the wizard
    /// keeps that answer, and a settings file with no line of ours in it is not ours to write to.
    private static void RepairOurStatusLines()
    {
        if (BridgePath.Current() is not { } bridge)
        {
            return;
        }

        foreach (var environment in SettingsStore.LoadEnvironments().Environments)
        {
            foreach (var folder in environment.ConfigDirectories)
            {
                if (ClaudeSettingsFile.RepairBridge(folder, bridge).Changed)
                {
                    Log.Write($"our status line was stale in {Path.GetFileName(folder)} and was put right");
                }
            }
        }
    }

    public void Dispose()
    {
        _watcher.Updated -= OnSnapshotsChanged;
        _watcher.Dispose();
        _direct?.Dispose();
        _updates?.Dispose();

        CancelHover();
        _card?.Close();
        _settings?.Close();
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
            window.CardRequested += pinned => OpenCard(pinned);
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
        WriteTooltip(ref data);
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
        data.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP;
        data.hIcon = _icon;
        WriteTooltip(ref data);
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
        var text = TrayText.Tooltip(Cards(), _updates?.NewerVersion);
        var room = data.szTip.AsSpan();
        room.Clear();
        text.AsSpan(0, Math.Min(text.Length, room.Length - 1)).CopyTo(room);
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
            // rarely, the status line speaks on every reply, so neither is always ahead. Whether a
            // session is working comes from the status line alone, whichever answer wins.
            .Select(snapshot => direct is not null && direct.TryGetValue(snapshot.Environment, out var fromApi)
                ? CardState.From(LimitSnapshot.Newer(snapshot, fromApi), now) with { WorkingUntil = CardState.WorkingUntilFor(snapshot) }
                : CardState.From(snapshot, now))
            .ToList();
    }

    private CardModel CurrentCard()
    {
        var now = DateTimeOffset.Now;
        var cards = Cards();
        WakeWhenWorkingEnds(cards, now);
        return CardModel.From(cards, now, _noAccess, _accounts);
    }

    /// "working now" has to go away by itself when a session goes quiet, and nothing else changes
    /// then. One timer for the moment the first such mark runs out, only while the card is open.
    private DispatcherTimer? _workingEnds;

    private void WakeWhenWorkingEnds(IReadOnlyList<CardState> cards, DateTimeOffset now)
    {
        _workingEnds?.Stop();
        var next = cards.Where(card => card.IsWorkingAt(now)).Min(card => card.WorkingUntil);
        if (_card is null || next is not { } until)
        {
            return;
        }

        _workingEnds ??= NewWorkingEndsTimer();
        _workingEnds.Interval = until - now + TimeSpan.FromSeconds(1);
        _workingEnds.Start();
    }

    private DispatcherTimer NewWorkingEndsTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, _application.Dispatcher);
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _card?.Update(CurrentCard());
        };
        return timer;
    }

    /// Plan and sign-in per environment. Read when the card opens, not on every new number:
    /// .claude.json can be large, and neither the plan nor the sign-in changes between two answers.
    private IReadOnlyDictionary<string, CardAccount> _accounts = new Dictionary<string, CardAccount>();

    private static IReadOnlyDictionary<string, CardAccount> ReadAccounts() =>
        SettingsStore.LoadEnvironments().Environments.ToDictionary(
            environment => environment.Name,
            environment => new CardAccount(
                ClaudeAccounts.Read(environment.ConfigDirectories[0]).PlanLabel,
                File.Exists(ClaudeInstall.CredentialsPathIn(environment.ConfigDirectories[0]))));

    /// The card's button: Claude Code in a window of its own, for this environment.
    private static void OpenClaude(string environment)
    {
        if (SettingsStore.LoadEnvironments().Environments.FirstOrDefault(e => e.Name == environment) is { } found)
        {
            ClaudeLauncher.Open(found.ConfigDirectories[0], Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }
    }

    /// Environments where Aiko's line is not in the Claude Code settings, so no numbers can ever
    /// arrive. Worked out when the settings change rather than every time the card is drawn.
    private IReadOnlySet<string> _noAccess = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private void NoteWhereWeHaveNoAccess()
    {
        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var environment in SettingsStore.LoadEnvironments().Environments)
        {
            // Direct mode fetches the numbers itself, so the status line is not needed there.
            if (environment.DirectMode)
            {
                continue;
            }

            if (environment.ConfigDirectories.Any(folder => !ClaudeSettingsFile.HasOurLine(folder)))
            {
                missing.Add(environment.Name);
            }
        }

        _noAccess = missing;
    }

    /// The ring shows one environment and the dot the other. With nothing reported yet both are
    /// empty and the icon draws the dashed ring.
    private (CardRow? Ring, CardRow? Dot) CurrentRows()
    {
        var cards = Cards().Where(card => card.HasData).ToList();
        if (cards.Count == 0)
        {
            return (null, null);
        }

        var chosen = SettingsStore.LoadEnvironments().RingEnvironment;
        var ringCard = cards.FirstOrDefault(c => c.Environment == chosen) ?? cards[0];

        var dotCard = cards.FirstOrDefault(c => c.Environment != ringCard.Environment);
        return (ringCard.IconRow, dotCard?.IconRow);
    }

    /// The environment the ring does not show, when there is one. Null with a single environment,
    /// and then the menu leaves the item out rather than showing one that does nothing.
    private static string? OtherEnvironment() => SettingsStore.LoadEnvironments().Dot?.Name;

    /// The chosen environment is kept in the environments file, not in a field here. It is a
    /// choice the user made, and a choice that quietly goes back to the other environment on the
    /// next start is worse than no choice at all.
    private void SwapRing()
    {
        var environments = SettingsStore.LoadEnvironments();
        var swapped = environments.SwapRing();
        if (ReferenceEquals(swapped, environments))
        {
            // One environment: there is nothing to swap with.
            return;
        }

        SettingsStore.SaveEnvironments(swapped);
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

    /// An unpinned card follows the mouse out.
    ///
    /// Windows says nothing useful about the mouse leaving a tray icon, and there is a gap between
    /// the icon and the card that the pointer crosses on its way in. So the pointer is looked at
    /// instead, and it has to be away from both for two turns in a row before the card goes. The
    /// watch runs only while an unpinned card is on screen, which is a few seconds at a time.
    private void WatchForTheMouseLeaving()
    {
        if (_card is null || _card.IsPinned)
        {
            return;
        }

        _awayTurns = 0;
        _cardWatch ??= NewCardWatch();
        _cardWatch.Start();
    }

    private DispatcherTimer NewCardWatch()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, _application.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };

        timer.Tick += (_, _) =>
        {
            if (_card is not { } card || card.IsPinned)
            {
                timer.Stop();
                return;
            }

            // The island is home for the card the same way the icon is. Without it a card opened
            // from the island closed half a second later, before the mouse could reach it.
            if (CursorOverIcon() || CursorOver(card) || CursorOverIsland())
            {
                _awayTurns = 0;
                return;
            }

            if (++_awayTurns < 2)
            {
                return;
            }

            timer.Stop();
            card.FadeAndClose();
        };

        return timer;
    }

    private bool CursorOverIsland() => _island is { IsVisible: true } island && CursorOver(island);

    private static bool CursorOver(Window window)
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

    private void OpenCard() => OpenCard(pinned: false);

    private void OpenCard(bool pinned)
    {
        _accounts = ReadAccounts();
        if (_card is { IsClosing: false })
        {
            // A second click on a card that is already pinned closes it, the way it opened.
            if (pinned && _card.IsPinned)
            {
                _card.FadeAndClose();
                return;
            }

            if (pinned)
            {
                _card.Pin();
            }
            _card.Update(CurrentCard());
            return;
        }

        var card = new CardWindow();
        card.SettingsRequested += () => OpenSettings();
        card.OpenClaudeRequested += OpenClaude;
        card.Closed += (_, _) =>
        {
            // A card still fading out may close after a new one has opened.
            if (_card != card)
            {
                return;
            }

            _card = null;
            _cardWatch?.Stop();
            _workingEnds?.Stop();
        };
        _card = card;
        card.Update(CurrentCard());
        if (pinned)
        {
            card.Pin();
        }
        WatchForTheMouseLeaving();

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

    /// Called after every change in the settings window, so only a real move between the tray and
    /// the island takes the icon down or puts it up.
    private void FollowSettings()
    {
        var island = SettingsStore.Load().Place == AikoPlace.Island;
        if (island != (_island is not null))
        {
            ApplyPlace();
        }
        else if (island)
        {
            OnFullScreenChanged(FullScreenWatch.IsFullScreenInFront());
        }

        FollowDirectMode();
        NoteWhereWeHaveNoAccess();
        UpdateIcon();
        _card?.Update(CurrentCard());
    }

    /// One settings window at a time: asking twice brings the open one forward instead of
    /// stacking a second copy on top of it.
    private void OpenSettings(bool checkUpdates = false, string? page = null)
    {
        if (_settings is not null)
        {
            _settings.Activate();
            if (checkUpdates)
            {
                _settings.CheckUpdatesNow();
            }
            return;
        }

        var window = new SettingsWindow(page);
        window.QuitRequested += () => _application.Shutdown();

        // Changes apply at once (D-179): the tray or the island, direct mode and our access follow
        // each one while the window is still open.
        window.SettingsChanged += FollowSettings;
        window.Closed += (_, _) =>
        {
            var again = window.ReopenPage;
            _settings = null;
            FollowSettings();

            if (again is not null)
            {
                OpenSettings(page: again);
            }
        };
        _settings = window;
        window.Show();
        if (checkUpdates)
        {
            window.CheckUpdatesNow();
        }

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
            // A left click on a tray icon opens the thing. Swapping the ring instead left the
            // click with no visible answer beyond two colours trading places, which reads as a
            // glitch, and hid the card behind a gesture nobody is told about.
            case NinSelect:
                CancelHover();
                OpenCard(pinned: true);
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

        // The click on the icon opens the card now, so swapping the ring needs a home. Here it
        // says which environment it would show, which the click never did.
        if (OtherEnvironment() is { } other)
        {
            fixed (char* swap = string.Format(Strings.MenuShowInRing, other))
            {
                PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, MenuSwap, swap);
            }
            PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
        }

        fixed (char* settings = Strings.Settings,
                     refresh = Strings.MenuRefresh,
                     updates = Strings.CheckForUpdates,
                     exit = Strings.QuitAiko)
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
            case MenuSwap:
                SwapRing();
                break;
            case MenuSettings:
                OpenSettings();
                break;
            case MenuRefresh:
                UpdateIcon();
                _island?.Update(Cards());
                _card?.Update(CurrentCard());
                break;
            case MenuUpdates:
                // The menu had this item from the start and nothing was listening for it: the item
                // was drawn, clicked and ignored.
                OpenSettings(checkUpdates: true);
                break;
            case MenuExit:
                _application.Shutdown();
                break;
        }
    }
}
