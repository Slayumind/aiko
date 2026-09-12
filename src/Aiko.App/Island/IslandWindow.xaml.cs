using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Aiko.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Aiko.App;

public partial class IslandWindow : Window
{
    private readonly DispatcherTimer _hover;
    private IslandPosition _position = IslandPosition.Default;

    public IslandWindow()
    {
        InitializeComponent();

        MouseLeftButtonDown += OnPressed;

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
        MouseEnter += (_, _) => _hover.Start();
        MouseLeave += (_, _) => _hover.Stop();

        // The window sizes itself to its content, and that happens after it is shown. Placing it
        // before then puts it wherever a width of zero lands, which is beside the middle instead
        // of in it.
        SizeChanged += (_, _) => PlaceAt(_position);
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

        Width = Panel.DesiredSize.Width;
        Height = Panel.DesiredSize.Height;
        UpdateLayout();
    }

    /// Puts the island on the edge it belongs to, on the screen it is currently over.
    public void PlaceAt(IslandPosition position)
    {
        var work = WorkArea();
        var placed = IslandPlacement.Place(position, ActualWidth, ActualHeight, work);

        Left = placed.X;
        Top = placed.Y;

        Log.Write(
            $"island placed: work {work.X},{work.Y} {work.Width}x{work.Height}, " +
            $"size {ActualWidth:0}x{ActualHeight:0}, at {Left:0},{Top:0}, " +
            $"dpi {VisualTreeHelper.GetDpi(this).DpiScaleX:0.00}");
    }

    private void OnPressed(object sender, MouseButtonEventArgs e)
    {
        var before = new Point(Left, Top);
        DragMove();

        // A click that moved nothing is a click, and it opens the card.
        if (Math.Abs(Left - before.X) < 3 && Math.Abs(Top - before.Y) < 3)
        {
            PlaceAt(_position);
            CardRequested?.Invoke(true);
            return;
        }

        SnapToEdge();
    }

    /// Dropped anywhere, the island goes to the nearest edge of the screen it was dropped on.
    private void SnapToEdge()
    {
        var work = WorkArea();
        var where = new Box(Left, Top, ActualWidth, ActualHeight);

        _position = IslandPlacement.Nearest(where, work);
        Panel.Show(Cards, _position.Edge);
        Fit();
        PlaceAt(_position);

        Moved?.Invoke(_position);
    }

    private IReadOnlyList<CardState> Cards { get; set; } = [];

    public void Update(IReadOnlyList<CardState> cards)
    {
        Cards = cards;
        Panel.Show(cards, _position.Edge);
        Fit();
        PlaceAt(_position);
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
