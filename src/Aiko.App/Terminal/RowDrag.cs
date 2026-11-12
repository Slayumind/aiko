using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Aiko.App;

/// A session dragged to another place in its folder (D-400), the way the cards of the Office are
/// dragged: after 6 pixels a card of the row lifts off, tilted a little and with a shadow, and
/// follows the pointer; the row stays as a pale place that moves through its folder while the
/// other rows slide out of its way; let go, the copy glides into that place. A press that does not
/// move stays a click.
///
/// The list is drawn anew on every change of a session, which would take the dragged row away in
/// the middle of the drag, so the panel holds its new view until the drag ends. The rows are drawn
/// in the new order then, where the slide has already put them, so nothing jumps.
///
/// The copy flies in a popup of its own, over the whole window: the terminal is a window of its
/// own inside ours, and nothing drawn in our window can cover it. The popup lives only while a row
/// is dragged.
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

    /// Room around the copy inside its popup, for the tilt and the shadow.
    private const double Room = 16;

    private readonly Panel _rows;
    private readonly Popup _layer = new()
    {
        AllowsTransparency = true,
        Placement = PlacementMode.Relative,
        StaysOpen = true,
        IsHitTestVisible = false,
        Focusable = false,
    };
    private readonly List<(FrameworkElement Row, string Id, string Folder)> _known = [];
    private (FrameworkElement Row, string Id, string Folder, Func<FrameworkElement> Card, Point From)? _press;

    /// The rows of the folder of the drag, in their order, the dragged one among them, with the
    /// place each had when the drag started.
    private List<(FrameworkElement Row, string Id, double Top, double Height)> _folder = [];
    private int _from;
    private int _to;
    private Border? _copy;
    private bool _landing;
    private Point _grab;

    /// rows: the panel of the list.
    public RowDrag(Panel rows)
    {
        _rows = rows;
        _layer.PlacementTarget = rows;

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

    /// card: what the lifted card shows, made anew for each drag: the whole name, the state and
    /// the time, which the row itself may cut to the width of the list.
    public void Watch(FrameworkElement row, string id, string folder, Func<FrameworkElement> card)
    {
        _known.Add((row, id, folder));
        row.PreviewMouseLeftButtonDown += (_, e) =>
        {
            Swallows = false;
            _press = e.ClickCount == 1 && !Dragging ? (row, id, folder, card, e.GetPosition(_rows)) : null;
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

    private void Start((FrameworkElement Row, string Id, string Folder, Func<FrameworkElement> Card, Point From) press)
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

        _copy = Copy(press.Row, press.Card());
        _grab = new Point(press.From.X, press.From.Y - _folder[_from].Top);
        _layer.Child = new Border
        {
            Padding = new Thickness(Room),
            Background = Brushes.Transparent,
            LayoutTransform = PopupZoom.Of(_rows),
            Child = _copy,
        };
        MoveCopy(0, _folder[_from].Top);
        _layer.IsOpen = true;
        press.Row.Opacity = PaleOpacity;

        Dragging = true;
        _rows.CaptureMouse();
    }

    /// The widest the lifted card grows for a long name; past it the name is cut.
    private const double CardMostWidth = 560;

    /// The card of the row on the raised fill of a card, tilted, with the shadow of the cards of
    /// Aiko: as tall as the row and at least as wide, wider for a name the list cuts. The row
    /// itself stays in the list as the pale place.
    private static Border Copy(FrameworkElement row, FrameworkElement card) => new()
    {
        MinWidth = row.ActualWidth,
        MaxWidth = Math.Max(row.ActualWidth, CardMostWidth),
        Height = row.ActualHeight,
        Margin = row.Margin,
        Padding = new Thickness(10, 8, 10, 8),
        CornerRadius = new CornerRadius(10),
        Background = Tokens.Brush("Raised"),
        BorderBrush = Tokens.Brush("InputLine"),
        BorderThickness = new Thickness(1),
        Effect = Tokens.Get<System.Windows.Media.Effects.Effect>("CardShadow"),
        RenderTransformOrigin = new Point(0.5, 0.5),
        RenderTransform = new RotateTransform(Motion.IsOn ? Tilt : 0),
        Child = card,
    };

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!Dragging || _copy is null)
        {
            return;
        }

        var pointer = e.GetPosition(_rows);
        MoveCopy(pointer.X - _grab.X, pointer.Y - _grab.Y);

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
            _layer.IsOpen = false;
            _layer.Child = null;
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
        var landing = (drop ? row.Top + PlaceShift() : row.Top) - Room;
        var glide = new DoubleAnimation(landing, Landing) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        glide.Completed += (_, _) => Done();
        _layer.BeginAnimation(Popup.VerticalOffsetProperty, glide);
        _layer.BeginAnimation(Popup.HorizontalOffsetProperty, new DoubleAnimation(-Room, Landing) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
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

    /// The copy at this place of the list: the popup is wider by its room on every side.
    private void MoveCopy(double left, double top)
    {
        _layer.BeginAnimation(Popup.HorizontalOffsetProperty, null);
        _layer.BeginAnimation(Popup.VerticalOffsetProperty, null);
        _layer.HorizontalOffset = left - Room;
        _layer.VerticalOffset = top - Room;
    }

    private double Top(FrameworkElement row) => row.TranslatePoint(new Point(0, 0), _rows).Y - row.Margin.Top;
}
