using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// What the place of the conversation draws: the feed, whether Claude Code works now, and what
/// the line under the field says.
sealed record TalkView(
    string Session,
    IReadOnlyList<FeedItem> Items,
    bool Busy,
    bool HasOpenCard,
    bool Compacting,
    PermissionMode Mode,
    ModelLabel Model,
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
    /// The widest the conversation gets, and the space on its sides.
    private const double ColumnWidth = 860;
    private const double Gutter = 24;

    /// Room for the scroll bar, which lies over the right edge of the feed.
    private const double ScrollBarRoom = 12;

    private readonly ObservableCollection<FeedSlot> _slots = [];
    private readonly ListBox _feed;
    private readonly TextBlock _empty;
    private readonly TalkInput _input = new();
    private readonly Border _reminder;
    private readonly TextBlock _reminderText = TerminalPanel.Text("", "TextNumber", "Caution", FontWeights.Normal);
    private readonly HashSet<string> _open = [];
    private ScrollViewer? _scroll;
    private string? _shownSession;

    /// The card Enter and Esc of the field answer, or null (D-323).
    private FeedItem.Card? _keyCard;

    /// Whether the feed follows its end as an answer grows. Only the reader's hand turns it off: the
    /// wheel up, the scroll bar, the keys that go up. Reaching the bottom again turns it on. It used
    /// to be guessed from the scroll itself, and the guess lost to the feed's own changes of offset:
    /// a reader at the bottom could not scroll up while an answer was written (check of 2026-10-15).
    private bool _follow = true;
    private readonly FeedGlide _glide = new();

    public TalkPanel()
    {
        Focusable = false;

        _feed = new ListBox
        {
            ItemsSource = _slots,
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
        _feed.AddHandler(PreviewMouseWheelEvent, new MouseWheelEventHandler((_, e) =>
        {
            if (e.Delta > 0)
            {
                ReaderGoesUp();
            }
        }), handledEventsToo: true);
        _feed.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((_, e) =>
        {
            if (e.OriginalSource is DependencyObject hit && OnScrollBar(hit))
            {
                ReaderGoesUp();
            }
        }), handledEventsToo: true);
        _feed.AddHandler(PreviewKeyDownEvent, new KeyEventHandler((_, e) =>
        {
            if (e.Key is Key.Up or Key.PageUp or Key.Home)
            {
                ReaderGoesUp();
            }
        }), handledEventsToo: true);
        _feed.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, e) =>
        {
            Follow(e);
            FollowReminder();
        }));

        _empty = TerminalPanel.Text(Strings.TerminalEmptyTalk, "TextName", "Muted", FontWeights.Normal);
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.VerticalAlignment = VerticalAlignment.Bottom;
        _empty.TextWrapping = TextWrapping.Wrap;

        _input.Sent += (text, images) => SendAsked?.Invoke(text, images);
        _input.StopAsked += () => StopAsked?.Invoke();
        _input.AllowAsked += () => ChooseByKey(new CardChoice.Allow());
        _input.DenyAsked += () => ChooseByKey(new CardChoice.Deny(""));
        _input.ModeAsked += () => ModeAsked?.Invoke();
        _input.ModelAsked += () => ModelAsked?.Invoke();

        // A card that went out of view says so above the field (D-309).
        var show = TerminalPanel.Flat(Strings.TerminalShow, ShowOpenCard, ghost: true);
        show.Height = 26;
        var reminderRow = new DockPanel();
        DockPanel.SetDock(show, Dock.Right);
        reminderRow.Children.Add(show);
        _reminderText.VerticalAlignment = VerticalAlignment.Center;
        reminderRow.Children.Add(_reminderText);
        _reminder = new Border
        {
            Padding = new Thickness(14, 2, 4, 2),
            Margin = new Thickness(0, 8, 0, 0),
            CornerRadius = new CornerRadius(10),
            Background = Tokens.Brush("Surface"),
            Visibility = Visibility.Collapsed,
            Child = reminderRow,
        };

        // The feed takes the whole window, so its scroll bar runs along the right edge from the top
        // to the bottom. Its rows and the field keep the same column, and the field lies over the
        // bottom of the feed (AIKO-244).
        var bottom = new StackPanel
        {
            Margin = new Thickness(Gutter, 0, Gutter, 0),
            MaxWidth = ColumnWidth,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        bottom.Children.Add(_reminder);
        bottom.Children.Add(_input);

        // The rows end above the field: the padding moves only the rows, the scroll bar keeps the
        // whole height (QuietScroll in Theme/Controls.xaml). The field grows with its lines and
        // the reminder comes and goes, so the padding follows its height.
        bottom.SizeChanged += (_, e) =>
        {
            _feed.Padding = new Thickness(0, 0, 0, e.NewSize.Height);
            _empty.Margin = new Thickness(0, 0, 0, e.NewSize.Height + 16);
        };

        var all = new Grid();
        all.Children.Add(_feed);
        all.Children.Add(_empty);
        all.Children.Add(bottom);
        Content = all;
    }

    public event Action<string, IReadOnlyList<AttachedImage>>? SendAsked;
    public event Action? StopAsked;
    public event Action? ModeAsked;
    public event Action? ModelAsked;
    public event Action<string, CardChoice>? Chosen;

    /// The field, for voice input to write into.
    internal TalkInput Input => _input;

    /// The field takes the keyboard, for a session the person just went into.
    public void FocusInput() => _input.Focus();

    public void Show(TalkView view)
    {
        // Another session opens at its end.
        var newSession = view.Session != _shownSession;
        _shownSession = view.Session;
        if (newSession)
        {
            _follow = true;
        }

        List<object> rows = [.. view.Items];
        if (view.Busy && !view.HasOpenCard)
        {
            rows.Add(view.Compacting ? WorkingMark.Compacting : WorkingMark.Row);
        }

        // A row keeps its place while its item changes, as an answer does with each piece. A new
        // place makes the list build the row again, and the virtualizing panel loses the reader
        // whose view starts inside that row: it threw them to the end (check of 2026-10-16).
        for (var at = 0; at < rows.Count; at++)
        {
            if (at >= _slots.Count)
            {
                _slots.Add(new FeedSlot(rows[at]));
            }
            else if (_slots[at].Holds(rows[at]))
            {
                _slots[at].Item = rows[at];
            }
            else
            {
                _slots[at] = new FeedSlot(rows[at]);
            }
        }

        while (_slots.Count > rows.Count)
        {
            _slots.RemoveAt(_slots.Count - 1);
        }

        _empty.Visibility = view.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _keyCard = TalkKeys.Card(view.Items);
        _input.Show(view.Session, view.Busy, _keyCard is not null, view.Mode, view.Model, view.Environment);

        if (newSession && _slots.Count > 0)
        {
            // Another session opens at its end at once; gliding there through its past would be noise.
            Dispatcher.BeginInvoke(() => JumpToEnd(_scroll ??= FindScroll(_feed)), System.Windows.Threading.DispatcherPriority.Background);
        }

        Dispatcher.BeginInvoke(FollowReminder, System.Windows.Threading.DispatcherPriority.Background);
    }

    internal void Choose(string requestId, CardChoice choice) => Chosen?.Invoke(requestId, choice);

    /// Enter or Esc in the field answers the card the keys answer (D-323).
    private void ChooseByKey(CardChoice choice)
    {
        if (_keyCard is { } card)
        {
            Choose(card.Request.RequestId, choice);
        }
    }

    /// Whether the content of a step is open. It is kept here, as a row is built again each time
    /// it comes into view.
    internal bool IsOpen(string toolUseId) => _open.Contains(toolUseId);

    internal void Toggle(string toolUseId)
    {
        if (!_open.Remove(toolUseId))
        {
            _open.Add(toolUseId);
        }
    }

    /// The word of a step, the one the list of sessions uses for it too.
    internal static string StepWord(StepKind kind, string label) => kind switch
    {
        StepKind.Read => Strings.TerminalStepRead,
        StepKind.Search => Strings.TerminalStepSearch,
        StepKind.Edit => Strings.TerminalStepEdit,
        StepKind.Run => Strings.TerminalStepRun,
        StepKind.Agent => Strings.TerminalStepAgent,
        StepKind.Web => Strings.TerminalStepWeb,
        StepKind.Skill => Strings.TerminalStepSkill,
        _ => label,
    };

    /// What the reminder calls a card: questions and a plan by their own words, a permission by
    /// the word of its step. The name of the tool behind a question or a plan means nothing to a person.
    private static string CardWord(RequestCard request) => request.Kind switch
    {
        CardKind.Question => Strings.TerminalWaitsQuestions,
        CardKind.Plan => Strings.TerminalWaitsPlan,
        _ => StepWord(request.Step.Kind, request.Step.Label),
    };

    private FeedSlot? OpenCard() =>
        _slots.LastOrDefault(slot => slot.Item is FeedItem.Card { State: RequestState.Open });

    /// The line "waiting for you: edit" shows while the open card is out of view, and names it
    /// with the word of its step (D-309).
    private void FollowReminder()
    {
        if (OpenCard() is not { Item: FeedItem.Card card } slot || InView(slot))
        {
            _reminder.Visibility = Visibility.Collapsed;
            return;
        }

        _reminderText.Text = string.Format(CultureInfo.CurrentCulture, Strings.TerminalWaitsForYou, CardWord(card.Request));
        _reminder.Visibility = Visibility.Visible;
    }

    private bool InView(FeedSlot slot)
    {
        if (_feed.ItemContainerGenerator.ContainerFromItem(slot) is not FrameworkElement row || !row.IsVisible
            || (_scroll ??= FindScroll(_feed)) is not { } scroll)
        {
            return false;
        }

        // A card the person is answering with the keyboard is in front of them, even when it grew
        // past the bottom of the feed while they typed (check of 2026-10-23).
        if (row.IsKeyboardFocusWithin)
        {
            return true;
        }

        // The buttons of a card are at its bottom: a card is in view when its bottom is.
        var bottom = row.TranslatePoint(new Point(0, row.ActualHeight), scroll).Y;
        return bottom > 0 && bottom <= scroll.ViewportHeight + 1;
    }

    private void ShowOpenCard()
    {
        if (OpenCard() is { } slot)
        {
            _feed.ScrollIntoView(slot);
        }
    }

    private void Follow(ScrollChangedEventArgs e)
    {
        // A wide code block scrolls sideways inside a row; only the feed's own scroll counts.
        if (e.OriginalSource is not ScrollViewer scroll || !ReferenceEquals(scroll.TemplatedParent, _feed))
        {
            return;
        }

        var atEnd = scroll.VerticalOffset >= scroll.ScrollableHeight - 2;
        if (!_follow && atEnd && e.VerticalChange > 0)
        {
            _follow = true;
        }
        // The rows grow, or the view gets lower as the field grows over the feed (check of 2026-10-16).
        else if (_follow && !atEnd && (e.ExtentHeightChange != 0 || e.ViewportHeightChange != 0))
        {
            _glide.ToEnd(scroll);
        }
    }

    private void ReaderGoesUp()
    {
        _follow = false;
        _glide.Stop();
    }

    /// To the bottom by number, not by ScrollToEnd: after ScrollToEnd the virtualizing panel kept
    /// bringing the end into view as rows changed size, over the reader's own scroll (2026-10-15).
    private void JumpToEnd(ScrollViewer? scroll)
    {
        _glide.Stop();
        scroll?.ScrollToVerticalOffset(scroll.ScrollableHeight);
    }

    private static bool OnScrollBar(DependencyObject hit)
    {
        for (var at = hit; at is not null; at = at is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(at) : null)
        {
            if (at is System.Windows.Controls.Primitives.ScrollBar)
            {
                return true;
            }
        }

        return false;
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
        row.SetBinding(FeedRow.ItemProperty, new Binding(nameof(FeedSlot.Item)));

        // The column of the field. A stretched row narrower than the feed stands in the middle. The
        // right gutter is wider by the scroll bar, which lies over the right edge of the feed.
        row.SetValue(MaxWidthProperty, ColumnWidth - ScrollBarRoom);
        row.SetValue(MarginProperty, new Thickness(Gutter, 0, Gutter + ScrollBarRoom, 0));
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

/// The place of one row in the feed. The list sees the place; the row draws the item in it.
sealed class FeedSlot(object item) : INotifyPropertyChanged
{
    private object _item = item;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// A FeedItem, or the WorkingMark. The core gives the same record for an item nothing
    /// happened to, and then the row is not built again.
    public object Item
    {
        get => _item;
        set
        {
            if (ReferenceEquals(_item, value) || Equals(_item, value))
            {
                return;
            }

            _item = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Item)));
        }
    }

    /// Whether the item is the one already in this place, maybe changed: the same id and kind.
    public bool Holds(object item) => (_item, item) switch
    {
        (FeedItem shown, FeedItem next) => shown.Id == next.Id && shown.GetType() == next.GetType(),
        _ => ReferenceEquals(_item, item),
    };
}

/// The last row of the feed while a turn goes on. It is not an item of the core: the core says
/// only that the session is busy.
sealed class WorkingMark
{
    public static readonly WorkingMark Row = new(false);

    /// Claude Code makes the conversation shorter (D-325).
    public static readonly WorkingMark Compacting = new(true);

    private WorkingMark(bool compacting) => IsCompacting = compacting;

    public bool IsCompacting { get; }
}

/// One row of the feed, built from its item whenever the item changes.
sealed class FeedRow : ContentControl
{
    /// The running piece of the compacting bar, as a share of the bar, and one run in seconds.
    private const double CompactingPiece = 0.25;
    private const double CompactingSeconds = 1.6;

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
        // A streaming answer keeps its view between lumps, so the words that are drying go on.
        if (Item is FeedItem.Answer { Streaming: true } writing && Content is InkAnswer ink && ink.Id == writing.Id)
        {
            ink.Update(writing.Text);
            return;
        }

        // The end of a streaming answer waits for its last words to dry before the finished view.
        if (Item is FeedItem.Answer { Streaming: false } written && Content is InkAnswer inked && inked.Id == written.Id)
        {
            inked.Finish(written.Text, () =>
            {
                if (Item is FeedItem.Answer { Streaming: false } still && still.Id == written.Id && ReferenceEquals(Content, inked))
                {
                    Content = AnswerView.WithCopy(still.Text);
                }
            });
            return;
        }

        Content = Item switch
        {
            WorkingMark { IsCompacting: true } => Compacting(),
            WorkingMark => Working(),
            FeedItem.OwnerMessage message => Message(message),
            FeedItem.Answer { Streaming: true } answer => new InkAnswer(answer.Id, answer.Text),
            FeedItem.Answer answer => AnswerView.WithCopy(answer.Text),
            FeedItem.Step { Answers.Count: > 0 } step => Answered(step.Answers),
            FeedItem.Step { Line.Label: QuestionAnswers.Tool, State: StepState.Failed } => Declined(),
            FeedItem.Step { Line.Hidden: false } step => StepRow(step),
            FeedItem.Card { State: RequestState.Open } card => Card(card.Request),
            FeedItem.Notice notice => Notice(notice),
            _ => null,
        };
    }

    /// The words "working" with a light that runs along them (D-332). The row waits half a second
    /// before it shows, so it does not blink at the end of a turn; after thirty seconds it adds
    /// the time of the turn. It moves only while it is on screen.
    private static UIElement Working() => new WorkingRow();

    /// "Making the conversation shorter" and a thin bar with a piece that runs along it. Claude
    /// Code does not say how far it is, so the bar shows only that the work goes on (D-325).
    private static UIElement Compacting()
    {
        var column = new StackPanel { Margin = new Thickness(0, 10, 0, 12) };
        column.Children.Add(TerminalPanel.Mono(Strings.TerminalCompacting, "Muted"));

        // The bar only means something while it runs; with motion off the words say it alone.
        if (!Motion.IsOn)
        {
            return column;
        }

        var run = new TranslateTransform();
        var piece = new System.Windows.Shapes.Rectangle
        {
            Fill = Tokens.Brush("Muted"),
            HorizontalAlignment = HorizontalAlignment.Left,
            RenderTransform = run,
        };
        var bar = new Grid
        {
            Height = 2,
            Margin = new Thickness(0, 8, 0, 0),
            Background = Tokens.Brush("Hairline"),
            ClipToBounds = true,
        };
        bar.Children.Add(piece);
        bar.SizeChanged += (_, size) =>
        {
            var width = size.NewSize.Width;
            piece.Width = width * CompactingPiece;
            run.BeginAnimation(TranslateTransform.XProperty, new System.Windows.Media.Animation.DoubleAnimation(
                -piece.Width, width, TimeSpan.FromSeconds(CompactingSeconds))
            {
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
            });
        };
        column.Children.Add(bar);
        return column;
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
            Child = MessageInside(message),
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

    /// The text of a message and its pictures, as small squares above the text.
    private static UIElement MessageInside(FeedItem.OwnerMessage message)
    {
        var column = new StackPanel();
        if (message.Attachments.Count > 0)
        {
            var pictures = new WrapPanel { Margin = new Thickness(0, 0, 0, message.Text.Length > 0 ? 8 : 0) };
            foreach (var picture in message.Attachments)
            {
                var image = new System.Windows.Media.Imaging.BitmapImage();
                image.BeginInit();
                image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                image.DecodePixelHeight = 160;
                image.StreamSource = new System.IO.MemoryStream(Convert.FromBase64String(picture.Base64));
                image.EndInit();
                image.Freeze();
                pictures.Children.Add(new Border
                {
                    Width = 80,
                    Height = 80,
                    Margin = new Thickness(0, 0, 8, 0),
                    CornerRadius = new CornerRadius(8),
                    Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill },
                });
            }

            column.Children.Add(pictures);
        }

        if (message.Text.Length > 0)
        {
            column.Children.Add(Words(message.Text, "Ink", new Thickness(0)));
        }

        return column;
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
    private UIElement StepRow(FeedItem.Step step)
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
            Child = WithResult(step, WithProgress(step, row)),
        };
    }

    /// A subagent at work says under its line what it is doing now, in its own few words. The
    /// line goes when the subagent is over.
    private static UIElement WithProgress(FeedItem.Step step, UIElement line)
    {
        if (step.Line.Kind != StepKind.Agent || step.State != StepState.Running || step.Progress.Trim().Length == 0)
        {
            return line;
        }

        var doing = TerminalPanel.Mono(step.Progress.Trim(), "Muted");
        doing.FontSize = 12;
        doing.Margin = new Thickness(62, 2, 0, 4);
        doing.MaxWidth = 560;
        doing.HorizontalAlignment = HorizontalAlignment.Left;
        doing.TextTrimming = TextTrimming.CharacterEllipsis;

        var column = new StackPanel();
        column.Children.Add(line);
        column.Children.Add(doing);
        return column;
    }

    /// The line of a step opens to what the step gave back, with a click (D-294).
    private UIElement WithResult(FeedItem.Step step, UIElement line)
    {
        if (step.Result.Trim().Length == 0)
        {
            return line;
        }

        var column = new StackPanel();
        var head = new Border { Background = Brushes.Transparent, Cursor = Cursors.Hand, Child = line };
        column.Children.Add(head);

        var open = FindPanel()?.IsOpen(step.ToolUseId) == true;
        var result = Words(step.Result.TrimEnd(), "Muted", new Thickness(0, 4, 0, 8));
        result.FontFamily = Tokens.Get<FontFamily>("Mono");
        result.FontSize = Tokens.Get<double>("TextSmall");
        result.MaxHeight = 280;
        result.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        result.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        column.Children.Add(result);

        head.MouseLeftButtonUp += (_, _) =>
        {
            FindPanel()?.Toggle(step.ToolUseId);
            result.Visibility = result.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        };
        return column;
    }

    private UIElement Card(RequestCard request)
    {
        if (request.Kind == CardKind.Question)
        {
            return new QuestionCardView(request, choice => Choose(request, choice));
        }

        var column = new StackPanel();

        var word = TalkPanel.StepWord(request.Step.Kind, request.Step.Label);
        var title = TerminalPanel.Text(Capital(word), "TextNumber", "Ink", FontWeights.Medium);
        column.Children.Add(title);

        if (request.Kind == CardKind.Plan)
        {
            // A plan is markdown, like an answer.
            column.Children.Add(AnswerView.For(request.Plan));
        }
        else if (request.Kind == CardKind.Permission)
        {
            // A command is shown whole: the step line keeps only its first line.
            var command = request.Step.Kind == StepKind.Run ? InputText(request, "command") : "";
            var subject = command.Length > 0 ? command : request.Step.Subject;
            if (subject.Length > 0)
            {
                var words = Words(subject, "Ink", new Thickness(0, 6, 0, 0));
                words.FontFamily = Tokens.Get<FontFamily>("Mono");
                words.FontSize = Tokens.Get<double>("TextSmall");
                column.Children.Add(words);
            }

            if (EditDiff.For(request.Source.ToolName, request.Source.Input) is { } diff)
            {
                column.Children.Add(Diff(diff));
            }
        }

        column.Children.Add(Buttons(request));

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

    private static string InputText(RequestCard request, string key) =>
        request.Source.Input.ValueKind == System.Text.Json.JsonValueKind.Object
        && request.Source.Input.TryGetProperty(key, out var value)
        && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    /// The lines an edit takes out and puts in, without numbers (D-303). A long diff scrolls.
    private static UIElement Diff(EditDiff diff)
    {
        var lines = new StackPanel();
        foreach (var line in diff.Lines)
        {
            var removed = line.Side == DiffSide.Removed;
            lines.Children.Add(new Border
            {
                Background = new SolidColorBrush(removed ? Color.FromArgb(0x1F, 0xFF, 0x64, 0x67) : Color.FromArgb(0x1F, 0x00, 0xBC, 0x7D)),
                Padding = new Thickness(10, 1, 10, 1),
                Child = new TextBlock
                {
                    Text = (removed ? "- " : "+ ") + line.Text,
                    FontFamily = Tokens.Get<FontFamily>("Mono"),
                    FontSize = Tokens.Get<double>("TextSmall"),
                    Foreground = Tokens.Brush(removed ? "Destructive" : "Positive"),
                    TextWrapping = TextWrapping.Wrap,
                },
            });
        }

        if (diff.More > 0)
        {
            var more = TerminalPanel.Mono(string.Format(CultureInfo.CurrentCulture, Strings.TerminalDiffMore, diff.More), "Muted");
            more.Margin = new Thickness(10, 6, 0, 4);
            lines.Children.Add(more);
        }

        return new Border
        {
            Background = Tokens.Brush("Background"),
            BorderBrush = Tokens.Brush("Hairline"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(0, 6, 0, 6),
            Child = new ScrollViewer { MaxHeight = 320, Content = lines },
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

        // The keyboard stays in the field, and the card says which keys answer it from there (D-323).
        var keys = TerminalPanel.Mono(Strings.TerminalCardKeys, "Muted");
        keys.VerticalAlignment = VerticalAlignment.Center;
        keys.Margin = new Thickness(4, 0, 0, 6);
        row.Children.Add(keys);
        return row;
    }

    /// What the person chose on a card of questions, left in the feed after the card: one quiet
    /// line per question, the header grey and the answer light, a green check in front.
    private static UIElement Answered(IReadOnlyList<QuestionAnswer> answers)
    {
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var at = 0; at < answers.Count; at++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var check = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(SiteIcons.Check),
                Fill = Tokens.Brush("Positive"),
                Stretch = Stretch.Uniform,
                Width = 10,
                Height = 10,
                Margin = new Thickness(0, 5, 8, 0),
                VerticalAlignment = VerticalAlignment.Top,
            };
            Grid.SetRow(check, at);
            grid.Children.Add(check);

            var header = TerminalPanel.Text(answers[at].Header, "TextNumber", "Muted", FontWeights.Normal);
            header.MaxWidth = 240;
            header.TextTrimming = TextTrimming.CharacterEllipsis;
            header.VerticalAlignment = VerticalAlignment.Top;
            header.Margin = new Thickness(0, 1, 12, 3);
            Grid.SetRow(header, at);
            Grid.SetColumn(header, 1);
            grid.Children.Add(header);

            var answer = TerminalPanel.Text(answers[at].Answer, "TextNumber", "Ink", FontWeights.Normal);
            answer.TextWrapping = TextWrapping.Wrap;
            answer.VerticalAlignment = VerticalAlignment.Top;
            answer.Margin = new Thickness(0, 1, 0, 3);
            Grid.SetRow(answer, at);
            Grid.SetColumn(answer, 2);
            grid.Children.Add(answer);
        }

        return grid;
    }

    /// The person said no to a card of questions.
    private static UIElement Declined()
    {
        var words = TerminalPanel.Text(Strings.TerminalQuestionsDenied, "TextNumber", "Muted", FontWeights.Normal);
        words.Margin = new Thickness(0, 6, 0, 6);
        words.HorizontalAlignment = HorizontalAlignment.Left;
        return words;
    }

    private static UIElement Notice(FeedItem.Notice notice)
    {
        var text = notice.Kind switch
        {
            NoticeKind.Interrupted => Strings.TerminalTurnStopped,
            NoticeKind.Compacted when notice.Size is { } size => string.Format(
                Strings.TerminalCompactedSize, CompactSize.Thousands(size.Before), CompactSize.Thousands(size.After)),
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
