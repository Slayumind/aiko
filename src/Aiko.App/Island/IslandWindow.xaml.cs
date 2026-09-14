using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Aiko.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Aiko.App;

public partial class IslandWindow : Window
{
    private readonly DispatcherTimer _hover;
    private IslandPosition _position = IslandPosition.Default;

    public IslandWindow()
    {
        InitializeComponent();

        MouseLeftButtonDown += OnPressed;
        MouseMove += OnMoved;
        MouseLeftButtonUp += OnReleased;
        LostMouseCapture += (_, _) => EndDrag();

        // The island answers to resting the mouse on it as well as to a click, the same 1.5
        // seconds as the tray icon.
        _hover = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _hover.Tick += (_, _) =>
        {
            _hover!.Stop();
            if (IsMouseOver)
            {
                CardRequested?.Invoke(false);
            }
        };
        MouseEnter += (_, _) =>
        {
            if (!_pressed)
            {
                _hover.Start();
                Unfold(true);
            }
        };
        MouseLeave += (_, _) =>
        {
            _hover.Stop();
            if (!_revealing && !_pressed)
            {
                Unfold(false);
            }
        };

        _revealEnds = new DispatcherTimer { Interval = IslandReveal.For };
        _revealEnds.Tick += (_, _) =>
        {
            _revealEnds.Stop();
            _revealing = false;
            if (!IsMouseOver)
            {
                Unfold(false);
            }
        };

        // The window sizes itself to its content, and that happens after it is shown. Placing it
        // before then puts it wherever a width of zero lands, which is beside the middle instead
        // of in it.
        SizeChanged += (_, _) =>
        {
            if (!_dragging)
            {
                PlaceAt(_position, log: !_following);
            }
        };
    }

    // ---- unfolding (D-161): on a hover, and by itself for a moment when a limit crosses a threshold ----

    private readonly DispatcherTimer _revealEnds;
    private bool _revealing;
    private bool _following;
    private DateTime _followUntil;

    /// Also used by --try-unfold, which opens the island without a mouse.
    internal void Unfold(bool open)
    {
        if (Panel.IsUnfolded == open)
        {
            return;
        }

        Panel.Unfold(open);
        FollowTheSize();
    }

    /// The window keeps the size of the panel while the percentages slide in or out, one frame at a
    /// time, and stops following the moment they are done: no frames while nothing moves.
    private void FollowTheSize()
    {
        _followUntil = DateTime.UtcNow + Motion.Or0(Motion.Expand).TimeSpan + TimeSpan.FromMilliseconds(60);
        if (_following)
        {
            return;
        }

        _following = true;
        CompositionTarget.Rendering += OnFollowFrame;
    }

    private void OnFollowFrame(object? sender, EventArgs e)
    {
        Panel.MarkForMeasure();
        Fit();
        PlaceAt(_position, log: false);

        if (DateTime.UtcNow > _followUntil)
        {
            CompositionTarget.Rendering -= OnFollowFrame;
            _following = false;
        }
    }

    /// Opens the island for a moment without a hover, so the numbers that just crossed a threshold
    /// are seen.
    private void Reveal()
    {
        _revealing = true;
        _revealEnds.Stop();
        _revealEnds.Start();
        Unfold(true);
    }

    /// The card is asked for by a click, or by resting the mouse on the island. True means a click:
    /// the card then stays until it is closed, the same as after a click on the tray icon.
    public event Action<bool>? CardRequested;

    /// Raised after a drag, with the edge the island ended up on.
    public event Action<IslandPosition>? Moved;

    public void Show(IReadOnlyList<CardState> cards, IslandPosition position)
    {
        _position = position;
        Cards = cards;
        Panel.Show(cards, position.Edge);

        if (!IsVisible)
        {
            // See through until it is in place, or it would flash in the corner of the screen.
            Opacity = 0;
            Show();
        }

        Fit();
        PlaceAt(position);
        Opacity = 1;
    }

    /// The window takes exactly the size of what it holds.
    ///
    /// Letting the window size itself kept the width from the first layout: the rings stood in a
    /// column against the side edge while the island stayed as wide as a row, sticking out into
    /// the screen.
    private void Fit()
    {
        Panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        // Read once: setting the width resizes the window at once, the panel is measured again inside
        // the old height, and its size then reads as that height. A column in hand stayed one ring tall.
        var size = Panel.DesiredSize;
        Width = size.Width;
        Height = size.Height;
        UpdateLayout();
    }

    /// Puts the island on the edge it belongs to, on the screen it is currently over.
    public void PlaceAt(IslandPosition position, bool log = true)
    {
        var work = WorkArea();
        var placed = IslandPlacement.Place(position, ActualWidth, ActualHeight, work);

        Left = placed.X;
        Top = placed.Y;

        if (!log)
        {
            return;
        }

        Log.Write(
            $"island placed: work {work.X},{work.Y} {work.Width}x{work.Height}, " +
            $"size {ActualWidth:0}x{ActualHeight:0}, at {Left:0},{Top:0}, " +
            $"dpi {VisualTreeHelper.GetDpi(this).DpiScaleX:0.00}");
    }

    // ---- the landing strip (D-161): in hand, the island follows the mouse and a pane of frosted glass shows
    // where it will land; let go, and it settles there ----

    /// Further than this, a press is a drag and not a click.
    private const double DragFrom = 3;

    private bool _pressed;
    private bool _dragging;
    private Point _pressedAt;

    /// Where in the island the mouse holds it, as a share of its size: a row that turns into a
    /// column stays in the hand at the same spot.
    private Point _grip;

    private ScreenEdge _handEdge;
    private IslandGhost? _ghost;

    private void OnPressed(object sender, MouseButtonEventArgs e)
    {
        PressAt(MouseOnScreen(e));
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMoved(object sender, MouseEventArgs e)
    {
        if (_pressed)
        {
            HoldAt(MouseOnScreen(e));
        }
    }

    /// The island taken in hand at this point of the screen. The mouse handlers call it, and so does
    /// --try-glass, which drags the island from code without touching the person's mouse.
    internal void PressAt(Point cursor)
    {
        _hover.Stop();
        _pressed = true;
        _pressedAt = cursor;
        _grip = new Point((_pressedAt.X - Left) / ActualWidth, (_pressedAt.Y - Top) / ActualHeight);
    }

    internal void HoldAt(Point cursor)
    {
        if (!_dragging)
        {
            if (Math.Abs(cursor.X - _pressedAt.X) < DragFrom && Math.Abs(cursor.Y - _pressedAt.Y) < DragFrom)
            {
                return;
            }

            StartDrag();
        }

        var work = WorkArea();
        var edge = IslandPlacement.NearestEdge(cursor.X, cursor.Y, work, _handEdge);
        if (edge != _handEdge)
        {
            // The rings lay themselves out for the new edge while the island is still in hand.
            _handEdge = edge;
            Panel.Show(Cards, edge, docked: false);
            Fit();
        }

        Left = cursor.X - (ActualWidth * _grip.X);
        Top = cursor.Y - (ActualHeight * _grip.Y);

        var centreX = Left + (ActualWidth / 2);
        var centreY = Top + (ActualHeight / 2);
        var landing = IslandPlacement.DropAt(edge, centreX, centreY, ActualWidth, ActualHeight, work);
        _ghost?.PlaceOn(IslandPlacement.Place(landing, ActualWidth, ActualHeight, work), edge);
    }

    /// Lets the island go where it is, as a mouse button coming up would.
    internal void LetGo() => EndDrag();

    private void OnReleased(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        var wasDragging = _dragging;
        EndDrag();

        // A press that moved nothing is a click, and it opens the card.
        if (!wasDragging)
        {
            CardRequested?.Invoke(true);
        }
    }

    private void StartDrag()
    {
        _dragging = true;
        _revealing = false;
        _revealEnds.Stop();
        Panel.Unfold(false, animate: false);
        _handEdge = _position.Edge;
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);

        Panel.Show(Cards, _handEdge, docked: false);
        Fit();
        _ghost = new IslandGhost();
        _ghost.Shown += () => TaskbarOrder.PutOnTop(this);
    }

    /// Lets go: the island lands on the strip, and the strip goes away.
    private void EndDrag()
    {
        var wasDragging = _dragging;
        _pressed = false;
        _dragging = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        _ghost?.Close();
        _ghost = null;

        if (!wasDragging)
        {
            return;
        }

        var work = WorkArea();
        _position = IslandPlacement.DropAt(_handEdge, Left + (ActualWidth / 2), Top + (ActualHeight / 2), ActualWidth, ActualHeight, work);
        Land(work);
        Moved?.Invoke(_position);
    }

    /// The island flattens against its edge and settles into place with a small overshoot, the
    /// same spring as everything else Aiko moves (D-161).
    private void Land(Box work)
    {
        var from = new Point(Left, Top);

        Panel.Show(Cards, _position.Edge);
        Panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = Panel.DesiredSize;
        var target = IslandPlacement.Place(_position, size.Width, size.Height, work);

        // The spring overshoots past the edge a little. At the other edges that goes off the screen;
        // at the bottom it would land on the taskbar, so the island lands under it instead.
        TaskbarOrder.PutUnder(this);

        if (Motion.IsOn)
        {
            BeginAnimation(LeftProperty, new DoubleAnimation(from.X, target.X, Motion.Settle) { EasingFunction = Motion.Spring });
            BeginAnimation(TopProperty, new DoubleAnimation(from.Y, target.Y, Motion.Settle) { EasingFunction = Motion.Spring });
        }

        // Under a running animation these only set where the island stays once it has landed.
        Width = size.Width;
        Height = size.Height;
        Left = target.X;
        Top = target.Y;
    }

    /// The mouse in the units WPF places windows in. Measured against this window while it holds the
    /// mouse, which works just as well outside it.
    private Point MouseOnScreen(MouseEventArgs e)
    {
        var inside = e.GetPosition(this);
        return new Point(Left + inside.X, Top + inside.Y);
    }

    private IReadOnlyList<CardState> Cards { get; set; } = [];

    public void Update(IReadOnlyList<CardState> cards)
    {
        var crossed = IslandReveal.ToneRose(Cards, cards);
        Cards = cards;

        // New numbers while the island is in hand change the rings, never where it is.
        if (_dragging)
        {
            Panel.Show(cards, _handEdge, docked: false);
            return;
        }

        Panel.Show(cards, _position.Edge);
        Fit();
        PlaceAt(_position, log: false);

        if (crossed)
        {
            Reveal();
        }
    }

    /// The working area of the monitor this window is on, in the units WPF uses. Not
    /// SystemParameters.WorkArea: that one only ever describes the main screen.
    private Box WorkArea()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var handle = new WindowInteropHelper(this).Handle;

        if (handle == IntPtr.Zero)
        {
            var fallback = SystemParameters.WorkArea;
            return new Box(fallback.X, fallback.Y, fallback.Width, fallback.Height);
        }

        var monitor = PInvoke.MonitorFromWindow((HWND)handle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };

        if (!PInvoke.GetMonitorInfo(monitor, ref info))
        {
            var fallback = SystemParameters.WorkArea;
            return new Box(fallback.X, fallback.Y, fallback.Width, fallback.Height);
        }

        var work = info.rcWork;
        return new Box(
            work.left / dpi.DpiScaleX,
            work.top / dpi.DpiScaleY,
            (work.right - work.left) / dpi.DpiScaleX,
            (work.bottom - work.top) / dpi.DpiScaleY);
    }
}
