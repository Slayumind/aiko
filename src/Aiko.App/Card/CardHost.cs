using System.Runtime.Versioning;
using System.Windows.Threading;
using Aiko.Core;

namespace Aiko.App;

/// The life of the card: the wait before it opens under a resting mouse, a click that pins it, the
/// watch that closes an unpinned card when the mouse walks away, and the timer that takes
/// "working now" off by itself. The card opens from the tray icon or from the island, whichever is
/// on screen.
[SupportedOSPlatform("windows10.0.17763")]
sealed class CardHost : IDisposable
{
    /// Long enough that running the mouse along the taskbar does not open the card.
    private static readonly TimeSpan HoverDelay = TimeSpan.FromSeconds(1.5);

    private readonly Dispatcher _dispatcher;
    private readonly ShellNumbers _numbers;
    private readonly TrayIcon _tray;
    private readonly IslandHost _island;
    private readonly DispatcherTimer _hover;

    private CardWindow? _card;
    private DispatcherTimer? _cardWatch;
    private int _awayTurns;

    /// "working now" has to go away by itself when a session goes quiet, and nothing else changes
    /// then. One timer for the moment the first such mark runs out, only while the card is open.
    private DispatcherTimer? _workingEnds;

    public CardHost(Dispatcher dispatcher, ShellNumbers numbers, TrayIcon tray, IslandHost island)
    {
        _dispatcher = dispatcher;
        _numbers = numbers;
        _tray = tray;
        _island = island;

        _hover = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = HoverDelay };
        _hover.Tick += OnHoverFinished;
    }

    /// The gear on the card.
    public event Action? SettingsRequested;

    public event Action? TerminalRequested;

    public CardWindow? Window => _card;

    /// Whether the pointer is over the icon, the island or the card: the places an open card stays for.
    public bool PointerAtHome =>
        _tray.CursorOver || _island.CursorOver || (_card is { } card && Pointer.IsOver(card));

    /// What the card shows right now.
    public CardModel Model()
    {
        var now = DateTimeOffset.Now;
        var cards = _numbers.Cards();
        WakeWhenWorkingEnds(cards, now);
        return _numbers.CardFor(cards, now);
    }

    /// New numbers or new settings: an open card is drawn again.
    public void Refresh() => _card?.Update(Model());

    /// Windows sends nothing that plainly means "the mouse left the icon". The two popup messages
    /// are about its own tooltip and arrive in pairs on every move, so cancelling on them killed
    /// the count every time. The wait simply runs out, and then we ask where the pointer is.
    public void StartHover()
    {
        // Already counting, or the card is up: nothing to start.
        if (_card is not null || _hover.IsEnabled)
        {
            return;
        }

        Log.Write("hover started");
        _hover.Start();
    }

    public void CancelHover() => _hover.Stop();

    public void Open(bool pinned)
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

            _card.Update(Model());
            return;
        }

        var card = new CardWindow();
        card.SettingsRequested += () => SettingsRequested?.Invoke();
        card.TerminalRequested += () => TerminalRequested?.Invoke();
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
        card.Update(Model());
        if (pinned)
        {
            card.Pin();
        }

        WatchForTheMouseLeaving();

        if (_tray.Rect is { } icon)
        {
            card.ShowAt(icon);
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

    public void Dispose()
    {
        CancelHover();
        _card?.Close();
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
            Open(pinned: false);
        }
    }

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
        var timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
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
            if (PointerAtHome)
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
        var timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher);
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _card?.Update(Model());
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
}
