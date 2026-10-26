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
    /// PageUp and PageDown move this many rows (Cherry Studio, research of 2026-10-26).
    private const int PageRows = 7;

    private readonly Border _listFrame = new();
    private IReadOnlyList<string> _items = [];
    private IReadOnlyList<string> _short = [];
    private IReadOnlyDictionary<int, string> _headers = new Dictionary<int, string>();
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

    /// Shorter words for the chosen item on the button, in the order of Items: the list may say
    /// more to tell two items apart than the button needs. Empty: the button shows the item.
    public IReadOnlyList<string> Short
    {
        get => _short;
        set
        {
            _short = value;
            Refresh();
        }
    }

    /// Group titles in the open list, each above the item with its index. A title is not an
    /// item: it cannot be picked and the arrows step over it.
    public IReadOnlyDictionary<int, string> Headers
    {
        get => _headers;
        set
        {
            _headers = value;
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

    /// The quiet look for a line of small text, as under the field of the terminal: no fill and no
    /// frame, the text in the mono face, the list as usual.
    public void MakeQuiet()
    {
        _button.Background = Brushes.Transparent;
        _button.BorderThickness = new Thickness(0);
        _button.Height = 20;
        _button.CornerRadius = new CornerRadius(5);
        _chosen.FontFamily = Tokens.Get<FontFamily>("Mono");
        _chosen.FontSize = 11.5;
        _chosen.Foreground = Brush("Muted");
        if (_button.Child is Grid { Children: [_, Border inner] })
        {
            inner.Padding = new Thickness(4, 0, 4, 0);
        }
    }

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
        // The look of a text field of the site: a quiet fill and a thin line (D-332).
        _button.Background = Brush("Surface");
        _button.BorderBrush = Brush("InputLine");
        _button.BorderThickness = new Thickness(1);
        _button.Cursor = Cursors.Hand;
        _button.Child = face;
        _button.MouseEnter += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(1, Motion.Hover, Motion.Standard));
        _button.MouseLeave += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(0, Motion.Hover, Motion.Standard));
        // The list opens on the press, not the release, as native menus and Radix do: a press,
        // a slide to an item and a release picks it.
        _button.MouseLeftButtonDown += (_, e) =>
        {
            Focus();
            Toggle();
            e.Handled = true;
        };

        // The release of the same press lands outside the list, and a popup that closes on any
        // click outside would close at once (check of 2026-10-26). Open keeps the list until that
        // release; the list itself is another window, so a release on it is caught here.
        _listFrame.PreviewMouseLeftButtonUp += (_, _) => _popup.StaysOpen = false;
        Motion.SetPress(_button, true);

        _listFrame.Background = Brush("Surface");
        _listFrame.BorderBrush = Brush("InputLine");
        _listFrame.BorderThickness = new Thickness(1);
        _listFrame.CornerRadius = new CornerRadius(10);
        _listFrame.Padding = new Thickness(4);
        _listFrame.Margin = new Thickness(0, 4, 0, 12);
        _listFrame.Effect = Tokens.Get<System.Windows.Media.Effects.Effect>("CardShadow");
        _listFrame.Child = _list;

        // A press on the list bubbles to the page through the drop-down. A scroll viewer there,
        // as on every settings page, takes the keyboard focus on a press, the drop-down loses it
        // and closes, and the release that picks the item never comes. The press stops here.
        _listFrame.MouseLeftButtonDown += (_, e) => e.Handled = true;

        _popup.PlacementTarget = _button;

        // Our own place: the left edges meet, the list under the button, above it if there is no
        // room below. Bottom placement follows the "menus on the right" setting of Windows and can
        // land beside the button; a left edge of 0 lands on it whatever the units of the screen.
        _popup.Placement = PlacementMode.Custom;
        _popup.CustomPopupPlacementCallback = (popup, target, _) =>
        [
            new CustomPopupPlacement(new Point(0, target.Height), PopupPrimaryAxis.Horizontal),
            new CustomPopupPlacement(new Point(0, -popup.Height), PopupPrimaryAxis.Horizontal),
        ];

        // The list grows after it opened, when its rows are laid out. A nudge makes the popup
        // place itself again for the final size.
        _listFrame.SizeChanged += (_, _) =>
        {
            if (_popup.IsOpen)
            {
                _popup.HorizontalOffset += 0.01;
                _popup.HorizontalOffset -= 0.01;
            }
        };
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
        // Opened by a press: the list stays until that press is released anywhere in the window.
        var pressed = Mouse.LeftButton == MouseButtonState.Pressed;
        _popup.StaysOpen = pressed;
        if (pressed && Window.GetWindow(this) is { } window)
        {
            void Released(object sender, MouseButtonEventArgs e)
            {
                window.PreviewMouseLeftButtonUp -= Released;
                _popup.StaysOpen = false;
            }

            window.PreviewMouseLeftButtonUp += Released;
        }

        _popup.IsOpen = true;

        Motion.PopIn(_listFrame, above: false);
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
            case Key.Home when _popup.IsOpen:
                Highlight(0);
                break;
            case Key.End when _popup.IsOpen:
                Highlight(_items.Count - 1);
                break;
            case Key.PageUp when _popup.IsOpen:
                Highlight(Math.Max(_highlighted - PageRows, 0));
                break;
            case Key.PageDown when _popup.IsOpen:
                Highlight(Math.Min(_highlighted + PageRows, _items.Count - 1));
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
            if (_headers.TryGetValue(i, out var header))
            {
                _list.Children.Add(Header(header, first: i == 0));
            }

            _list.Children.Add(Item(i));
        }
    }

    private static TextBlock Header(string text, bool first) => new()
    {
        Text = text,
        FontFamily = Tokens.Get<FontFamily>("Sans"),
        FontSize = Tokens.Get<double>("TextTiny"),
        Foreground = Tokens.Brush("Muted"),
        Margin = new Thickness(8, first ? 4 : 10, 8, 4),
    };

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

        // Only a pointer that moves lights a row: a still one under a list that changed keeps the
        // row of the keyboard.
        item.MouseMove += (_, _) =>
        {
            if (_highlighted == index || !PointerTrail.Moved(item))
            {
                return;
            }

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
        _chosen.Text = _selected < 0 || _selected >= _items.Count ? ""
            : _selected < _short.Count ? _short[_selected]
            : _items[_selected];
        if (_popup.IsOpen)
        {
            FillList();
        }
    }

    private Brush Brush(string key) => Tokens.Brush(key);
}
