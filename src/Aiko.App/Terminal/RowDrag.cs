using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Aiko.App;

/// A session dragged to another place in its folder (D-400). A press on a row and a move of a few
/// pixels starts the drag; a line shows where the row will stand, only between rows of the same
/// folder; letting go says where. A press that does not move stays a click.
///
/// The list is drawn anew on every change of a session, which would take the dragged row away in
/// the middle of the drag, so the panel holds its new view until the drag ends.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class RowDrag
{
    private readonly Panel _rows;
    private readonly Canvas _layer;
    private readonly Border _line;
    private readonly List<(FrameworkElement Row, string Id, string Folder)> _known = [];
    private (FrameworkElement Row, string Id, string Folder, Point From)? _press;
    private string? _before;

    /// rows: the panel of the list. layer: a canvas laid over it, for the line.
    public RowDrag(Panel rows, Canvas layer)
    {
        _rows = rows;
        _layer = layer;
        _line = new Border
        {
            Height = 2,
            CornerRadius = new CornerRadius(1),
            Background = Tokens.Brush("Ink"),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        _layer.Children.Add(_line);
        _layer.IsHitTestVisible = false;

        _rows.MouseMove += OnMove;
        _rows.MouseLeftButtonUp += OnUp;
        _rows.LostMouseCapture += (_, _) => End(drop: false);
    }

    /// The session was let go: its id, and the id of the session it now stands before, or null
    /// for the end of its folder.
    public event Action<string, string?>? Moved;

    /// The drag is over, dropped or not.
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
            _press = e.ClickCount == 1 ? (row, id, folder, e.GetPosition(_rows)) : null;
        };
        row.MouseMove += (_, e) =>
        {
            if (_press is not { } press || Dragging || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var moved = e.GetPosition(_rows) - press.From;
            if (Math.Abs(moved.Y) >= SystemParameters.MinimumVerticalDragDistance)
            {
                Start(press.Row);
            }
        };
    }

    private void Start(FrameworkElement row)
    {
        Dragging = true;
        row.Opacity = 0.45;
        _rows.CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!Dragging || _press is not { } press)
        {
            return;
        }

        // The rows of the same folder, without the one that moves: the line goes before the first
        // one whose middle is under the pointer, or after the last.
        var y = e.GetPosition(_rows).Y;
        var folder = _known.Where(known => known.Folder == press.Folder && known.Id != press.Id && known.Row.IsVisible).ToList();
        if (folder.Count == 0)
        {
            return;
        }

        var target = folder.FirstOrDefault(known => y < Top(known.Row) + (known.Row.ActualHeight / 2));
        _before = target.Row is null ? null : target.Id;
        var lineY = target.Row is null ? Top(folder[^1].Row) + folder[^1].Row.ActualHeight : Top(target.Row);

        Canvas.SetLeft(_line, 8);
        Canvas.SetTop(_line, lineY - 1);
        _line.Width = Math.Max(0, _rows.ActualWidth - 16);
        _line.Visibility = Visibility.Visible;
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (Dragging)
        {
            e.Handled = true;
            Swallows = true;
            End(drop: true);
        }

        _press = null;
    }

    private void End(bool drop)
    {
        if (!Dragging)
        {
            return;
        }

        Dragging = false;
        _line.Visibility = Visibility.Collapsed;
        var press = _press;
        _press = null;
        press?.Row.ClearValue(UIElement.OpacityProperty);
        if (_rows.IsMouseCaptured)
        {
            _rows.ReleaseMouseCapture();
        }

        // A drop where the line never showed changes nothing.
        if (drop && press is { } done && _line.Width > 0)
        {
            Moved?.Invoke(done.Id, _before);
        }

        _before = null;
        _line.Width = 0;
        Ended?.Invoke();
    }

    private double Top(FrameworkElement row) => row.TranslatePoint(new Point(0, 0), _rows).Y;
}
