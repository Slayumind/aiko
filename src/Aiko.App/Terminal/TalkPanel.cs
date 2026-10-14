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
sealed record TalkView(IReadOnlyList<FeedItem> Items, bool Busy, bool HasOpenCard);

/// The conversation with Claude Code: the feed above, the field below (D-294, D-302).
///
/// The feed is a virtual list: a row is built only when it comes into view, so a long
/// conversation stays light (spike 2026-10-13, 2000 rows under software rendering). The panel
/// draws a TalkView and reports what the person did; the host decides the rest.
sealed class TalkPanel : UserControl
{
    private readonly ObservableCollection<FeedItem> _items = [];
    private readonly ListBox _feed;
    private readonly TextBox _input = new();
    private readonly TextBlock _empty;
    private readonly FrameworkElement _working;
    private readonly Button _stop;
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

        var dot = TerminalPanel.Dot(filled: true);
        var word = TerminalPanel.Mono(Strings.TerminalWorking, "Muted");
        word.Margin = new Thickness(8, 0, 0, 0);
        _working = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 6, 0, 10),
            Children = { dot, word },
        };

        TerminalPanel.Field(_input);
        _input.AcceptsReturn = true;
        _input.TextWrapping = TextWrapping.Wrap;
        _input.MinHeight = 44;
        _input.MaxHeight = 220;
        _input.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _input.VerticalContentAlignment = VerticalAlignment.Top;
        _input.Padding = new Thickness(11, 10, 11, 10);
        _input.PreviewKeyDown += OnInputKey;

        _stop = TerminalPanel.Flat(Strings.TerminalStop, () => StopAsked?.Invoke(), ghost: false);
        _stop.Margin = new Thickness(8, 0, 0, 0);
        _stop.VerticalAlignment = VerticalAlignment.Bottom;

        var placeholder = TerminalPanel.Text(Strings.TerminalDescribeTask, "TextName", "Muted", FontWeights.Normal);
        placeholder.Margin = new Thickness(14, 11, 14, 0);
        placeholder.VerticalAlignment = VerticalAlignment.Top;
        placeholder.IsHitTestVisible = false;
        _input.TextChanged += (_, _) => placeholder.Visibility = _input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        var field = new Grid();
        field.Children.Add(_input);
        field.Children.Add(placeholder);

        var inputRow = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(_stop, Dock.Right);
        inputRow.Children.Add(_stop);
        inputRow.Children.Add(field);

        var bottom = new StackPanel();
        bottom.Children.Add(_working);
        bottom.Children.Add(inputRow);

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
    public event Action<string, CardChoice>? Chosen;

    /// The field takes the keyboard, for a session the person just went into.
    public void FocusInput() => _input.Focus();

    public void Show(TalkView view)
    {
        var atEnd = AtEnd();
        _view = view;

        // Rows that did not change keep their place and their control; the core gives the same
        // record for an item nothing happened to.
        for (var at = 0; at < view.Items.Count; at++)
        {
            if (at >= _items.Count)
            {
                _items.Add(view.Items[at]);
            }
            else if (!ReferenceEquals(_items[at], view.Items[at]) && !Equals(_items[at], view.Items[at]))
            {
                _items[at] = view.Items[at];
            }
        }

        while (_items.Count > view.Items.Count)
        {
            _items.RemoveAt(_items.Count - 1);
        }

        _empty.Visibility = view.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _working.Visibility = view.Busy && !view.HasOpenCard ? Visibility.Visible : Visibility.Collapsed;
        _stop.Visibility = view.Busy ? Visibility.Visible : Visibility.Collapsed;

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

/// One row of the feed, built from its item whenever the item changes.
sealed class FeedRow : ContentControl
{
    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item), typeof(FeedItem), typeof(FeedRow), new PropertyMetadata(null, (row, _) => ((FeedRow)row).Build()));

    public FeedItem? Item
    {
        get => (FeedItem?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    private void Build()
    {
        Content = Item switch
        {
            FeedItem.OwnerMessage message => Message(message),
            FeedItem.Answer answer => Words(answer.Text, "Ink", new Thickness(0, 6, 0, 10)),
            FeedItem.Step { Line.Hidden: false } step => StepRow(step),
            FeedItem.Card { State: RequestState.Open } card => Card(card.Request),
            FeedItem.Notice notice => Notice(notice),
            _ => null,
        };
    }

    private static UIElement Message(FeedItem.OwnerMessage message)
    {
        var column = new StackPanel();
        column.Children.Add(Words(message.Text, "Ink", new Thickness(0)));
        if (message.Queued)
        {
            var queued = TerminalPanel.Mono(Strings.TerminalQueued, "Muted");
            queued.Margin = new Thickness(0, 4, 0, 0);
            column.Children.Add(queued);
        }

        return new Border
        {
            Background = Tokens.Brush("Raised"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(80, 12, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Right,
            Opacity = message.Queued ? 0.6 : 1,
            Child = column,
        };
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

    private static UIElement StepRow(FeedItem.Step step)
    {
        var tone = step.State == StepState.Failed ? "Destructive" : "Muted";
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(TerminalPanel.Mono(TalkPanel.StepWord(step.Line.Kind, step.Line.Label), tone));

        var subject = TerminalPanel.Mono(step.Line.Subject, step.State == StepState.Failed ? "Destructive" : "Ink");
        subject.Margin = new Thickness(8, 0, 0, 0);
        subject.MaxWidth = 560;
        subject.TextTrimming = TextTrimming.CharacterEllipsis;
        row.Children.Add(subject);

        if (step.Line.Added > 0 || step.Line.Removed > 0)
        {
            var added = TerminalPanel.Mono($"+{step.Line.Added}", "Positive");
            added.Margin = new Thickness(8, 0, 0, 0);
            var removed = TerminalPanel.Mono($"−{step.Line.Removed}", "Destructive");
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

        return row;
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
