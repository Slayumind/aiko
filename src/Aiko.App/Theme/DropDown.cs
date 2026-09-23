using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Aiko.App;

/// A choice from a short list, drawn in Aiko's own look (D-168).
///
/// The standard ComboBox of Windows cannot take the motion language and looks like another app.
/// This one is a button that opens a small list below itself. It works from the keyboard: Enter,
/// Space, Down or F4 open it, the arrows move, Enter picks, Escape closes. A click anywhere else
/// closes it too.
sealed class DropDown : UserControl
{
    private readonly Border _button = new();
    private readonly TextBlock _chosen = new();
    private readonly RotateTransform _chevronTurn = new();
    private readonly Popup _popup = new();
    private readonly StackPanel _list = new();
    private readonly ScaleTransform _listScale = new(1, 1);
    private readonly Border _listFrame = new();
    private IReadOnlyList<string> _items = [];
    private int _selected = -1;
    private int _highlighted = -1;

    public DropDown()
    {
        Focusable = true;
        FocusVisualStyle = null;
        Build();
        KeyDown += OnKeyDown;
        LostKeyboardFocus += (_, _) => Close();
    }

    /// Raised when the person picks an item, not when the code sets one.
    public event Action<int>? Picked;

    public IReadOnlyList<string> Items
    {
        get => _items;
        set
        {
            _items = value;
            _selected = Math.Clamp(_selected, -1, _items.Count - 1);
            Refresh();
        }
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            _selected = Math.Clamp(value, -1, _items.Count - 1);
            Refresh();
        }
    }

    public bool IsOpen => _popup.IsOpen;

    private void Build()
    {
        var chevron = new Path
        {
            Data = Geometry.Parse("M0,0 L4,4 L8,0"),
            Stroke = Brush("Muted"),
            StrokeThickness = 1.4,
            Width = 8,
            Height = 5,
            Margin = new Thickness(8, 1, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _chevronTurn,
        };

        _chosen.FontFamily = Tokens.Get<FontFamily>("Sans");
        _chosen.FontSize = Tokens.Get<double>("TextSmall");
        _chosen.Foreground = Brush("Ink");
        _chosen.VerticalAlignment = VerticalAlignment.Center;
        _chosen.TextTrimming = TextTrimming.CharacterEllipsis;

        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(chevron, Dock.Right);
        row.Children.Add(chevron);
        row.Children.Add(_chosen);

        var hover = new Border { CornerRadius = new CornerRadius(8), Background = Brush("HoverLayer"), Opacity = 0 };
        var face = new Grid();
        face.Children.Add(hover);
        face.Children.Add(new Border { Padding = new Thickness(10, 0, 10, 0), Child = row });

        _button.Height = 30;
        _button.CornerRadius = new CornerRadius(8);
        _button.Background = Brush("Raised");
        _button.BorderBrush = Brush("Hairline");
        _button.BorderThickness = new Thickness(1);
        _button.Cursor = Cursors.Hand;
        _button.Child = face;
        _button.MouseEnter += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(1, Motion.Hover, Motion.Standard));
        _button.MouseLeave += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(0, Motion.Hover, Motion.Standard));
        _button.MouseLeftButtonUp += (_, _) =>
        {
            Focus();
            Toggle();
        };
        Motion.SetPress(_button, true);

        _listFrame.Background = Brush("Surface");
        _listFrame.BorderBrush = Brush("InputLine");
        _listFrame.BorderThickness = new Thickness(1);
        _listFrame.CornerRadius = new CornerRadius(10);
        _listFrame.Padding = new Thickness(4);
        _listFrame.Margin = new Thickness(0, 4, 0, 12);
        _listFrame.Effect = Tokens.Get<System.Windows.Media.Effects.Effect>("CardShadow");
        _listFrame.RenderTransformOrigin = new Point(0.5, 0);
        _listFrame.RenderTransform = _listScale;
        _listFrame.Child = _list;

        // A press on the list bubbles to the page through the drop-down. A scroll viewer there,
        // as on every settings page, takes the keyboard focus on a press, the drop-down loses it
        // and closes, and the release that picks the item never comes. The press stops here.
        _listFrame.MouseLeftButtonDown += (_, e) => e.Handled = true;

        _popup.PlacementTarget = _button;
        _popup.Placement = PlacementMode.Bottom;
        _popup.AllowsTransparency = true;
        _popup.StaysOpen = false;
        _popup.Child = _listFrame;
        _popup.Closed += (_, _) => _chevronTurn.BeginAnimation(RotateTransform.AngleProperty, Motion.To(0, Motion.Expand, Motion.Standard));

        var root = new Grid();
        root.Children.Add(_button);
        root.Children.Add(_popup);
        Content = root;

        Refresh();
    }

    private void Toggle()
    {
        if (_popup.IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    private void Open()
    {
        if (_items.Count == 0)
        {
            return;
        }

        _highlighted = Math.Max(_selected, 0);
        _listFrame.MinWidth = _button.ActualWidth;
        FillList();
        _popup.IsOpen = true;

        _listFrame.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, Motion.Or0(Motion.Expand)) { EasingFunction = Motion.Standard });
        _listScale.BeginAnimation(ScaleTransform.ScaleYProperty, new System.Windows.Media.Animation.DoubleAnimation(0.96, 1, Motion.Or0(Motion.Expand)) { EasingFunction = Motion.Standard });
        _chevronTurn.BeginAnimation(RotateTransform.AngleProperty, Motion.To(180, Motion.Expand, Motion.Standard));
    }

    private void Close() => _popup.IsOpen = false;

    private void Pick(int index)
    {
        Close();
        if (index < 0 || index >= _items.Count)
        {
            return;
        }

        var changed = index != _selected;
        _selected = index;
        Refresh();
        if (changed)
        {
            Picked?.Invoke(index);
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter or Key.Space when _popup.IsOpen:
                Pick(_highlighted);
                break;
            case Key.Enter or Key.Space or Key.F4:
            case Key.Down when !_popup.IsOpen:
                Open();
                break;
            case Key.Down:
                Highlight(Math.Min(_highlighted + 1, _items.Count - 1));
                break;
            case Key.Up when _popup.IsOpen:
                Highlight(Math.Max(_highlighted - 1, 0));
                break;
            case Key.Escape when _popup.IsOpen:
                Close();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void Highlight(int index)
    {
        _highlighted = index;
        FillList();
    }

    private void FillList()
    {
        _list.Children.Clear();
        for (var i = 0; i < _items.Count; i++)
        {
            _list.Children.Add(Item(i));
        }
    }

    private Border Item(int index)
    {
        var text = new TextBlock
        {
            Text = _items[index],
            FontFamily = Tokens.Get<FontFamily>("Sans"),
            FontSize = Tokens.Get<double>("TextSmall"),
            Foreground = Brush(index == _selected ? "Ink" : "Muted"),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var tick = new Path
        {
            Data = Geometry.Parse("M0,3 L3,6 L9,0"),
            Stroke = Brush("Ink"),
            StrokeThickness = 1.4,
            Width = 9,
            Height = 6,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = index == _selected ? Visibility.Visible : Visibility.Hidden,
        };

        var row = new DockPanel();
        DockPanel.SetDock(tick, Dock.Right);
        row.Children.Add(tick);
        row.Children.Add(text);

        var item = new Border
        {
            Height = 28,
            Padding = new Thickness(8, 0, 8, 0),
            CornerRadius = new CornerRadius(6),
            Background = index == _highlighted ? Brush("Raised") : Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = row,
        };

        item.MouseEnter += (_, _) =>
        {
            _highlighted = index;
            foreach (var other in _list.Children.OfType<Border>())
            {
                other.Background = ReferenceEquals(other, item) ? Brush("Raised") : Brushes.Transparent;
            }
        };
        item.MouseLeftButtonUp += (_, _) => Pick(index);
        Motion.SetPress(item, true);
        return item;
    }

    private void Refresh()
    {
        _chosen.Text = _selected >= 0 && _selected < _items.Count ? _items[_selected] : "";
        if (_popup.IsOpen)
        {
            FillList();
        }
    }

    private Brush Brush(string key) => Tokens.Brush(key);
}
