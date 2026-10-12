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

    /// The island asks for the card: true for a click, which pins it.
    public event Action<bool>? CardRequested;

    /// Set by a check, which drags the island about. The settings of the person are not its to change.
    public bool ChangingNothing { get; set; }

    public bool IsShown => _window is not null;

    public IslandWindow? Window => _window;

    public bool CursorOver => _window is { IsVisible: true } window && Pointer.IsOver(window);

    /// Where the island is, for a card that opens right under it. Null while it is not shown.
    public Rect? Bounds =>
        _window is { } window ? new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight) : null;

    public void Show(IReadOnlyList<CardState> cards, IslandPosition position)
    {
        if (_window is null)
        {
            var window = new IslandWindow();
            window.CardRequested += pinned => CardRequested?.Invoke(pinned);
            window.Moved += SavePosition;
            _window = window;
        }

        _window.Show(cards, position);

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

    /// Hides or shows the island for what is in front right now. Called again when the setting
    /// that asks for the hiding changes.
    public void FollowFullScreen() => OnFullScreenChanged(FullScreenWatch.IsFullScreenInFront());

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
            if (_window is not { } island)
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

    private void SavePosition(IslandPosition position)
    {
        if (ChangingNothing)
        {
            Log.Write($"island would be saved at {position.Edge} {position.Along:0.00}");
            return;
        }

        SettingsStore.Save(SettingsStore.Load() with { Island = position });
        Log.Write($"island moved to {position.Edge} at {position.Along:0.00}");
    }
}
