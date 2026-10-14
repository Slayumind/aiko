using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// What the place of the conversation draws: the feed and whether Claude Code works now.
sealed record TalkView(
    IReadOnlyList<FeedItem> Items,
    bool Busy,
    bool HasOpenCard,
    PermissionMode Mode,
    string Model,
    string Environment);

/// The conversation with Claude Code: the feed above, the field below (D-294, D-302).
///
/// The feed is a virtual list: a row is built only when it comes into view, so a long
/// conversation stays light (spike 2026-10-13, 2000 rows under software rendering). The panel
/// draws a TalkView and reports what the person did; the host decides the rest.
sealed class TalkPanel : UserControl
{
    /// The rows of the feed: the items of the core, and the line "working" after them while a turn
    /// goes on (D-308).
    private readonly ObservableCollection<object> _items = [];
    private readonly ListBox _feed;
    private readonly TextBox _input = new();
    private readonly TextBlock _empty;
    private readonly Button _stop;
    private readonly TextBlock _mode = TerminalPanel.Mono("", "Muted");
    private readonly TextBlock _where = TerminalPanel.Mono("", "Muted");
    private TalkView? _view;

    public TalkPanel()
    {
        Focusable = false;

        _feed = new ListBox
        {
            ItemsSource = _items,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Focusable = false,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new DataTemplate { VisualTree = RowFactory() },
            ItemContainerStyle = BareItem(),
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_feed, ScrollBarVisibility.Disabled);
        ScrollViewer.SetCanContentScroll(_feed, true);
        VirtualizingPanel.SetIsVirtualizing(_feed, true);
        VirtualizingPanel.SetVirtualizationMode(_feed, VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(_feed, ScrollUnit.Pixel);

        _empty = TerminalPanel.Text(Strings.TerminalEmptyTalk, "TextName", "Muted", FontWeights.Normal);
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.TextWrapping = TextWrapping.Wrap;

        TerminalPanel.Field(_input);
        _input.Background = Brushes.Transparent;
        _input.BorderThickness = new Thickness(0);
        _input.Padding = new Thickness(0);
        _input.AcceptsReturn = true;
        _input.TextWrapping = TextWrapping.Wrap;
        _input.MinHeight = 24;
        _input.MaxHeight = 200;
        _input.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _input.VerticalContentAlignment = VerticalAlignment.Top;
        _input.PreviewKeyDown += OnInputKey;

        _stop = StopButton(() => StopAsked?.Invoke());

        var placeholder = TerminalPanel.Text(Strings.TerminalDescribeTask, "TextName", "Muted", FontWeights.Normal);
        placeholder.Margin = new Thickness(2, 1, 0, 0);
        placeholder.VerticalAlignment = VerticalAlignment.Top;
        placeholder.IsHitTestVisible = false;
        _input.TextChanged += (_, _) => placeholder.Visibility = _input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        // The stop button stands where the paper clip will be, at the right of the field (D-308).
        var field = new DockPanel();
        DockPanel.SetDock(_stop, Dock.Right);
        field.Children.Add(_stop);
        var text = new Grid();
        text.Children.Add(_input);
        text.Children.Add(placeholder);
        field.Children.Add(text);

        var compose = new Border
        {
            Background = Tokens.Brush("Surface"),
            BorderBrush = Tokens.Brush("InputLine"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 10, 0, 0),
            Cursor = Cursors.IBeam,
            Child = field,
        };
        compose.MouseLeftButtonDown += (_, _) => _input.Focus();

        // "mode · model · environment" under the field. The mode changes with a click or
        // Shift+Tab, in the order of Claude Code (D-302).
        _mode.Cursor = Cursors.Hand;
        _mode.ToolTip = "Shift+Tab";
        _mode.MouseLeftButtonUp += (_, _) => ModeAsked?.Invoke();
        var status = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(14, 6, 0, 14) };
        status.Children.Add(_mode);
        status.Children.Add(_where);

        var bottom = new StackPanel();
        bottom.Children.Add(compose);
        bottom.Children.Add(status);

        var middle = new Grid();
        middle.Children.Add(_feed);
        middle.Children.Add(_empty);

        var all = new DockPanel { Margin = new Thickness(24, 0, 24, 0), MaxWidth = 860 };
        DockPanel.SetDock(bottom, Dock.Bottom);
        all.Children.Add(bottom);
        all.Children.Add(middle);
        Content = all;
    }

    public event Action<string>? SendAsked;
    public event Action? StopAsked;
    public event Action? ModeAsked;
    public event Action<string, CardChoice>? Chosen;

    /// The field takes the keyboard, for a session the person just went into.
    public void FocusInput() => _input.Focus();

    public void Show(TalkView view)
    {
        var atEnd = AtEnd();
        _view = view;

        List<object> rows = [.. view.Items];
        if (view.Busy && !view.HasOpenCard)
        {
            rows.Add(WorkingMark.Row);
        }

        // Rows that did not change keep their place and their control; the core gives the same
        // record for an item nothing happened to.
        for (var at = 0; at < rows.Count; at++)
        {
            if (at >= _items.Count)
            {
                _items.Add(rows[at]);
            }
            else if (!ReferenceEquals(_items[at], rows[at]) && !Equals(_items[at], rows[at]))
            {
                _items[at] = rows[at];
            }
        }

        while (_items.Count > rows.Count)
        {
            _items.RemoveAt(_items.Count - 1);
        }

        _empty.Visibility = view.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _stop.Visibility = view.Busy ? Visibility.Visible : Visibility.Collapsed;
        _mode.Text = PermissionModes.Name(view.Mode);
        _where.Text = string.Concat(view.Model.Length > 0 ? " · " + view.Model : "", " · ", view.Environment);

        if (atEnd && _items.Count > 0)
        {
            Dispatcher.BeginInvoke(() => _feed.ScrollIntoView(_items[^1]), System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    internal void Choose(string requestId, CardChoice choice) => Chosen?.Invoke(requestId, choice);

    /// The word of a step, the one the list of sessions uses for it too.
    internal static string StepWord(StepKind kind, string label) => kind switch
    {
        StepKind.Read => Strings.TerminalStepRead,
        StepKind.Search => Strings.TerminalStepSearch,
        StepKind.Edit => Strings.TerminalStepEdit,
        StepKind.Run => Strings.TerminalStepRun,
        StepKind.Agent => Strings.TerminalStepAgent,
        StepKind.Web => Strings.TerminalStepWeb,
        _ => label,
    };

    /// A small square button with a square in it, as in the mockup of a turn that works.
    private static Button StopButton(Action click)
    {
        var square = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(2),
            Background = Tokens.Brush("Ink"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var hover = new Border { CornerRadius = new CornerRadius(8), Background = Tokens.Brush("HoverLayer"), Opacity = 0 };
        var face = new Grid { Width = 26, Height = 26 };
        face.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(0x0B, 0xFF, 0xFF, 0xFF)),
            BorderBrush = Tokens.Brush("InputLine"),
            BorderThickness = new Thickness(1),
        });
        face.Children.Add(hover);
        face.Children.Add(square);

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        var button = new Button
        {
            Content = face,
            ToolTip = Strings.TerminalStop,
            Cursor = Cursors.Hand,
            Focusable = false,
            Margin = new Thickness(10, -1, 0, -1),
            VerticalAlignment = VerticalAlignment.Top,
            Template = new ControlTemplate(typeof(Button)) { VisualTree = presenter },
        };
        button.MouseEnter += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(1, Motion.Or0(Motion.Hover), Motion.Standard));
        button.MouseLeave += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(0, Motion.Or0(Motion.Hover), Motion.Standard));
        button.Click += (_, _) => click();
        return button;
    }

    /// Enter sends, Shift+Enter is a new line, Esc stops the work (D-294).
    private void OnInputKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            var text = _input.Text.Trim();
            if (text.Length > 0)
            {
                _input.Clear();
                SendAsked?.Invoke(text);
            }
        }
        else if (e.Key == Key.Escape && _view is { Busy: true })
        {
            e.Handled = true;
            StopAsked?.Invoke();
        }
        else if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            e.Handled = true;
            ModeAsked?.Invoke();
        }
    }

    /// Whether the feed shows its last row, so a new one should come into view too. Someone who
    /// scrolled up to read stays where they are.
    private bool AtEnd()
    {
        var scroll = FindScroll(_feed);
        return scroll is null || scroll.VerticalOffset >= scroll.ScrollableHeight - 2;
    }

    private static ScrollViewer? FindScroll(DependencyObject parent)
    {
        for (var at = 0; at < VisualTreeHelper.GetChildrenCount(parent); at++)
        {
            var child = VisualTreeHelper.GetChild(parent, at);
            if (child is ScrollViewer scroll)
            {
                return scroll;
            }

            if (FindScroll(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static FrameworkElementFactory RowFactory()
    {
        var row = new FrameworkElementFactory(typeof(FeedRow));
        row.SetBinding(FeedRow.ItemProperty, new Binding());
        return row;
    }

    /// A row of the feed is not a choice: no highlight, no focus, no padding of the list.
    private static Style BareItem()
    {
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        return new Style(typeof(ListBoxItem))
        {
            Setters =
            {
                new Setter(FocusableProperty, false),
                new Setter(PaddingProperty, new Thickness(0)),
                new Setter(TemplateProperty, new ControlTemplate(typeof(ListBoxItem)) { VisualTree = presenter }),
            },
        };
    }
}

/// The last row of the feed while a turn goes on. It is not an item of the core: the core says
/// only that the session is busy.
sealed class WorkingMark
{
    public static readonly WorkingMark Row = new();

    private WorkingMark()
    {
    }
}

/// One row of the feed, built from its item whenever the item changes.
sealed class FeedRow : ContentControl
{
    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item), typeof(object), typeof(FeedRow), new PropertyMetadata(null, (row, _) => ((FeedRow)row).Build()));

    /// A FeedItem, or the WorkingMark.
    public object? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    private void Build()
    {
        Content = Item switch
        {
            WorkingMark => Working(),
            FeedItem.OwnerMessage message => Message(message),
            FeedItem.Answer answer => AnswerView.For(answer.Text),
            FeedItem.Step { Line.Hidden: false } step => StepRow(step),
            FeedItem.Card { State: RequestState.Open } card => Card(card.Request),
            FeedItem.Notice notice => Notice(notice),
            _ => null,
        };
    }

    /// The word "working" and three dots that fade to the right.
    private static UIElement Working()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 12) };
        row.Children.Add(TerminalPanel.Mono(Strings.TerminalWorking, "Muted"));
        foreach (var opacity in new[] { 1, 0.6, 0.3 })
        {
            row.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 4,
                Height = 4,
                Margin = new Thickness(opacity == 1 ? 8 : 3, 1, 0, 0),
                Fill = Tokens.Brush("Muted"),
                Opacity = opacity,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        return row;
    }

    /// The words of the owner: a block on the right, at most 70% of the feed wide (D-299). A queued
    /// message is dimmed and says so under the block (D-308).
    private static UIElement Message(FeedItem.OwnerMessage message)
    {
        var block = new Border
        {
            Background = Tokens.Brush("Surface"),
            BorderBrush = Tokens.Brush("Hairline"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14, 14, 4, 14),
            Padding = new Thickness(14, 10, 14, 10),
            HorizontalAlignment = HorizontalAlignment.Right,
            Opacity = message.Queued ? 0.55 : 1,
            Child = Words(message.Text, "Ink", new Thickness(0)),
        };

        var column = new StackPanel();
        column.Children.Add(block);
        if (message.Queued)
        {
            var queued = TerminalPanel.Mono(Strings.TerminalQueued, "Muted");
            queued.Margin = new Thickness(0, 4, 0, 0);
            queued.HorizontalAlignment = HorizontalAlignment.Right;
            column.Children.Add(queued);
        }

        var row = new Grid { Margin = new Thickness(0, 12, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(7, GridUnitType.Star) });
        Grid.SetColumn(column, 1);
        row.Children.Add(column);
        return row;
    }

    /// Text that can be selected and copied, but not typed into.
    private static TextBox Words(string text, string tone, Thickness margin) => new()
    {
        Text = text,
        IsReadOnly = true,
        IsReadOnlyCaretVisible = false,
        TextWrapping = TextWrapping.Wrap,
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(0),
        Margin = margin,
        FontFamily = Tokens.Get<FontFamily>("Sans"),
        FontSize = Tokens.Get<double>("TextName"),
        Foreground = Tokens.Brush(tone),
        SelectionBrush = Tokens.Brush("Muted"),
    };

    /// A step is one line behind a rule on the left; the rules of steps in a row join into one
    /// line, so the rows keep no gap between them (D-299).
    private static UIElement StepRow(FeedItem.Step step)
    {
        var tone = step.State == StepState.Failed ? "Destructive" : "Muted";
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var verb = TerminalPanel.Text(TalkPanel.StepWord(step.Line.Kind, step.Line.Label), "TextNumber", tone, FontWeights.Normal);
        verb.MinWidth = 52;
        row.Children.Add(verb);

        var subject = TerminalPanel.Mono(step.Line.Subject, step.State == StepState.Failed ? "Destructive" : "Ink");
        subject.FontSize = 12.5;
        subject.Margin = new Thickness(10, 0, 0, 0);
        subject.MaxWidth = 560;
        subject.TextTrimming = TextTrimming.CharacterEllipsis;
        row.Children.Add(subject);

        if (step.Line.Added > 0 || step.Line.Removed > 0)
        {
            var added = TerminalPanel.Mono($"+{step.Line.Added}", "Positive");
            added.FontSize = 12.5;
            added.Margin = new Thickness(8, 0, 0, 0);
            var removed = TerminalPanel.Mono($"−{step.Line.Removed}", "Destructive");
            removed.FontSize = 12.5;
            removed.Margin = new Thickness(4, 0, 0, 0);
            row.Children.Add(added);
            row.Children.Add(removed);
        }

        if (step.State == StepState.Running)
        {
            var dot = TerminalPanel.Dot(filled: false);
            dot.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(dot);
        }

        return new Border
        {
            BorderBrush = Tokens.Brush("Raised"),
            BorderThickness = new Thickness(2, 0, 0, 0),
            Padding = new Thickness(14, 0, 0, 0),
            MinHeight = 24,
            Child = row,
        };
    }

    private UIElement Card(RequestCard request)
    {
        var column = new StackPanel();

        var word = TalkPanel.StepWord(request.Step.Kind, request.Step.Label);
        var title = TerminalPanel.Text(Capital(word), "TextNumber", "Ink", FontWeights.Medium);
        column.Children.Add(title);

        if (request.Kind == CardKind.Plan)
        {
            column.Children.Add(Words(request.Plan, "Ink", new Thickness(0, 8, 0, 0)));
        }
        else if (request.Kind == CardKind.Permission && request.Step.Subject.Length > 0)
        {
            var subject = Words(request.Step.Subject, "Ink", new Thickness(0, 6, 0, 0));
            subject.FontFamily = Tokens.Get<FontFamily>("Mono");
            subject.FontSize = Tokens.Get<double>("TextSmall");
            column.Children.Add(subject);
        }

        if (request.Kind == CardKind.Question)
        {
            column.Children.Add(Questions(request));
        }
        else
        {
            column.Children.Add(Buttons(request));
        }

        return new Border
        {
            Background = Tokens.Brush("Surface"),
            BorderBrush = Tokens.Brush("InputLine"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 8, 0, 8),
            Child = column,
        };
    }

    private UIElement Buttons(RequestCard request)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        row.Children.Add(Spaced(TerminalPanel.Flat(Strings.TerminalAllow, () => Choose(request, new CardChoice.Allow()), ghost: false, main: true)));

        var second = request.Second switch
        {
            SecondChoice.EditsInSession => Strings.TerminalAllowEdits,
            SecondChoice.AlwaysAllow => Strings.TerminalAllowInProject,
            _ => null,
        };
        if (second is not null)
        {
            row.Children.Add(Spaced(TerminalPanel.Flat(second, () => Choose(request, new CardChoice.Second()), ghost: false)));
        }

        row.Children.Add(Spaced(TerminalPanel.Flat(Strings.TerminalDeny, () => Choose(request, new CardChoice.Deny("")), ghost: true)));
        return row;
    }

    /// One button per option. With several questions the card waits until each has its answer.
    private UIElement Questions(RequestCard request)
    {
        var picked = new string?[request.Questions.Count];
        var column = new StackPanel();

        for (var at = 0; at < request.Questions.Count; at++)
        {
            var question = request.Questions[at];
            var text = TerminalPanel.Text(question.Text, "TextNumber", "Ink", FontWeights.Normal);
            text.TextWrapping = TextWrapping.Wrap;
            text.Margin = new Thickness(0, 10, 0, 6);
            column.Children.Add(text);

            var options = new WrapPanel();
            var index = at;
            foreach (var option in question.Options)
            {
                Button? button = null;
                button = TerminalPanel.Flat(option.Label, () =>
                {
                    picked[index] = option.Label;
                    foreach (var other in options.Children.OfType<Button>())
                    {
                        other.Opacity = ReferenceEquals(other, button) ? 1 : 0.5;
                    }

                    if (picked.All(answer => answer is not null))
                    {
                        Choose(request, new CardChoice.Answers([.. picked.Select(answer => (IReadOnlyList<string>)[answer!])]));
                    }
                }, ghost: false);
                button.ToolTip = option.Description.Length > 0 ? option.Description : null;
                options.Children.Add(Spaced(button));
            }

            column.Children.Add(options);
        }

        var deny = TerminalPanel.Flat(Strings.TerminalDeny, () => Choose(request, new CardChoice.Deny("")), ghost: true);
        deny.Margin = new Thickness(0, 10, 0, 0);
        deny.HorizontalAlignment = HorizontalAlignment.Left;
        column.Children.Add(deny);
        return column;
    }

    private static UIElement Notice(FeedItem.Notice notice)
    {
        var text = notice.Kind switch
        {
            NoticeKind.Interrupted => Strings.TerminalTurnStopped,
            NoticeKind.Compacted => Strings.TerminalCompacted,
            NoticeKind.Failed => notice.Text.Length > 0 ? notice.Text : Strings.TerminalFailed,
            NoticeKind.Output => notice.Text,
            _ => "",
        };
        if (text.Length == 0)
        {
            return new Border();
        }

        var words = Words(text, notice.Kind == NoticeKind.Failed ? "Destructive" : "Muted", new Thickness(0, 4, 0, 4));
        if (notice.Kind == NoticeKind.Output)
        {
            words.FontFamily = Tokens.Get<FontFamily>("Mono");
            words.FontSize = Tokens.Get<double>("TextSmall");
        }

        return words;
    }

    private void Choose(RequestCard request, CardChoice choice) =>
        FindPanel()?.Choose(request.RequestId, choice);

    private TalkPanel? FindPanel()
    {
        DependencyObject? at = this;
        while (at is not null and not TalkPanel)
        {
            at = VisualTreeHelper.GetParent(at);
        }

        return at as TalkPanel;
    }

    private static Button Spaced(Button button)
    {
        button.Margin = new Thickness(0, 0, 8, 6);
        return button;
    }

    private static string Capital(string word) =>
        word.Length == 0 ? word : char.ToUpper(word[0], CultureInfo.CurrentCulture) + word[1..];
}
