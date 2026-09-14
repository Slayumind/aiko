using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Aiko.Core;

namespace Aiko.App;

/// One item of the wizard checklist: a header that says what it is and how far along, and a body
/// that unfolds under it.
[ContentProperty(nameof(Body))]
sealed class ChecklistRow : UserControl
{
    private readonly Border _frame = new();
    private readonly Border _hover = new();
    private readonly Grid _mark = new() { Width = 16, Height = 16 };
    private readonly ScaleTransform _markScale = new(1, 1);
    private readonly TextBlock _title = new();
    private readonly TextBlock _status = new();
    private readonly RotateTransform _chevronTurn = new();
    private readonly RevealPanel _reveal = new();
    private readonly ContentPresenter _body = new() { Margin = new Thickness(26, 10, 0, 2) };
    private ItemState _state = ItemState.Locked;

    /// Built at once, not on Loaded: a snapshot draws the row without ever putting it in a window.
    public ChecklistRow()
    {
        Build();
    }

    /// Raised when the person clicks the header of an item that is not locked.
    public event Action<ChecklistRow>? HeaderClicked;

    public ChecklistItem Item { get; set; }

    public string Title
    {
        get => _title.Text;
        set => _title.Text = value;
    }

    public string Status
    {
        get => _status.Text;
        set => _status.Text = value;
    }

    public object? Body
    {
        get => _body.Content;
        set => _body.Content = value;
    }

    public bool IsOpen
    {
        get => _reveal.IsOpen;
        set
        {
            _reveal.IsOpen = value;
            _chevronTurn.BeginAnimation(RotateTransform.AngleProperty, Motion.To(value ? 180 : 0, Motion.Expand, Motion.Standard));
        }
    }

    public ItemState State
    {
        get => _state;
        set
        {
            var wasDone = _state is ItemState.Done;
            _state = value;

            DrawMark();

            // The tick lands with a small spring, only when it is earned while the window is open.
            if (!wasDone && value is ItemState.Done && IsLoaded && Motion.IsOn)
            {
                var land = new DoubleAnimation(0.6, 1, Motion.Settle) { EasingFunction = Motion.Spring };
                _markScale.BeginAnimation(ScaleTransform.ScaleXProperty, land);
                _markScale.BeginAnimation(ScaleTransform.ScaleYProperty, land);
            }

            Opacity = value is ItemState.Locked ? 0.55 : 1;
            _frame.Cursor = value is ItemState.Locked ? Cursors.Arrow : Cursors.Hand;
        }
    }

    private void Build()
    {
        _title.FontFamily = Tokens.Get<FontFamily>("Sans");
        _title.FontSize = Tokens.Get<double>("TextSmall");
        _title.Foreground = Tokens.Brush("Ink");
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.Margin = new Thickness(10, 0, 8, 0);
        _title.TextTrimming = TextTrimming.CharacterEllipsis;

        _status.FontFamily = Tokens.Get<FontFamily>("Mono");
        _status.FontSize = Tokens.Get<double>("TextTiny");
        _status.Foreground = Tokens.Brush("Muted");
        _status.VerticalAlignment = VerticalAlignment.Center;

        var chevron = new Path
        {
            Data = Geometry.Parse("M0,0 L4,4 L8,0"),
            Stroke = Tokens.Brush("Muted"),
            StrokeThickness = 1.4,
            Width = 8,
            Height = 5,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _chevronTurn,
        };

        _mark.RenderTransformOrigin = new Point(0.5, 0.5);
        _mark.RenderTransform = _markScale;
        _mark.VerticalAlignment = VerticalAlignment.Center;

        var header = new Grid { MinHeight = 22 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_title, 1);
        Grid.SetColumn(_status, 2);
        Grid.SetColumn(chevron, 3);
        header.Children.Add(_mark);
        header.Children.Add(_title);
        header.Children.Add(_status);
        header.Children.Add(chevron);
        header.Background = Brushes.Transparent;
        header.MouseLeftButtonUp += (_, _) =>
        {
            if (_state is not ItemState.Locked)
            {
                HeaderClicked?.Invoke(this);
            }
        };

        _reveal.Child = _body;

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(_reveal);

        _hover.CornerRadius = new CornerRadius(10);
        _hover.Background = Tokens.Brush("HoverLayer");
        _hover.Opacity = 0;
        _hover.IsHitTestVisible = false;

        var layers = new Grid();
        layers.Children.Add(_hover);
        layers.Children.Add(new Border { Padding = new Thickness(12, 10, 12, 10), Child = stack });

        _frame.CornerRadius = new CornerRadius(10);
        _frame.BorderBrush = Tokens.Brush("Hairline");
        _frame.BorderThickness = new Thickness(1);
        _frame.Margin = new Thickness(0, 0, 0, 6);
        _frame.Child = layers;

        header.MouseEnter += (_, _) =>
        {
            if (_state is not ItemState.Locked)
            {
                _hover.BeginAnimation(OpacityProperty, Motion.To(1, Motion.Hover, Motion.Standard));
            }
        };
        header.MouseLeave += (_, _) => _hover.BeginAnimation(OpacityProperty, Motion.To(0, Motion.Hover, Motion.Standard));

        Content = _frame;
        State = _state;
    }

    private void DrawMark()
    {
        _mark.Children.Clear();

        switch (_state)
        {
            case ItemState.Done:
                _mark.Children.Add(new Ellipse { Fill = Tokens.Brush("Positive") });
                _mark.Children.Add(new Path
                {
                    Data = Geometry.Parse("M4.5,8.2 L7,10.6 L11.6,5.6"),
                    Stroke = Tokens.Brush("Background"),
                    StrokeThickness = 1.8,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                });
                break;

            case ItemState.Skipped:
                _mark.Children.Add(new Ellipse { Stroke = Tokens.Brush("InputLine"), StrokeThickness = 1.4 });
                _mark.Children.Add(new Path { Data = Geometry.Parse("M5,8 L11,8"), Stroke = Tokens.Brush("Muted"), StrokeThickness = 1.4 });
                break;

            case ItemState.Locked:
                _mark.Children.Add(new Path
                {
                    Data = Geometry.Parse("M5,7.5 L5,5.8 A3,3 0 0 1 11,5.8 L11,7.5 M3.8,7.5 L12.2,7.5 L12.2,13 L3.8,13 Z"),
                    Stroke = Tokens.Brush("Muted"),
                    StrokeThickness = 1.2,
                });
                break;

            default:
                _mark.Children.Add(new Ellipse { Stroke = Tokens.Brush("Muted"), StrokeThickness = 1.4 });
                break;
        }
    }
}
