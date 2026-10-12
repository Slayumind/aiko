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
    /// Long enough that running the mouse along the taskbar does not open the card.
    private static readonly TimeSpan HoverDelay = TimeSpan.FromSeconds(1.5);

    private readonly Application _application;
    private readonly SnapshotWatcher _watcher = new();
    private readonly ShellNumbers _numbers;
    private readonly TrayIcon _tray;
    private readonly IslandHost _island;

    /// The face for two seconds after an event (D-211). Drawn by the icon and the island later.
    private TrayMoodPlayer? _mood;
    private DispatcherTimer? _hover;
    private CardWindow? _card;
    private SettingsWindow? _settings;
    private DirectPoller? _direct;
    private UpdateWatch? _updates;
    private DispatcherTimer? _cardWatch;
    private int _awayTurns;

    /// Set by --try-shell. A check draws the real shell and must leave the machine as it found it.
    private bool _changingNothing;

    public AikoShell(Application application)
    {
        _application = application;
        _numbers = new ShellNumbers(_watcher);
        _tray = new TrayIcon(
            application.Dispatcher,
            _numbers.IconRows,
            () => TrayText.Tooltip(_numbers.Cards(), _updates?.NewerVersion));
        _tray.Clicked += OnIconClicked;
        _tray.MouseMoved += StartHover;
        _tray.MenuRequested += OnMenuRequested;
        _island = new IslandHost(application.Dispatcher);
        _island.CardRequested += pinned => OpenCard(pinned);
    }

    /// With changingNothing the start skips everything that writes or asks the network: the repair
    /// of the status lines, the pages that open once and note that they did, the plugin sync, the
    /// commands in PATH, direct mode, the update check and the clean-up of old activity files.
    public void Show(bool changingNothing = false)
    {
        _changingNothing = changingNothing;
        _island.ChangingNothing = changingNothing;

        _tray.Open();

        _hover = new DispatcherTimer(DispatcherPriority.Normal, _application.Dispatcher) { Interval = HoverDelay };
        _hover.Tick += OnHoverFinished;

        _watcher.Updated += OnSnapshotsChanged;

        _mood = new TrayMoodPlayer(_application.Dispatcher);
        _mood.FaceChanged += OnFaceChanged;
        _mood.Follow(SettingsStore.LoadEnvironments());

        var poller = new DirectPoller(_application.Dispatcher);
        poller.Reported += OnSnapshotsChanged;
        _direct = poller;
        _numbers.Direct = poller;

        if (!changingNothing)
        {
            var updates = new UpdateWatch(_application.Dispatcher);
            updates.Found += _tray.Update;
            _updates = updates;
        }

        ApplyPlace();
        FollowDirectMode();

        // Worth a line each: if the limits never arrive, the shell we guessed and the folders we
        // found are the first things to check, and both are invisible otherwise.
        var folders = ClaudeFolders.Find();
        Log.Write($"started, status line shell: {ShellDetect.Current()}");
        Log.Write($"found {folders.Count} claude folders, {EnvironmentScan.Pick(folders).Count} usable");

        if (changingNothing)
        {
            _numbers.NoteWhereWeHaveNoAccess();
            return;
        }

        // Nothing set up yet: this is a first run, so settings open themselves on the checklist.
        if (!SettingsStore.LoadEnvironments().HasEnvironments)
        {
            OpenSettings(page: SettingsPanel.ChecklistPageKey);
        }
        else
        {
            StartupChores.RepairOurStatusLines();
            OpenThePageShownOnce();
            StartupChores.RefreshPluginsAndCommands();
        }

        _numbers.NoteWhereWeHaveNoAccess();
        StartupChores.ClearStaleActivity();
    }

    /// A page that opens by itself one time, for someone who set Aiko up before the page existed.
    private void OpenThePageShownOnce()
    {
        // Once, for someone who set Aiko up before the persona existed (D-200).
        var app = SettingsStore.Load();
        if (WizardChecklist.OpensMeetAikoOnStart(app, SettingsStore.LoadEnvironments()))
        {
            OpenSettings(page: SettingsPanel.ChecklistPageKey);
            _settings?.OpenChecklist(ChecklistItem.MeetAiko);
            app = app with { MeetAikoShown = true };
            SettingsStore.Save(app);
        }

        // Once, for someone who set Aiko up before the count had its own switch. Not asking
        // would leave them sending nothing and never learning the question existed.
        else if (WizardChecklist.OpensPrivacyOnStart(app, SettingsStore.LoadEnvironments()))
        {
            OpenSettings(page: SettingsPanel.ChecklistPageKey);
            _settings?.OpenChecklist(ChecklistItem.Privacy);
        }

        // Once, for someone who turned Aiko on before she had a memory. The page says what the
        // memory reads and writes, and it is where she can be turned off.
        else if (WizardChecklist.OpensMemoryOnStart(app, SettingsStore.LoadEnvironments()))
        {
            OpenSettings(page: SettingsPanel.PersonalityPageKey);
            SettingsStore.Save(app with { MemoryShown = true });
        }
    }

    // ---- doors for --try-shell: the check drives the shell through these and nothing else ----

    /// The tray icon or the island, whatever the settings say, and nothing is saved.
    public void ShowPlaceForCheck(AikoPlace place) => ApplyPlace(place);

    public void OpenCardForCheck(bool pinned) => OpenCard(pinned);

    public CardWindow? CardForCheck => _card;

    public IslandWindow? IslandForCheck => _island.Window;

    public SettingsWindow? SettingsForCheck => _settings;

    public Rect? IconRectForCheck => _tray.Rect;

    public bool FaceOnIconForCheck => _tray.HasFace;

    /// Whether the pointer is over the icon, the island or the card: the places an open card stays for.
    public bool MouseAtHomeForCheck =>
        _tray.CursorOver || _island.CursorOver || (_card is { } card && Pointer.IsOver(card));

    public int BlocksOnCardForCheck => CurrentCard().Blocks.Count;

    /// One face, without waiting for a session to do anything.
    public void ShowFaceForCheck(AikoFace? face) => OnFaceChanged(face);

    public void OpenSettingsForCheck() => OpenSettings();

    /// The wait before the card, as a mouse over the icon starts it.
    public void StartHoverForCheck() => StartHover();

    public void Dispose()
    {
        _watcher.Updated -= OnSnapshotsChanged;
        _watcher.Dispose();
        _mood?.Dispose();
        _direct?.Dispose();
        _updates?.Dispose();

        CancelHover();
        _card?.Close();
        _settings?.Close();
        _island.Close();

        _tray.Dispose();
    }

    /// Direct mode is switched per environment, and the settings window and the wizard both
    /// change it, so this is called again whenever either of them closes.
    private void FollowDirectMode()
    {
        // Direct mode reads the token and asks the server. A check has no business doing either.
        if (!_changingNothing)
        {
            _direct?.Follow(SettingsStore.LoadEnvironments());
        }
    }

    /// Tray or island, whichever the settings say. Called again after the settings window or the
    /// wizard closes, so a change takes effect at once.
    private void ApplyPlace(AikoPlace? forced = null)
    {
        var settings = SettingsStore.Load();

        if ((forced ?? settings.Place) == AikoPlace.Island)
        {
            _tray.Remove();
            _island.Show(_numbers.Cards(), settings.Island);
        }
        else
        {
            _island.Close();
            _tray.Add();
        }
    }

    private void OnSnapshotsChanged() =>
        _application.Dispatcher.BeginInvoke(() =>
        {
            _mood?.OnLimits(EnvironmentSnapshots.Combine(SettingsStore.LoadEnvironments(), _watcher.ByFile));
            var cards = _numbers.Cards();
            _tray.Update();
            _island.Update(cards);
            _card?.Update(CurrentCard());
        });

    /// A face comes or goes (D-211), on the tray icon or on the island, wherever Aiko lives.
    private void OnFaceChanged(AikoFace? face)
    {
        var style = SettingsStore.LoadPersona().Face;

        _island.ShowFace(face, style);
        _tray.ShowFace(face, style);
    }

    private CardModel CurrentCard()
    {
        var now = DateTimeOffset.Now;
        var cards = _numbers.Cards();
        WakeWhenWorkingEnds(cards, now);
        return _numbers.CardFor(cards, now);
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

    /// The card's button: Claude Code in a window of its own, for this environment.
    private static void OpenClaude(string environment)
    {
        if (SettingsStore.LoadEnvironments().Environments.FirstOrDefault(e => e.Name == environment) is { } found)
        {
            ClaudeLauncher.Open(found.ConfigDirectories[0], Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }
    }

    /// A left click on a tray icon opens the thing. Swapping the ring instead left the click with
    /// no visible answer beyond two colours trading places, which reads as a glitch, and hid the
    /// card behind a gesture nobody is told about.
    private void OnIconClicked()
    {
        CancelHover();
        OpenCard(pinned: true);
    }

    private void OnMenuRequested(IntPtr anchor)
    {
        CancelHover();
        ShowMenu(anchor);
    }

    /// Windows sends nothing that plainly means "the mouse left the icon". The two popup messages
    /// are about its own tooltip and arrive in pairs on every move, so cancelling on them killed
    /// the count every time. The wait simply runs out, and then we ask where the pointer is.
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
            if (_tray.CursorOver || Pointer.IsOver(card) || _island.CursorOver)
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

    private void OnHoverFinished(object? sender, EventArgs e)
    {
        CancelHover();

        // Windows does not promise to say when the mouse left the icon, so the pointer is checked
        // here. Without this the card would appear long after the user walked away.
        var over = _tray.CursorOver;
        Log.Write($"hover finished, cursor over icon: {over}, icon at {_tray.Rect}");
        if (over)
        {
            OpenCard();
        }
    }

    private void OpenCard() => OpenCard(pinned: false);

    private void OpenCard(bool pinned)
    {
        _numbers.ReadAccounts();
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

        if (_tray.Rect is { } rect)
        {
            card.ShowAt(rect);
        }
        else if (_island.Bounds is { } island)
        {
            // From the island the card opens right under it.
            card.ShowAt(island);
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
        if (island != _island.IsShown)
        {
            ApplyPlace();
        }
        else if (island)
        {
            _island.FollowFullScreen();
        }

        FollowDirectMode();
        _numbers.NoteWhereWeHaveNoAccess();
        _mood?.Follow(SettingsStore.LoadEnvironments());
        _tray.Update();
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

    private void ShowMenu(IntPtr anchor)
    {
        switch (TrayMenu.Show(_tray.Window, anchor, ShellNumbers.OtherEnvironment()))
        {
            case TrayCommand.SwapRing:
                if (ShellNumbers.SwapRing())
                {
                    _tray.Update();
                }

                break;
            case TrayCommand.Settings:
                OpenSettings();
                break;
            case TrayCommand.Refresh:
                _tray.Update();
                _island.Update(_numbers.Cards());
                _card?.Update(CurrentCard());
                break;
            case TrayCommand.Updates:
                // The menu had this item from the start and nothing was listening for it: the item
                // was drawn, clicked and ignored.
                OpenSettings(checkUpdates: true);
                break;
            case TrayCommand.Exit:
                _application.Shutdown();
                break;
        }
    }
}
