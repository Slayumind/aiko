using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Aiko.App;

/// A session dragged to another place in its folder (D-400), the way the cards of the Office are
/// dragged: after 6 pixels a copy of the row lifts off, tilted a little and with a shadow, and
/// follows the pointer; the row stays as a pale place that moves through its folder while the
/// other rows slide out of its way; let go, the copy glides into that place. A press that does not
/// move stays a click.
///
/// The list is drawn anew on every change of a session, which would take the dragged row away in
/// the middle of the drag, so the panel holds its new view until the drag ends. The rows are drawn
/// in the new order then, where the slide has already put them, so nothing jumps.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class RowDrag
{
    /// The numbers of the Office (office-dnd.ts, desktop-board.tsx): the distance that starts a
    /// drag, the tilt of the copy, the pale place, the slide of the others and the landing.
    private const double StartDistance = 6;
    private const double Tilt = -2;
    private const double PaleOpacity = 0.3;
    private static readonly Duration Slide = new(TimeSpan.FromMilliseconds(250));
    private static readonly Duration Landing = new(TimeSpan.FromMilliseconds(180));

    private readonly Panel _rows;
    private readonly Canvas _layer;
    private readonly List<(FrameworkElement Row, string Id, string Folder)> _known = [];
    private (FrameworkElement Row, string Id, string Folder, Point From)? _press;

    /// The rows of the folder of the drag, in their order, the dragged one among them, with the
    /// place each had when the drag started.
    private List<(FrameworkElement Row, string Id, double Top, double Height)> _folder = [];
    private int _from;
    private int _to;
    private Border? _copy;
    private bool _landing;
    private Point _grab;

    /// rows: the panel of the list. layer: a canvas laid over it, for the copy that is dragged.
    public RowDrag(Panel rows, Canvas layer)
    {
        _rows = rows;
        _layer = layer;
        _layer.IsHitTestVisible = false;

        _rows.MouseMove += OnMove;
        _rows.MouseLeftButtonUp += OnUp;
        _rows.LostMouseCapture += (_, _) => Land(drop: false);
    }

    /// The session was let go in a new place: its id, and the id of the session it now stands
    /// before, or null for the end of its folder.
    public event Action<string, string?>? Moved;

    /// The drag is over, dropped or not, and the copy has landed.
    public event Action? Ended;

    public bool Dragging { get; private set; }

    /// True for the click that ends a drag, so the row under the mouse is not opened by it.
    public bool Swallows { get; private set; }

    /// The rows are drawn anew: the old ones are forgotten.
    public void Clear() => _known.Clear();

    public void Watch(FrameworkElement row, string id, string folder)
    {
        _known.Add((row, id, folder));
        row.PreviewMouseLeftButtonDown += (_, e) =>
        {
            Swallows = false;
            _press = e.ClickCount == 1 && !Dragging ? (row, id, folder, e.GetPosition(_rows)) : null;
        };
        row.MouseMove += (_, e) =>
        {
            if (_press is not { } press || Dragging || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            if ((e.GetPosition(_rows) - press.From).Length >= StartDistance)
            {
                Start(press);
            }
        };
    }

    private void Start((FrameworkElement Row, string Id, string Folder, Point From) press)
    {
        _folder = [.. _known
            .Where(known => known.Folder == press.Folder && known.Row.IsVisible)
            .Select(known => (known.Row, known.Id, Top(known.Row), known.Row.ActualHeight + known.Row.Margin.Top + known.Row.Margin.Bottom))];
        _from = _folder.FindIndex(known => known.Id == press.Id);
        if (_from < 0)
        {
            return;
        }

        _to = _from;
        foreach (var known in _folder)
        {
            known.Row.RenderTransform = new TranslateTransform();
        }

        _copy = Copy(press.Row);
        _grab = new Point(press.From.X, press.From.Y - _folder[_from].Top);
        Canvas.SetLeft(_copy, 0);
        Canvas.SetTop(_copy, _folder[_from].Top);
        _layer.Children.Add(_copy);
        press.Row.Opacity = PaleOpacity;

        Dragging = true;
        _rows.CaptureMouse();
    }

    /// A picture of the row on the raised fill of a card, tilted, with the shadow of the cards of
    /// Aiko: the row itself stays in the list as the pale place.
    private static Border Copy(FrameworkElement row)
    {
        var dpi = VisualTreeHelper.GetDpi(row);
        var width = Math.Max(1, row.ActualWidth);
        var height = Math.Max(1, row.ActualHeight);
        var picture = new RenderTargetBitmap((int)Math.Ceiling(width * dpi.DpiScaleX), (int)Math.Ceiling(height * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        picture.Render(row);

        return new Border
        {
            Width = width,
            Height = height,
            Margin = row.Margin,
            CornerRadius = new CornerRadius(10),
            Background = Tokens.Brush("Raised"),
            BorderBrush = Tokens.Brush("InputLine"),
            BorderThickness = new Thickness(1),
            Effect = Tokens.Get<System.Windows.Media.Effects.Effect>("CardShadow"),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(Motion.IsOn ? Tilt : 0),
            Child = new Image { Source = picture, Width = width, Height = height, Stretch = Stretch.None },
        };
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!Dragging || _copy is null)
        {
            return;
        }

        var pointer = e.GetPosition(_rows);
        Canvas.SetLeft(_copy, pointer.X - _grab.X);
        Canvas.SetTop(_copy, pointer.Y - _grab.Y);

        // The place is where the middle of the copy is: past every row whose middle is above it.
        var middle = pointer.Y - _grab.Y + (_folder[_from].Height / 2);
        var to = Math.Clamp(Place(middle), 0, _folder.Count - 1);
        if (to != _to)
        {
            _to = to;
            SlideRows();
        }
    }

    /// The index the dragged row takes among the rows of its folder, for the middle of the copy.
    private int Place(double middle)
    {
        var place = 0;
        for (var at = 0; at < _folder.Count; at++)
        {
            if (at == _from)
            {
                continue;
            }

            if (middle > _folder[at].Top + (_folder[at].Height / 2))
            {
                place++;
            }
        }

        return place;
    }

    /// The rows between the old place and the new one move out of the way by the height of the
    /// dragged row, and the pale place moves to where the row will stand.
    private void SlideRows()
    {
        var moving = _folder[_from].Height;
        for (var at = 0; at < _folder.Count; at++)
        {
            if (at != _from)
            {
                var passed = _to > _from ? at > _from && at <= _to : at < _from && at >= _to;
                SlideTo(_folder[at].Row, !passed ? 0 : _to > _from ? -moving : moving, Slide);
            }
        }

        SlideTo(_folder[_from].Row, PlaceShift(), Slide);
    }

    private static void SlideTo(FrameworkElement row, double y, Duration time)
    {
        if (row.RenderTransform is not TranslateTransform shift)
        {
            return;
        }

        if (!Motion.IsOn)
        {
            shift.BeginAnimation(TranslateTransform.YProperty, null);
            shift.Y = y;
            return;
        }

        shift.BeginAnimation(TranslateTransform.YProperty, Motion.To(y, time, Motion.Standard));
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (Dragging)
        {
            e.Handled = true;
            Swallows = true;
            Land(drop: true);
        }

        _press = null;
    }

    /// The copy glides to the pale place, or back where it came from when the drag was lost, and
    /// goes; then the move is told and the list is drawn again.
    private void Land(bool drop)
    {
        if (!Dragging || _landing || _copy is not { } copy)
        {
            return;
        }

        // The list keeps holding still while the copy lands: drawn anew in the middle, the pale
        // place would jump back to where the row was.
        _landing = true;
        _press = null;
        if (_rows.IsMouseCaptured)
        {
            _rows.ReleaseMouseCapture();
        }

        if (!drop)
        {
            _to = _from;
            SlideRows();
        }

        var row = _folder[_from];
        void Done()
        {
            Dragging = false;
            _landing = false;
            _layer.Children.Remove(copy);
            _copy = null;
            row.Row.ClearValue(UIElement.OpacityProperty);
            if (drop && _to != _from)
            {
                var order = _folder.Select(known => known.Id).Where(id => id != row.Id).ToList();
                order.Insert(_to, row.Id);
                var before = _to + 1 < order.Count ? order[_to + 1] : null;
                Moved?.Invoke(row.Id, before);
            }

            Ended?.Invoke();
        }

        if (!Motion.IsOn)
        {
            Done();
            return;
        }

        // Where the pale place will stand once its slide ends: the new top of the row.
        var landing = drop ? row.Top + PlaceShift() : row.Top;
        var glide = new DoubleAnimation(landing, Landing) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        glide.Completed += (_, _) => Done();
        copy.BeginAnimation(Canvas.TopProperty, glide);
        copy.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(0, Landing) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        if (copy.RenderTransform is RotateTransform tilt)
        {
            tilt.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, Landing));
        }
    }

    /// How far the pale place moves for the place the row takes now.
    private double PlaceShift()
    {
        double shift = 0;
        for (var at = 0; at < _folder.Count; at++)
        {
            if (_to > _from && at > _from && at <= _to)
            {
                shift += _folder[at].Height;
            }
            else if (_to < _from && at < _from && at >= _to)
            {
                shift -= _folder[at].Height;
            }
        }

        return shift;
    }

    private double Top(FrameworkElement row) => row.TranslatePoint(new Point(0, 0), _rows).Y - row.Margin.Top;
}
