using System.Runtime.Versioning;
using System.Windows;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The one owner of everything Aiko shows: the icon in the tray or the island at the edge of the
/// screen, the card, the settings window and the first run wizard. All of them show the same
/// numbers and the user swaps between the two places at will, so one class decides who is told
/// what and when.
///
/// The parts do the work: TrayIcon and TrayMenu, IslandHost, ShellNumbers for what the
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

    /// The face for two seconds after an event (D-211). Drawn by the icon and the island later.
    private TrayMoodPlayer? _mood;
    private FirstRunHost? _firstRun;
    private TerminalHost? _terminal;

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
        // The terminal window is the main window; the tray and the island are how it waits while it
        // is closed (D-372). A click on either opens it, a click on "waits" opens that session.
        _island.Clicked += OpenTerminal;
        _island.SessionRequested += id =>
        {
            OpenTerminal();
            _terminal?.OpenSession(id);
        };
    }

    /// With changingNothing the start skips everything that writes or asks the network: the repair
    /// of the status lines, the pages that open once and note that they did, the plugin sync, the
    /// commands in PATH, direct mode, the update check and the clean-up of old activity files.
    /// withWindows: Windows started Aiko at sign-in (Startup.Argument).
    public void Show(bool changingNothing = false, bool withWindows = false)
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
            updates.Found += () => _terminal?.ShowUpdate(updates.NewerVersion is not null);
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
            Startup.Refresh();

            if (!ShowTerminalOnce() && withWindows && SettingsStore.Load().OpenTerminalAtStartup)
            {
                OpenTerminal();
            }
        }

        _numbers.NoteWhereWeHaveNoAccess();
        StartupChores.ClearStaleActivity();
    }

    /// The terminal window opens by itself once, when Aiko is set up (D-350): at the start, or when
    /// the settings of the first run close. Says whether it opened now.
    private bool ShowTerminalOnce()
    {
        var app = SettingsStore.Load();
        if (app.TerminalShown || !SettingsStore.LoadEnvironments().HasEnvironments)
        {
            return false;
        }

        SettingsStore.Change(settings => settings with { TerminalShown = true });
        OpenTerminal();
        return true;
    }

    /// A page that opens by itself one time, for someone who set Aiko up before the page existed.
    private void OpenThePageShownOnce()
    {
        // Once, for someone who set Aiko up before the persona existed (D-200).
        var app = SettingsStore.Load();
        if (WizardChecklist.OpensMeetAikoOnStart(app, SettingsStore.LoadEnvironments()))
        {
            OpenedTerminal()?.OpenChecklist(ChecklistItem.MeetAiko);
            SettingsStore.Change(settings => settings with { MeetAikoShown = true });
        }

        // Once, for someone who set Aiko up before the count had its own switch. Not asking
        // would leave them sending nothing and never learning the question existed.
        else if (WizardChecklist.OpensPrivacyOnStart(app, SettingsStore.LoadEnvironments()))
        {
            OpenedTerminal()?.OpenChecklist(ChecklistItem.Privacy);
        }

        // Once, for someone who turned Aiko on before she had a memory. The page says what the
        // memory reads and writes, and it is where she can be turned off.
        else if (WizardChecklist.OpensMemoryOnStart(app, SettingsStore.LoadEnvironments()))
        {
            OpenSettings(page: SettingsPanel.PersonalityPageKey);
            SettingsStore.Change(settings => settings with { MemoryShown = true });
        }
    }

    // ---- doors for --try-shell: the check drives the shell through these and nothing else ----

    /// The tray icon or the island, as the check asks and whatever the settings say. Nothing is saved.
    public void ShowPlaceForCheck(AikoPlace place)
    {
        _placeForCheck = place;
        ApplyPlace();
    }

    /// A left click on the tray icon, as a person makes it.
    public void ClickIconForCheck() => OnIconClicked();

    /// A click on the island, away from the mark of "waits".
    public void ClickIslandForCheck() => _island.ClickForCheck();

    /// The terminal window is open.
    public bool TerminalShownForCheck => _terminal is not null;

    public void CloseTerminalForCheck() => _terminal?.Close();

    public IslandWindow? IslandForCheck => _island.Window;

    /// The settings are open: in the terminal window, or in a window of their own before the first
    /// environment.
    public bool SettingsShownForCheck => _terminal is { SettingsOpen: true } || _firstRun is { IsVisible: true };

    /// The first run, which stands in the terminal window before the first environment.
    public bool FirstRunForCheck => _firstRun is not null;

    /// The settings go back to the session, and the terminal window closes: while it is open the
    /// island stays away (D-310), and the next steps of the check need it.
    public void CloseSettingsForCheck()
    {
        _firstRun?.Close();
        _terminal?.CloseSettings();
        _terminal?.Close();
    }

    public Rect? IconRectForCheck => _tray.Rect;

    public bool FaceOnIconForCheck => _tray.HasFace;

    /// One face, without waiting for a session to do anything.
    public void ShowFaceForCheck(AikoFace? face) => OnFaceChanged(face);

    public void OpenSettingsForCheck() => OpenSettings();

    /// The menu as a right click opens it. The call returns when the menu closes.
    public void OpenMenuForCheck() => OnMenuRequested(IntPtr.Zero);

    public void PressInMenuForCheck(uint key) => _tray.PostForCheck(Windows.Win32.PInvoke.WM_KEYDOWN, key);

    public void CancelMenuForCheck() => _tray.PostForCheck(Windows.Win32.PInvoke.WM_CANCELMODE, 0);

    /// After an error that repeats (CrashGuard): the terminal and the settings close, the tray and
    /// the island stay. The sessions of the terminal live on without its window (D-351).
    public void CloseWindows()
    {
        _firstRun?.Close();
        _terminal?.Close();
    }

    public void Dispose()
    {
        _watcher.Updated -= OnSnapshotsChanged;
        _watcher.Dispose();
        _mood?.Dispose();
        _direct?.Dispose();
        _updates?.Dispose();

        _firstRun?.Close();
        _terminal?.Close();
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
            var snapshots = EnvironmentSnapshots.Combine(SettingsStore.LoadEnvironments(), _watcher.ByFile);
            _mood?.OnLimits(snapshots);
            var cards = _numbers.Cards();
            RedrawIcons();
            _island.Update(cards);
            _terminal?.LimitsChanged();
        });

    /// A face comes or goes (D-211), on the tray icon or on the island, wherever Aiko lives, and on
    /// the taskbar button of the open window (D-409).
    private void OnFaceChanged(AikoFace? face)
    {
        var style = SettingsStore.LoadPersona().Face;

        _island.ShowFace(face, style);
        _tray.ShowFace(face, style);
        _terminal?.Button.ShowFace(face, style);
    }

    /// The tray icon and the taskbar button draw the same picture (D-409).
    private void RedrawIcons()
    {
        _tray.Update();
        _terminal?.Button.Update();
    }

    /// The counts of the sessions on the island and the tray (D-409).
    private void ShowSessions(SessionCounts counts)
    {
        _island.ShowSessions(counts);
        _tray.ShowBadge(counts.Badge);
    }

    /// The sound of a session that starts to wait. A check changes nothing for the person, so it
    /// plays none.
    private void PlayWaiting()
    {
        if (!_changingNothing)
        {
            NoticeSound.Play();
        }
    }

    /// A left click on the tray icon opens the terminal window or brings it forward (D-372). The
    /// limits are in the tooltip of the icon and in the header of the window.
    private void OnIconClicked() => OpenTerminal();

    private void OnMenuRequested(IntPtr anchor)
    {
        switch (TrayMenu.Show(_tray.Window, anchor, ShellNumbers.OtherEnvironment()))
        {
            case TrayCommand.SwapRing:
                // The swap is saved, and a check saves nothing.
                if (!_changingNothing && ShellNumbers.SwapRing())
                {
                    RedrawIcons();
                }

                break;
            case TrayCommand.Open:
                OpenTerminal();
                break;
            case TrayCommand.Settings:
                OpenSettings();
                break;
            case TrayCommand.Exit:
                Quit();
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
        _terminal?.Follow(SettingsStore.LoadEnvironments());
        _numbers.NoteWhereWeHaveNoAccess();
        FollowMoods();
        RedrawIcons();
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

    /// Aiko quits from the tray or the settings. While programs run in the terminal the window asks
    /// first (D-351); a quit for an update or the end of Windows does not come through here.
    private void Quit()
    {
        if (_terminal is { AnyRunning: true } terminal)
        {
            terminal.AskToQuit(_application.Shutdown);
            return;
        }

        _application.Shutdown();
    }

    /// One terminal window. Closing it hides it while a program runs in it (D-351); the sessions
    /// come back from terminal.json the next time Aiko starts. Null while there is no environment
    /// yet: then the first run checklist opens instead.
    private void OpenTerminal() => OpenedTerminal();

    private TerminalHost? OpenedTerminal()
    {
        if (_terminal is not null)
        {
            _terminal.Activate();
            return _terminal;
        }

        // A terminal has tabs of environments; with none yet, the first run checklist makes one.
        var environments = SettingsStore.LoadEnvironments();
        if (environments.Environments.Count == 0)
        {
            OpenFirstRun();
            return null;
        }

        // The open header shows the plan of each environment, as the card does. A check opens the
        // window on no sessions, so no program starts and terminal.json stays as it is.
        _numbers.ReadAccounts();
        var sessions = _changingNothing ? TerminalSessions.Empty : SettingsStore.LoadTerminal();
        var terminal = new TerminalHost(environments, sessions, _numbers.SnapshotFor, () => _numbers.CardFor(_numbers.Cards(), DateTimeOffset.Now), _numbers.IconRows, keepsNothing: _changingNothing);
        terminal.Closed += () =>
        {
            _terminal = null;
            ShowSessions(SessionCounts.None);
            // "Start over" took every environment away: the window closed, and the first run comes.
            if (!SettingsStore.LoadEnvironments().HasEnvironments)
            {
                OpenFirstRun();
            }
        };
        terminal.OnTaskbarChanged += _island.FollowTerminal;
        terminal.WaitingStarted += PlayWaiting;
        terminal.SessionsChanged += ShowSessions;
        // Changes apply at once (D-179): the tray or the island, direct mode and our access follow
        // each one while the settings are still open.
        terminal.SettingsChanged += FollowSettings;
        terminal.SettingsClosed += FollowSettings;
        terminal.QuitAsked += Quit;
        terminal.ShowUpdate(_updates?.NewerVersion is not null);
        _terminal = terminal;
        terminal.Show();

        Log.Write("terminal opened");
        return terminal;
    }

    /// The settings open in the terminal window, on the place of the list and the terminal
    /// (D-374). Asked again, the open settings move to the page asked for.
    private void OpenSettings(string? page = null)
    {
        if (OpenedTerminal() is not { } terminal)
        {
            return;
        }

        terminal.OpenSettings(page);
        Log.Write("settings opened");
    }

    /// Before the first environment there is no terminal yet: the first run opens in the terminal
    /// window with the checklist alone (D-394), and Finish hands the window to the terminal.
    private void OpenFirstRun()
    {
        if (_firstRun is not null)
        {
            _firstRun.Activate();
            return;
        }

        var firstRun = new FirstRunHost();
        firstRun.QuitAsked += Quit;
        firstRun.SettingsChanged += FollowSettings;
        firstRun.Finished += () =>
        {
            firstRun.Close();
            FollowSettings();
            if (!_changingNothing && !ShowTerminalOnce())
            {
                OpenTerminal();
            }
        };
        firstRun.Closed += () =>
        {
            _firstRun = null;
            FollowSettings();
        };
        _firstRun = firstRun;
        firstRun.Show();

        Log.Write("first run opened");
    }
}
