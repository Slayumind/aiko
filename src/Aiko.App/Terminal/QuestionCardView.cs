using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The card of questions in the feed (D-330, AIKO-327): the strip of steps, one question, its
/// options in two columns, the preview under them, Back and Next. The card takes the keyboard when
/// it comes; QuestionFlow in the core decides what every key and click does.
sealed class QuestionCardView : Border
{
    /// Narrower than this, the options stand in one column.
    private const double NarrowWidth = 600;

    /// The answers so far stay with the request while the feed draws its row again.
    private static readonly ConditionalWeakTable<RequestCard, StrongBox<QuestionFlow>> Kept = new();

    private readonly RequestCard _request;
    private readonly Action<CardChoice> _choose;
    private readonly StackPanel _column = new();
    private readonly StrongBox<QuestionFlow> _flow;
    private TextBox? _own;
    private FrameworkElement? _next;
    private bool _narrow;

    public QuestionCardView(RequestCard request, Action<CardChoice> choose)
    {
        _request = request;
        _choose = choose;
        _flow = Kept.GetValue(request, card => new StrongBox<QuestionFlow>(QuestionFlow.Start(card.Questions)));

        Background = Tokens.Brush("Surface");
        BorderBrush = Tokens.Brush("InputLine");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(12);
        Padding = new Thickness(16, 14, 16, 14);
        Margin = new Thickness(0, 8, 0, 8);
        Focusable = true;
        FocusVisualStyle = null;
        Child = _column;

        KeyDown += OnKey;
        // The frame of the keyboard follows the focus. Focus that comes straight into the field of
        // Other needs no frame, and drawing again would take the field away from under the click.
        IsKeyboardFocusWithinChanged += (_, _) =>
        {
            if (_own is null || !ReferenceEquals(Keyboard.FocusedElement, _own))
            {
                Draw();
            }
        };
        SizeChanged += (_, size) =>
        {
            var narrow = size.NewSize.Width < NarrowWidth;
            if (narrow != _narrow)
            {
                _narrow = narrow;
                Draw();
            }
        };

        // The card takes the keyboard when it comes, as the console does (D-330).
        Loaded += (_, _) => Keyboard.Focus(this);
        Draw();
    }

    private QuestionFlow Flow => _flow.Value!;

    /// How many options stand side by side now: the arrows move over the grid as it is drawn.
    private int Columns => _narrow ? 1 : 2;

    /// Puts a card on a question before it is drawn: snapshots show a card half way through.
    public static void Preset(RequestCard request, QuestionFlow flow) =>
        Kept.AddOrUpdate(request, new StrongBox<QuestionFlow>(flow));

    /// Whether a key comes from inside a card of questions: the window leaves it to the card.
    public static bool Holds(object source)
    {
        for (var at = source as DependencyObject; at is not null; at = ParentOf(at))
        {
            if (at is QuestionCardView)
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? ParentOf(DependencyObject child) =>
        child is Visual ? VisualTreeHelper.GetParent(child) : LogicalTreeHelper.GetParent(child);

    // ---- what a key or a click does ----

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, _own) || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        QuestionStep? step = e.Key switch
        {
            Key.Up => Flow.Press(QuestionKey.Up, Columns),
            Key.Down => Flow.Press(QuestionKey.Down, Columns),
            Key.Left => Flow.Press(QuestionKey.Left, Columns),
            Key.Right => Flow.Press(QuestionKey.Right, Columns),
            Key.Space => Flow.Press(QuestionKey.Space),
            Key.Enter => Flow.Press(QuestionKey.Enter),
            Key.Escape => Flow.Press(QuestionKey.Escape),
            >= Key.D1 and <= Key.D9 => Flow.Number(e.Key - Key.D0),
            >= Key.NumPad1 and <= Key.NumPad9 => Flow.Number(e.Key - Key.NumPad0),
            _ => null,
        };

        if (step is not null)
        {
            e.Handled = true;
            Apply(step);
        }
    }

    private void Apply(QuestionStep step)
    {
        _flow.Value = step.Flow;
        switch (step.Outcome)
        {
            case QuestionOutcome.Answer:
                _choose(new CardChoice.Answers(Flow.Answers()));
                return;
            case QuestionOutcome.Deny:
                _choose(new CardChoice.Deny(""));
                return;
        }

        Draw();
        if (step.Outcome == QuestionOutcome.WriteOwn && _own is not null)
        {
            // Words already there are to be changed, so the caret goes after them.
            _own.CaretIndex = _own.Text.Length;
            Keyboard.Focus(_own);
        }
        else if (!IsKeyboardFocusWithin || ReferenceEquals(Keyboard.FocusedElement, _own))
        {
            Keyboard.Focus(this);
        }
    }

    // ---- drawing ----

    private void Draw()
    {
        var flow = Flow;
        _column.Children.Clear();
        _own = null;

        if (flow.Questions.Count > 1)
        {
            _column.Children.Add(Strip(flow));
        }

        var ask = TerminalPanel.Text(flow.Open.Text, "TextName", "Ink", FontWeights.SemiBold);
        ask.TextWrapping = TextWrapping.Wrap;
        ask.Margin = new Thickness(0, flow.Questions.Count > 1 ? 10 : 0, 0, 0);
        _column.Children.Add(ask);

        if (flow.Open.MultiSelect)
        {
            var many = TerminalPanel.Mono(Strings.TerminalQuestionMany, "Muted");
            many.Margin = new Thickness(0, 6, 0, 0);
            many.HorizontalAlignment = HorizontalAlignment.Left;
            _column.Children.Add(many);
        }

        _column.Children.Add(Options(flow));

        if (flow.Preview is { } preview)
        {
            _column.Children.Add(Preview(preview));
        }

        _column.Children.Add(Buttons(flow));
    }

    /// A short bar per question, green when answered, light for the open one, and the words
    /// "question 2 of 4 · header" after them. A click on a bar opens its question.
    private UIElement Strip(QuestionFlow flow)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        for (var at = 0; at < flow.Questions.Count; at++)
        {
            var question = at;
            var bar = new Border
            {
                Width = 18,
                Height = 3,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                Background = Tokens.Brush(flow.Mark(at) switch
                {
                    QuestionMark.Answered => "Positive",
                    QuestionMark.Open => "Ink",
                    _ => "Raised",
                }),
            };
            bar.MouseLeftButtonUp += (_, _) => Apply(new QuestionStep(Flow.Show(question), QuestionOutcome.None));
            row.Children.Add(bar);
        }

        var words = string.Format(CultureInfo.CurrentCulture, Strings.TerminalQuestionStep, flow.At + 1, flow.Questions.Count);
        if (flow.Open.Header.Length > 0)
        {
            words += " · " + flow.Open.Header;
        }

        row.Children.Add(TerminalPanel.Mono(words, "Muted"));
        return row;
    }

    /// The options and Other, in two columns, or in one in a narrow window.
    private UIElement Options(QuestionFlow flow)
    {
        var columns = Columns;
        var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        for (var column = 0; column < columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var row = 0; row <= flow.OwnRow; row++)
        {
            if (row % columns == 0)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var option = Option(flow, row);
            Grid.SetRow(option, row / columns);
            Grid.SetColumn(option, row % columns);
            option.Margin = new Thickness(row % columns == 0 ? 0 : 3, 0, row % columns == columns - 1 ? 0 : 3, 6);
            grid.Children.Add(option);
        }

        return grid;
    }

    /// One row: the box or the ring, the label with its number, the description under it. The row
    /// the keyboard stands on has a thicker, lighter frame while the card has the keyboard.
    private FrameworkElement Option(QuestionFlow flow, int row)
    {
        var own = row == flow.OwnRow;
        var label = own ? Strings.TerminalQuestionOther : flow.Open.Options[row].Label;
        var description = own ? Strings.TerminalQuestionOtherHint : flow.Open.Options[row].Description;
        var picked = flow.IsPicked(row);

        var inside = new Grid();
        inside.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        inside.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inside.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inside.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inside.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var marker = flow.Open.MultiSelect ? Box(picked) : Ring(picked);
        inside.Children.Add(marker);

        var name = TerminalPanel.Text(label, "TextName", "Ink", FontWeights.Medium);
        name.TextWrapping = TextWrapping.Wrap;
        name.VerticalAlignment = VerticalAlignment.Top;
        name.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(name, 1);
        inside.Children.Add(name);

        var number = TerminalPanel.Mono((row + 1).ToString(CultureInfo.InvariantCulture), "Muted");
        number.VerticalAlignment = VerticalAlignment.Top;
        number.Margin = new Thickness(10, 2, 0, 0);
        Grid.SetColumn(number, 2);
        inside.Children.Add(number);

        // Other, chosen or with the keyboard on it, holds the field for the words in place of its hint.
        var writing = own && (flow.Focus == row || picked);
        if (writing)
        {
            var field = OwnField(flow);
            Grid.SetRow(field, 1);
            Grid.SetColumn(field, 1);
            Grid.SetColumnSpan(field, 2);
            inside.Children.Add(field);
        }
        else if (description.Length > 0)
        {
            var more = TerminalPanel.Text(description, "TextNumber", "Muted", FontWeights.Normal);
            more.TextWrapping = TextWrapping.Wrap;
            more.Margin = new Thickness(10, 2, 0, 0);
            Grid.SetRow(more, 1);
            Grid.SetColumn(more, 1);
            Grid.SetColumnSpan(more, 2);
            inside.Children.Add(more);
        }

        var face = new Border
        {
            Background = Tokens.Brush("Background"),
            BorderBrush = Tokens.Brush(picked ? "InputLine" : "Hairline"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 8, 10, 8),
            Child = inside,
        };

        // One frame per tile (check of 2026-10-23). The frame of the keyboard lies over the
        // tile's own one instead of around it; it takes no room, so the words never move.
        var ring = new Grid { Cursor = Cursors.Hand };
        ring.Children.Add(face);
        if (row == flow.Focus && IsKeyboardFocusWithin && !ReferenceEquals(Keyboard.FocusedElement, _own))
        {
            ring.Children.Add(new Border
            {
                BorderBrush = Tokens.Brush("Muted"),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                IsHitTestVisible = false,
            });
        }

        ring.MouseLeftButtonUp += (_, e) =>
        {
            // A click in the field of Other is for the words, not for the tile.
            if (_own is not null && e.OriginalSource is Visual clicked && (ReferenceEquals(clicked, _own) || _own.IsAncestorOf(clicked)))
            {
                return;
            }

            e.Handled = true;
            Keyboard.Focus(this);
            Apply(Flow.Click(row));
        };
        return ring;
    }

    /// The box of a question with several answers; ticked, it is filled and holds the check.
    private static FrameworkElement Box(bool ticked)
    {
        var box = new Grid { Width = 14, Height = 14, Margin = new Thickness(0, 3, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        box.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            BorderBrush = ticked ? Brushes.Transparent : Tokens.Brush("Muted"),
            BorderThickness = new Thickness(1),
            Background = ticked ? Tokens.Brush("Ink") : Brushes.Transparent,
        });
        if (ticked)
        {
            box.Children.Add(new Path
            {
                Data = Geometry.Parse(SiteIcons.Check),
                Fill = Tokens.Brush("Background"),
                Stretch = Stretch.Uniform,
                Width = 10,
                Height = 10,
            });
        }

        return box;
    }

    /// The ring of a question with one answer; chosen, its edge grows thick.
    private static FrameworkElement Ring(bool chosen) => new Ellipse
    {
        Width = 14,
        Height = 14,
        Margin = new Thickness(0, 3, 0, 0),
        VerticalAlignment = VerticalAlignment.Top,
        Stroke = Tokens.Brush(chosen ? "Ink" : "Muted"),
        StrokeThickness = chosen ? 4 : 1,
    };

    /// The person's own words, inside the tile of Other, in the place and the letters of its hint:
    /// no frame and no fill, only the caret. While it is empty the hint stays there in grey.
    /// Enter takes the words; Esc gives the keyboard back to the tiles.
    private FrameworkElement OwnField(QuestionFlow flow)
    {
        var field = new TextBox
        {
            Text = flow.Own[flow.At],
            FontFamily = Tokens.Get<FontFamily>("Sans"),
            FontSize = Tokens.Get<double>("TextNumber"),
            Foreground = Tokens.Brush("Ink"),
            CaretBrush = Tokens.Brush("Ink"),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            TextWrapping = TextWrapping.Wrap,
            FocusVisualStyle = null,
        };

        var hint = TerminalPanel.Text(Strings.TerminalQuestionOtherHint, "TextNumber", "Muted", FontWeights.Normal);
        hint.IsHitTestVisible = false;
        hint.VerticalAlignment = VerticalAlignment.Top;
        // The text of a TextBox stands two pixels in from its edge; the hint stands with it.
        hint.Margin = new Thickness(2, 0, 0, 0);
        hint.Visibility = field.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        field.TextChanged += (_, _) =>
        {
            _flow.Value = Flow.Write(field.Text);
            hint.Visibility = field.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

            // The words answer the question as they are typed; drawing the card again would take
            // the field away from under the caret, so only Next follows them.
            ShowNext();
        };
        field.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                Keyboard.Focus(this);
                Apply(Flow.Confirm());
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Keyboard.Focus(this);
                Draw();
            }
        };
        _own = field;

        // Two pixels left, so the words start under the label above them.
        var both = new Grid { Margin = new Thickness(8, 2, 0, 0) };
        both.Children.Add(field);
        both.Children.Add(hint);
        return both;
    }

    /// The preview of an option: monospaced, as it was written, scrolled sideways when wide.
    private static UIElement Preview(string preview) => new Border
    {
        Background = Tokens.Brush("Background"),
        BorderBrush = Tokens.Brush("Hairline"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(10, 8, 10, 8),
        Margin = new Thickness(0, 4, 0, 0),
        Child = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            Content = new TextBlock
            {
                Text = preview.TrimEnd(),
                FontFamily = Tokens.Get<FontFamily>("Mono"),
                FontSize = Tokens.Get<double>("TextSmall"),
                LineHeight = 17,
                Foreground = Tokens.Brush("Ink"),
            },
        },
    };

    /// Back and Next (Answer on the last question) at the left, a quiet Deny at the right.
    private UIElement Buttons(QuestionFlow flow)
    {
        var row = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };

        var deny = TerminalPanel.Flat(Strings.TerminalDeny, () => _choose(new CardChoice.Deny("")), ghost: true);
        DockPanel.SetDock(deny, Dock.Right);
        var esc = KeyName("Esc");
        DockPanel.SetDock(esc, Dock.Right);
        row.Children.Add(esc);
        row.Children.Add(deny);

        if (flow.At > 0)
        {
            var back = TerminalPanel.Flat(Strings.TerminalQuestionBack, () => Apply(new QuestionStep(Flow.Back(), QuestionOutcome.None)), ghost: false);
            back.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(back);
        }

        var next = TerminalPanel.Flat(
            flow.IsLast ? Strings.TerminalQuestionAnswer : Strings.TerminalQuestionNext,
            () => Apply(Flow.Forward()),
            ghost: false,
            main: true);
        _next = next;
        ShowNext();
        row.Children.Add(next);
        row.Children.Add(KeyName("Enter"));
        return row;
    }

    /// Next (or Answer) can be pressed once the open question has its answer.
    private void ShowNext()
    {
        if (_next is null)
        {
            return;
        }

        _next.IsEnabled = Flow.IsAnswered(Flow.At);
        _next.Opacity = _next.IsEnabled ? 1 : 0.5;
    }

    private static TextBlock KeyName(string name)
    {
        var key = TerminalPanel.Mono(name, "Muted");
        key.Margin = new Thickness(8, 0, 8, 0);
        return key;
    }
}
