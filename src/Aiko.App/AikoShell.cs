using System.Runtime.Versioning;
using System.Windows;
using Aiko.Core;

namespace Aiko.App;

/// The one owner of everything Aiko shows: the icon in the tray or the island at the edge of the
/// screen, the card, the settings window and the first run wizard. All of them show the same
/// numbers and the user swaps between the two places at will, so one class decides who is told
/// what and when.
///
/// The parts do the work: TrayIcon and TrayMenu, IslandHost, CardHost, ShellNumbers for what the
/// numbers are, StartupChores for what is put right at every start. This class only connects them.
/// The Windows version below is what TrayIcon needs; the reason is written there.
[SupportedOSPlatform("windows10.0.17763")]
sealed class AikoShell : IDisposable
{
    private readonly Application _application;
    private readonly SnapshotWatcher _watcher = new();
    private readonly ShellNumbers _numbers;
    private readonly TrayIcon _tray;
    private readonly IslandHost _island;
    private readonly CardHost _cards;

    /// The face for two seconds after an event (D-211). Drawn by the icon and the island later.
    private TrayMoodPlayer? _mood;
    private SettingsWindow? _settings;
    private DirectPoller? _direct;
    private UpdateWatch? _updates;

    /// Set by --try-shell. A check draws the real shell and must leave the machine as it found it.
    private bool _changingNothing;

    /// The place a check asked for. It holds until the check asks for another one, so a settings
    /// window that closes in the middle does not put Aiko back where the settings say.
    private AikoPlace? _placeForCheck;

    public AikoShell(Application application)
    {
        _application = application;
        _numbers = new ShellNumbers(_watcher);
        _tray = new TrayIcon(
            application.Dispatcher,
            _numbers.IconRows,
            () => TrayText.Tooltip(_numbers.Cards(), _updates?.NewerVersion));
        _tray.Clicked += OnIconClicked;
        _tray.MenuRequested += OnMenuRequested;
        _island = new IslandHost(application.Dispatcher);
        _cards = new CardHost(application.Dispatcher, _numbers, _tray, _island);
        _cards.SettingsRequested += () => OpenSettings();
        _tray.MouseMoved += _cards.StartHover;
        _island.CardRequested += _cards.Open;
    }

    /// With changingNothing the start skips everything that writes or asks the network: the repair
    /// of the status lines, the pages that open once and note that they did, the plugin sync, the
    /// commands in PATH, direct mode, the update check and the clean-up of old activity files.
    public void Show(bool changingNothing = false)
    {
        _changingNothing = changingNothing;
        _island.ChangingNothing = changingNothing;

        _tray.Open();

        _watcher.Updated += OnSnapshotsChanged;

        _mood = new TrayMoodPlayer(_application.Dispatcher);
        _mood.FaceChanged += OnFaceChanged;
        FollowMoods();

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

    /// The tray icon or the island, whatever the settings say. Nothing is saved.
    public void ShowPlaceForCheck(AikoPlace place)
    {
        _placeForCheck = place;
        ApplyPlace();
    }

    public void OpenCardForCheck(bool pinned) => _cards.Open(pinned);

    public CardWindow? CardForCheck => _cards.Window;

    public IslandWindow? IslandForCheck => _island.Window;

    public SettingsWindow? SettingsForCheck => _settings;

    public Rect? IconRectForCheck => _tray.Rect;

    public bool FaceOnIconForCheck => _tray.HasFace;

    /// Whether the pointer is over the icon, the island or the card: the places an open card stays for.
    public bool MouseAtHomeForCheck => _cards.PointerAtHome;

    public int BlocksOnCardForCheck => _cards.Model().Blocks.Count;

    /// One face, without waiting for a session to do anything.
    public void ShowFaceForCheck(AikoFace? face) => OnFaceChanged(face);

    public void OpenSettingsForCheck() => OpenSettings();

    /// The wait before the card, as a mouse over the icon starts it.
    public void StartHoverForCheck() => _cards.StartHover();

    /// The menu as a right click opens it. The call returns when the menu closes.
    public void OpenMenuForCheck() => OnMenuRequested(IntPtr.Zero);

    public void PressInMenuForCheck(uint key) => _tray.PostForCheck(Windows.Win32.PInvoke.WM_KEYDOWN, key);

    public void CancelMenuForCheck() => _tray.PostForCheck(Windows.Win32.PInvoke.WM_CANCELMODE, 0);

    public void Dispose()
    {
        _watcher.Updated -= OnSnapshotsChanged;
        _watcher.Dispose();
        _mood?.Dispose();
        _direct?.Dispose();
        _updates?.Dispose();

        _cards.Dispose();
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
    private void ApplyPlace()
    {
        var settings = SettingsStore.Load();

        if ((_placeForCheck ?? settings.Place) == AikoPlace.Island)
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
            _cards.Refresh();
        });

    /// A face comes or goes (D-211), on the tray icon or on the island, wherever Aiko lives.
    private void OnFaceChanged(AikoFace? face)
    {
        var style = SettingsStore.LoadPersona().Face;

        _island.ShowFace(face, style);
        _tray.ShowFace(face, style);
    }

    /// A left click on a tray icon opens the thing. Swapping the ring instead left the click with
    /// no visible answer beyond two colours trading places, which reads as a glitch, and hid the
    /// card behind a gesture nobody is told about.
    private void OnIconClicked()
    {
        _cards.CancelHover();
        _cards.Open(pinned: true);
    }

    private void OnMenuRequested(IntPtr anchor)
    {
        _cards.CancelHover();

        switch (TrayMenu.Show(_tray.Window, anchor, ShellNumbers.OtherEnvironment()))
        {
            case TrayCommand.SwapRing:
                // The swap is saved, and a check saves nothing.
                if (!_changingNothing && ShellNumbers.SwapRing())
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
                _cards.Refresh();
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

    /// Called after every change in the settings window, so only a real move between the tray and
    /// the island takes the icon down or puts it up.
    private void FollowSettings()
    {
        var island = (_placeForCheck ?? SettingsStore.Load().Place) == AikoPlace.Island;
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
        FollowMoods();
        _tray.Update();
        _cards.Refresh();
    }

    private void FollowMoods()
    {
        // A check shows the faces it asks for. A session at work on this machine would bring its
        // own in the middle of a step.
        if (!_changingNothing)
        {
            _mood?.Follow(SettingsStore.LoadEnvironments());
        }
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
}
