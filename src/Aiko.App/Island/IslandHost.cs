using System.Windows;
using System.Windows.Threading;
using Aiko.Core;

namespace Aiko.App;

/// The island while Aiko lives at the edge of the screen: the window itself, hiding it behind a
/// full screen window, and remembering where the person dragged it.
sealed class IslandHost(Dispatcher dispatcher)
{
    private const double FaceSize = 18;

    private IslandWindow? _window;
    private FullScreenWatch? _fullScreen;
    private bool _fullScreenInFront;
    private bool _underTerminal;

    /// A click on the island, away from the mark of "waits".
    public event Action? Clicked;

    /// For --try-shell: the click a person makes.
    public void ClickForCheck() => Clicked?.Invoke();

    /// Set by a check, which drags the island about. The settings of the person are not its to change.
    public bool ChangingNothing { get; set; }

    public bool IsShown => _window is not null;

    public IslandWindow? Window => _window;

    /// Where the island is, for a notice that comes up next to it. Null while it is not shown.
    public Rect? Bounds =>
        _window is { } window ? new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight) : null;

    public void Show(IReadOnlyList<CardState> cards, IslandPosition position)
    {
        if (_window is null)
        {
            var window = new IslandWindow();
            window.Clicked += () => Clicked?.Invoke();
            window.Moved += SavePosition;
            window.SessionRequested += id => SessionRequested?.Invoke(id);
            _window = window;
        }

        _window.Show(cards, position);
        _window.ShowSessions(_sessions);

        // Only watched while the island is on screen: the tray icon has nothing to hide from.
        if (_fullScreen is null)
        {
            var watch = new FullScreenWatch();
            watch.Changed += OnFullScreenChanged;
            _fullScreen = watch;
        }

        FollowFullScreen();
    }

    public void Close()
    {
        _fullScreen?.Dispose();
        _fullScreen = null;

        _window?.Close();
        _window = null;
    }

    public void Update(IReadOnlyList<CardState> cards) => _window?.Update(cards);

    private Aiko.Core.Terminal.SessionCounts _sessions = Aiko.Core.Terminal.SessionCounts.None;

    /// How many sessions work and wait (D-364), kept for an island that opens later.
    public void ShowSessions(Aiko.Core.Terminal.SessionCounts sessions)
    {
        _sessions = sessions;
        _window?.ShowSessions(sessions);
    }

    /// A click on the mark of "waits": the session to open.
    public event Action<string>? SessionRequested;

    /// Hides or shows the island for what is in front right now. Called again when the setting
    /// that asks for the hiding changes.
    public void FollowFullScreen() => OnFullScreenChanged(FullScreenWatch.IsFullScreenInFront());

    /// The window of the terminal has a button on the taskbar (D-409): the button shows the ring and
    /// the sign of the sessions, so the island steps aside, also while the window is minimized. Only
    /// a closed or hidden window brings it back.
    public void FollowTerminal(bool onTaskbar)
    {
        _underTerminal = onTaskbar;
        Apply();
    }

    /// A face comes onto the island or goes (D-211). The island is always dark, whatever the taskbar.
    public void ShowFace(AikoFace? face, FaceStyle style)
    {
        if (_window is not { } window)
        {
            return;
        }

        if (face is { } shown)
        {
            window.ShowFace(FaceDrawing.For(style, shown, FaceGround.Dark, FaceSize));
        }
        else
        {
            window.HideFace();
        }
    }

    private void OnFullScreenChanged(bool fullScreen) =>
        dispatcher.BeginInvoke(() =>
        {
            _fullScreenInFront = fullScreen;
            Apply();
        });

    private void Apply()
    {
        if (_window is not { } island)
        {
            return;
        }

        var behindFullScreen = _fullScreenInFront && SettingsStore.Load().HideIslandInFullScreen;
        var hide = behindFullScreen || _underTerminal;
        var wanted = hide ? Visibility.Hidden : Visibility.Visible;

        // Windows says which window came to the front on every switch, which is many times a
        // minute. Only a real change is worth a line.
        if (island.Visibility == wanted)
        {
            return;
        }

        island.Visibility = wanted;
        Log.Write($"island {(!hide ? "shown again" : _underTerminal ? "hidden while the terminal is open" : "hidden behind a full screen window")}");
    }

    private void SavePosition(IslandPosition position)
    {
        if (ChangingNothing)
        {
            Log.Write($"island would be saved at {position.Edge} {position.Along:0.00}");
            return;
        }

        SettingsStore.Change(settings => settings with { Island = position });
        Log.Write($"island moved to {position.Edge} at {position.Along:0.00}");
    }
}
