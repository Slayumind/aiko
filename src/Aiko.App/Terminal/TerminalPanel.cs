using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The inside of the terminal window (D-292, D-299): the title bar with the tabs of the
/// environments, the list of sessions and the place of the conversation.
///
/// It draws a TerminalView and reports what the person did. It decides nothing: the host answers
/// every event with a new view. It is a control and not a window, so a snapshot can draw it.
sealed class TerminalPanel : UserControl
{
    public const double BarHeight = 44;
    private const double ListWidth = 252;

    private readonly Grid _bar = new() { Height = BarHeight, Background = Brushes.Transparent };
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _right = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly ColumnDefinition _listColumn = new() { Width = new GridLength(ListWidth) };
    private readonly DockPanel _list = new();
    private readonly StackPanel _rows = new();
    private readonly Grid _main = new();
    private readonly Popup _listPopup = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Bottom };
    private readonly Button _listButton;
    private readonly Path _maximizeIcon = Line("");
    private readonly Button _maximize;

    private TerminalView? _view;
    private bool _narrow;

    /// The session whose name is being changed in the list, or null.
    private string? _renaming;

    // The conversation keeps its rows and what is typed while the view is drawn again.
    private readonly TalkPanel _talk = new();

    internal TalkInput Input => _talk.Input;

    /// Keys of the conversation from any place of the window (D-328). The window hears a key first,
    /// wherever the keyboard is, even nowhere after a click on a part that takes no keyboard. The
    /// field of the conversation keeps its own keys, and so does any other field one types into.
    /// Enter and Esc stay with a button, a drop-down and a popup of their own.
    public void OnWindowKey(object sender, KeyEventArgs e)
    {
        if (e.Handled || !_talk.IsVisible || OwnTyping(e.OriginalSource))
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Space && modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            _talk.Input.PressVoice();
        }
        else if (key == Key.Tab && modifiers == ModifierKeys.Shift)
        {
            e.Handled = true;
            _talk.Input.AskMode();
        }
        else if (key == Key.V && modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            _talk.Input.PasteHere();
        }
        else if (modifiers == ModifierKeys.None
            && key is Key.Enter or Key.Escape
            && e.OriginalSource is not (ButtonBase or ComboBox or ComboBoxItem)
            && !QuestionCardView.Holds(e.OriginalSource)
            && InThisWindow(e.OriginalSource))
        {
            e.Handled = _talk.Input.PressOutside(key == Key.Enter ? TalkKey.Enter : TalkKey.Escape);
        }
    }

    /// Letters typed in another place of the window go into the field (D-328). A space stays with
    /// the button or the list it was pressed on, and a digit with the card of questions (D-330).
    public void OnWindowText(object sender, TextCompositionEventArgs e)
    {
        if (e.Handled || !_talk.IsVisible || OwnTyping(e.OriginalSource) || QuestionCardView.Holds(e.OriginalSource)
            || e.Text.Length == 0 || e.Text.All(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return;
        }

        e.Handled = _talk.Input.TypeHere(e.Text);
    }

    /// The field of the conversation, or another field one types into: the name of a session, the
    /// first message of a new one.
    private bool OwnTyping(object source) =>
        _talk.Input.Owns(source) || source is TextBoxBase { IsReadOnly: false };

    /// A popup, such as the list of a drop-down, is a window of its own, and Enter and Esc belong to
    /// it. So do a link in the text of an answer, which is not a Visual.
    private static bool InThisWindow(object source) =>
        source is Visual visual && PresentationSource.FromVisual(visual)?.RootVisual is Window;

    private string? _talkSession;

    // What is typed into the form stays here while the view is drawn again.

    public TerminalPanel()
    {
        Background = Tokens.Brush("Background");
        FontFamily = Tokens.Get<FontFamily>("Sans");
        Focusable = false;

        // Every scroll bar of the window is the thin one of the settings: the list, the feed and
        // the field.
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Theme/Controls.xaml", UriKind.Absolute),
        });
        Resources[typeof(ScrollViewer)] = Resources["QuietScroll"];

        _listButton = Flat("", () => _listPopup.IsOpen = !_listPopup.IsOpen, ghost: false);
        _listButton.Margin = new Thickness(10, 0, 0, 0);
        _listButton.Visibility = Visibility.Collapsed;
        _listPopup.PlacementTarget = _listButton;
        // The list under the button, left edges together, as every drop-down of Aiko (DropDown).
        _listPopup.Placement = PlacementMode.Custom;
        _listPopup.CustomPopupPlacementCallback = (_, target, _) => [new CustomPopupPlacement(new Point(0, target.Height), PopupPrimaryAxis.Horizontal)];
        _listPopup.Opened += (_, _) =>
        {
            if (_listPopup.Child is FrameworkElement list)
            {
                Motion.PopIn(list, above: false);
            }
        };

        _maximize = BarButton(_maximizeIcon, Strings.TerminalMaximize, () => MaximizeAsked?.Invoke());
        var minimize = BarButton(Line("M0,5 L10,5"), Strings.TerminalMinimize, () => MinimizeAsked?.Invoke());
        var close = BarButton(Line("M0,0 L10,10 M10,0 L0,10"), Strings.TerminalCloseWindow, () => CloseAsked?.Invoke());
        Buttons = [minimize, _maximize, close];
        ShowMaximized(false);

        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(_listButton);
        left.Children.Add(_tabs);
        _tabs.Margin = new Thickness(10, 0, 0, 0);
        _bar.Children.Add(left);
        _bar.Children.Add(_right);
        _bar.Children.Add(new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom, Background = Tokens.Brush("Hairline") });

        var body = new Grid();
        body.ColumnDefinitions.Add(_listColumn);
        body.ColumnDefinitions.Add(new ColumnDefinition());
        var listSide = new Grid();
        listSide.Children.Add(_list);
        listSide.Children.Add(new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Right, Background = Tokens.Brush("Hairline") });
        body.Children.Add(listSide);
        Grid.SetColumn(_main, 1);
        body.Children.Add(_main);

        var all = new DockPanel();
        DockPanel.SetDock(_bar, Dock.Top);
        all.Children.Add(_bar);
        all.Children.Add(body);
        Content = all;

        _talk.SendAsked += (text, images) => SendAsked?.Invoke(text, images);
        _talk.StopAsked += () => StopAsked?.Invoke();
        _talk.ModeAsked += () => ModeAsked?.Invoke();
        _talk.ModelAsked += () => ModelAsked?.Invoke();
        _talk.Chosen += (id, choice) => Chosen?.Invoke(id, choice);
        _talk.FolderPicked += index => FolderPicked?.Invoke(index);
        _talk.OtherFolderAsked += () => OtherFolderAsked?.Invoke();

        SizeChanged += (_, e) =>
        {
            var narrow = WindowPlace.IsNarrow(e.NewSize.Width);
            if (narrow != _narrow && _view is not null)
            {
                _narrow = narrow;
                Show(_view);
            }
        };
    }

    /// The part of the title bar the window is dragged by.
    public FrameworkElement Bar => _bar;

    /// The buttons of the window, for the hit test of the window.
    public IReadOnlyList<Button> Buttons { get; }

    public event Action<string>? TabPicked;
    public event Action<string>? RowPicked;
    public event Action<string>? RowClosed;

    /// The id of a session and the new name typed for it.
    public event Action<string, string>? RowRenamed;
    public event Action? CreateAsked;
    public event Action<int>? FolderPicked;
    public event Action? OtherFolderAsked;
    public event Action<bool>? AskAnswered;
    public event Action? SignInAsked;
    public event Action? MinimizeAsked;
    public event Action? MaximizeAsked;
    public event Action? CloseAsked;
    public event Action<string, IReadOnlyList<AttachedImage>>? SendAsked;
    public event Action? StopAsked;
    public event Action? ModeAsked;
    public event Action? ModelAsked;
    public event Action<string, CardChoice>? Chosen;

    public void ShowMaximized(bool maximized)
    {
        _maximizeIcon.Data = Geometry.Parse(maximized ? "M0,2.5 L7.5,2.5 L7.5,10 L0,10 Z M2.5,2.5 L2.5,0 L10,0 L10,7.5 L7.5,7.5" : "M0,0 L10,0 L10,10 L0,10 Z");
        _maximize.ToolTip = maximized ? Strings.TerminalRestore : Strings.TerminalMaximize;
    }

    public void Show(TerminalView view)
    {
        _view = view;
        _narrow = ActualWidth > 0 ? WindowPlace.IsNarrow(ActualWidth) : WindowPlace.IsNarrow(Width);

        ShowBar(view);
        ShowList(view);
        ShowMain(view);
    }

    // ---- the title bar ----

    private void ShowBar(TerminalView view)
    {
        _tabs.Children.Clear();
        foreach (var tab in view.Tabs)
        {
            _tabs.Children.Add(Tab(tab));
        }

        _right.Children.Clear();
        if (view.Badge is { } badge)
        {
            _right.Children.Add(Badge(badge));
        }

        foreach (var button in Buttons)
        {
            _right.Children.Add(button);
        }

        _listButton.Visibility = _narrow ? Visibility.Visible : Visibility.Collapsed;
        _listButton.Content = WithChevron(view.SessionName ?? Strings.TerminalSessions);
    }

    private UIElement Tab(TabView tab)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(Text(tab.Name, "TextNumber", tab.Selected ? "Ink" : "Muted", tab.Selected ? FontWeights.Medium : FontWeights.Normal));
        if (tab.Waiting > 0)
        {
            var pause = Pause();
            pause.Margin = new Thickness(8, 0, 5, 0);
            row.Children.Add(pause);
            row.Children.Add(Mono(_narrow ? tab.Waiting.ToString(CultureInfo.CurrentCulture) : WaitingWords(tab.Waiting), "Caution"));
        }

        var back = new Border
        {
            Height = 28,
            Padding = new Thickness(12, 0, 12, 0),
            Margin = new Thickness(0, 0, 4, 0),
            CornerRadius = new CornerRadius(14),
            Background = tab.Selected ? Tokens.Brush("Raised") : Brushes.Transparent,
            Cursor = Cursors.Hand,
            Child = row,
        };
        back.MouseLeftButtonUp += (_, _) => TabPicked?.Invoke(tab.Name);
        WindowChromeHit(back);
        return back;
    }

    private static string WaitingWords(int count) =>
        string.Format(
            CultureInfo.CurrentCulture,
            count % 10 == 1 && count % 100 != 11 ? Strings.TerminalTabWaitingOne : Strings.TerminalTabWaitingMany,
            count);

    private static UIElement Badge(BadgeView badge)
    {
        var tone = Tokens.ToneBrush(badge.Tone);
        var ring = new Grid { Width = 14, Height = 14, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        ring.Children.Add(new Ellipse { Stroke = Tokens.Brush("Raised"), StrokeThickness = 2 });
        ring.Children.Add(new Path { Stroke = tone, StrokeThickness = 2, Data = Arc(7, 6, badge.Percent / 100.0) });

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 14, 0),
            ToolTip = badge.Kind == LimitKind.FiveHour ? Strings.TerminalLimitFiveHour : Strings.TerminalLimitWeek,
        };
        row.Children.Add(ring);
        row.Children.Add(Mono(badge.Percent.ToString(CultureInfo.CurrentCulture) + "%", "Muted"));
        return row;
    }

    /// A part of a circle from the top, clockwise.
    private static Geometry Arc(double centre, double radius, double part)
    {
        part = Math.Clamp(part, 0, 0.999);
        var angle = part * 2 * Math.PI;
        var end = new Point(centre + radius * Math.Sin(angle), centre - radius * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = new Point(centre, centre - radius) };
        figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0, part > 0.5, SweepDirection.Clockwise, true));
        return new PathGeometry([figure]);
    }

    // ---- the list of sessions ----

    private void ShowList(TerminalView view)
    {
        // While a name is being changed the list stands still: a new list would take the field
        // away from under the keys.
        if (_renaming is not null)
        {
            return;
        }

        _rows.Children.Clear();
        foreach (var group in view.Groups)
        {
            _rows.Children.Add(new Border { Padding = new Thickness(10, 10, 10, 4), Child = Mono(group.Name, "Muted") });
            foreach (var row in group.Rows)
            {
                _rows.Children.Add(Row(row, row.Id == view.SessionId && view.Main == MainView.Talk, view.Now));
            }
        }

        var create = Flat(Strings.TerminalCreateSession, () => CreateAsked?.Invoke(), ghost: true, plus: true);
        create.HorizontalAlignment = HorizontalAlignment.Stretch;
        create.HorizontalContentAlignment = HorizontalAlignment.Left;
        create.Margin = new Thickness(8);

        // The list lives in the window or, in a narrow one, under the button with the session name.
        _list.Children.Clear();
        _listPopup.Child = null;
        var holder = new DockPanel { Background = Tokens.Brush("Background") };
        DockPanel.SetDock(create, Dock.Bottom);
        holder.Children.Add(create);
        holder.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            Padding = new Thickness(8, 4, 8, 0),
            Content = _rows,
        });

        if (_narrow)
        {
            _listColumn.Width = new GridLength(0);
            _listPopup.Child = new Border
            {
                Width = ListWidth + 20,
                MaxHeight = 480,
                Margin = new Thickness(0, 4, 12, 12),
                CornerRadius = new CornerRadius(12),
                BorderBrush = Tokens.Brush("InputLine"),
                BorderThickness = new Thickness(1),
                Background = Tokens.Brush("Background"),
                Effect = Tokens.Get<System.Windows.Media.Effects.Effect>("CardShadow"),
                Child = holder,
            };
        }
        else
        {
            _listColumn.Width = new GridLength(ListWidth);
            _list.Children.Add(holder);
        }
    }

    private UIElement Row(SessionRow row, bool selected, DateTimeOffset now)
    {
        var top = new DockPanel();
        var mark = Mark(row.State);
        DockPanel.SetDock(mark, Dock.Right);
        top.Children.Add(mark);
        var name = Text(row.Name, "TextName", "Ink", FontWeights.Normal);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        top.Children.Add(name);

        // A double click on the name changes it in place (LibreChat, Open WebUI).
        name.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2)
            {
                e.Handled = true;
                Rename(row, top, name);
            }
        };

        var lines = new StackPanel();
        lines.Children.Add(top);
        var second = SecondLine(row, now);
        if (second.Length > 0)
        {
            var line = Text(second, "TextSmall", "Muted", FontWeights.Normal);
            line.TextTrimming = TextTrimming.CharacterEllipsis;
            line.Margin = new Thickness(0, 3, 0, 0);
            lines.Children.Add(line);
        }

        var hover = new Border { CornerRadius = new CornerRadius(10), Background = Tokens.Brush("HoverLayer"), Opacity = 0 };
        var face = new Grid();
        face.Children.Add(hover);
        face.Children.Add(new Border { Padding = new Thickness(10, 8, 10, 8), Child = lines });

        var back = new Border
        {
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 1, 0, 1),
            Background = selected ? Tokens.Brush("Surface") : Brushes.Transparent,
            BorderBrush = selected ? Tokens.Brush("InputLine") : Brushes.Transparent,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Child = face,
            ContextMenu = RowMenu(row.Id, () => Rename(row, top, name)),
        };
        back.MouseEnter += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(1, Motion.Or0(Motion.Hover), Motion.Standard));
        back.MouseLeave += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(0, Motion.Or0(Motion.Hover), Motion.Standard));
        back.MouseLeftButtonUp += (_, _) =>
        {
            _listPopup.IsOpen = false;
            RowPicked?.Invoke(row.Id);
        };
        return back;
    }

    private ContextMenu RowMenu(string id, Action rename)
    {
        var name = new MenuItem { Header = Strings.TerminalRenameSession };
        name.Click += (_, _) => rename();
        var close = new MenuItem { Header = Strings.TerminalCloseSession };
        close.Click += (_, _) => RowClosed?.Invoke(id);
        return new ContextMenu { Items = { name, close } };
    }

    /// The name becomes a field with all of it selected: Enter or leaving the field keeps the new
    /// name, Esc keeps the old one. An empty name changes nothing (TerminalSessions.Rename).
    private void Rename(SessionRow row, DockPanel top, TextBlock name)
    {
        var field = new TextBox { Text = row.Name, MaxLength = 100 };
        Field(field);
        field.Padding = new Thickness(4, 1, 4, 1);
        top.Children.Remove(name);
        top.Children.Add(field);
        _renaming = row.Id;

        var done = false;
        void Finish(bool keep)
        {
            if (done)
            {
                return;
            }

            done = true;
            _renaming = null;
            if (keep && field.Text.Trim() != row.Name)
            {
                RowRenamed?.Invoke(row.Id, field.Text);
            }
            else if (_view is { } view)
            {
                ShowList(view);
            }
        }

        field.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Escape)
            {
                e.Handled = true;
                Finish(keep: e.Key == Key.Enter);
            }
        };
        field.LostKeyboardFocus += (_, _) => Finish(keep: true);
        Dispatcher.BeginInvoke(() =>
        {
            field.Focus();
            field.SelectAll();
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private static UIElement Mark(RowState state)
    {
        var (icon, word, tone) = state switch
        {
            RowState.Working => ((UIElement)Dot(filled: true), Strings.TerminalWorking, "Muted"),
            RowState.Waiting => (Pause(), Strings.TerminalWaiting, "Caution"),
            RowState.Done => (SiteIcons.Draw(SiteIcons.Check, 10, "Positive"), Strings.TerminalDone, "Positive"),
            RowState.Asleep => (Dot(filled: false), Strings.TerminalAsleep, "Muted"),
            RowState.Stopped => (Line("M0,0 L8,8 M8,0 L0,8", "Destructive"), Strings.TerminalStopped, "Destructive"),
            _ => (new Border(), "", "Muted"),
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        if (word.Length > 0)
        {
            row.Children.Add(icon);
            var text = Mono(word, tone);
            text.Margin = new Thickness(6, 0, 0, 0);
            row.Children.Add(text);
        }

        return row;
    }

    private static string SecondLine(SessionRow row, DateTimeOffset now)
    {
        if (row.Glance is not { } glance)
        {
            return UsedWords(row.LastUsed, now);
        }

        string With(string format, params object[] parts) => string.Format(CultureInfo.CurrentCulture, format, parts);

        return glance.Line switch
        {
            GlanceLine.Request => With(Strings.TerminalLineRequest, StepWord(glance), glance.Text).TrimEnd(),
            GlanceLine.Question => With(Strings.TerminalLineQuestion, glance.Text),
            GlanceLine.Plan => Strings.TerminalLinePlan,
            GlanceLine.Step => With(glance.Kind switch
            {
                StepKind.Read => Strings.TerminalLineReading,
                StepKind.Search => Strings.TerminalLineSearching,
                StepKind.Edit => Strings.TerminalLineEditing,
                StepKind.Run => Strings.TerminalLineRunning,
                StepKind.Agent => Strings.TerminalLineAgent,
                StepKind.Web => Strings.TerminalLineOpening,
                StepKind.Skill => Strings.TerminalLineSkill,
                _ => Strings.TerminalLineCalling,
            }, glance.Kind == StepKind.Other ? Called(glance) : glance.Text),
            GlanceLine.Answer => glance.Text,
            GlanceLine.StoppedAt => With(Strings.TerminalLineStoppedAt, StepWord(glance)),
            GlanceLine.SignIn => Strings.TerminalLineSignIn,
            _ => "",
        };
    }

    /// The word of a step, the one the feed uses for it.
    private static string StepWord(TalkGlance glance) => TalkPanel.StepWord(glance.Kind, glance.Label);

    /// A tool without a word of its own: "memory · remember" for an MCP tool, the name for others.
    private static string Called(TalkGlance glance) =>
        glance.Text.Length > 0 ? $"{glance.Label} · {glance.Text}" : glance.Label;

    private static string UsedWords(long lastUsed, DateTimeOffset now)
    {
        if (lastUsed <= 0)
        {
            return "";
        }

        var used = DateTimeOffset.FromUnixTimeSeconds(lastUsed).ToOffset(now.Offset);
        var time = used.ToString("t", CultureInfo.CurrentCulture);
        return SessionRows.DayOf(lastUsed, now.ToUnixTimeSeconds(), (int)now.Offset.TotalMinutes) switch
        {
            UsedDay.Today => time,
            UsedDay.Yesterday => string.Format(CultureInfo.CurrentCulture, Strings.TerminalYesterday, time),
            _ => used.ToString("d MMMM", CultureInfo.CurrentCulture),
        };
    }

    // ---- the place of the conversation ----

    private void ShowMain(TerminalView view)
    {
        UIElement content = view.Main switch
        {
            MainView.Form when view.Form is { } form => Draft(view, form),
            MainView.Talk when view.SignedOut => Message(
                string.Format(CultureInfo.CurrentCulture, Strings.TerminalLoginTitle, view.Environment),
                Strings.TerminalLoginBody,
                Flat(Strings.SignIn, () => SignInAsked?.Invoke(), ghost: false, main: true)),
            MainView.Talk when view.Talk is { } talk => Talk(view, talk),
            MainView.Talk => Message(null, Strings.TerminalEmptyTalk, null),
            _ => Message(
                string.Format(CultureInfo.CurrentCulture, Strings.TerminalNoSessionsTitle, view.Environment),
                Strings.TerminalNoSessionsBody,
                Flat(Strings.TerminalCreateSession, () => CreateAsked?.Invoke(), ghost: false, main: true, plus: true)),
        };

        // The feed stays in the tree from one draw to the next. Taken out and put back, it is
        // measured again and its scroll goes back to the top.
        if (_main.Children.Count > 0 && ReferenceEquals(_main.Children[0], content))
        {
            _main.Children.RemoveRange(1, _main.Children.Count - 1);
        }
        else
        {
            _main.Children.Clear();
            _main.Children.Add(content);
        }

        if (view.Ask is { } ask)
        {
            _main.Children.Add(Ask(ask));
        }
    }

    private UIElement Talk(TerminalView view, TalkView talk)
    {
        _talk.Show(talk);
        if (_talkSession != view.SessionId)
        {
            _talkSession = view.SessionId;
            Dispatcher.BeginInvoke(_talk.FocusInput, System.Windows.Threading.DispatcherPriority.Input);
        }

        return _talk;
    }

    /// A new session before its first message (D-342): the conversation itself, empty, with the
    /// folder, model and mode of the draft in the line under the field. The first message makes
    /// the session.
    private UIElement Draft(TerminalView view, FormView form)
    {
        var model = form.Models[Math.Clamp(form.Model, 0, form.Models.Count - 1)];
        return Talk(view with { SessionId = "draft:" + form.Environment }, new TalkView(
            "draft:" + form.Environment,
            [],
            Busy: false,
            HasOpenCard: false,
            Compacting: false,
            form.Mode,
            new ModelLabel(model.DisplayName, false),
            form.Environment)
        {
            Commands = form.Commands,
            Folder = form.Folder >= 0 ? form.Folders[form.Folder].Path : "",
            Draft = form,
        });
    }

    private static UIElement Message(string? title, string body, UIElement? button)
    {
        var column = new StackPanel { MaxWidth = 520, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(40, 0, 40, 0) };
        if (title is not null)
        {
            var head = Text(title, "TextName", "Ink", FontWeights.Medium);
            head.FontSize = 16;
            head.TextWrapping = TextWrapping.Wrap;
            head.Margin = new Thickness(0, 0, 0, 8);
            column.Children.Add(head);
        }

        var text = Text(body, "TextName", "Muted", FontWeights.Normal);
        text.TextWrapping = TextWrapping.Wrap;
        text.LineHeight = 21;
        column.Children.Add(text);

        if (button is FrameworkElement element)
        {
            element.Margin = new Thickness(0, 14, 0, 0);
            element.HorizontalAlignment = HorizontalAlignment.Left;
            column.Children.Add(element);
        }

        return column;
    }

    /// A folder in the form: its name and the folder it sits in, not the whole path (D-332).
    internal static string FolderLabel(FolderChoice folder) =>
        SessionName.ParentName(folder.Path) is { Length: > 0 } parent ? $"{folder.Name}  ·  {parent}" : folder.Name;

    private UIElement Ask(AskView ask)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        var stop = Flat(Strings.TerminalStopAndClose, () => AskAnswered?.Invoke(true), ghost: false, danger: true);
        var keep = Flat(Strings.TerminalKeepOpen, () => AskAnswered?.Invoke(false), ghost: false);
        keep.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(stop);
        buttons.Children.Add(keep);

        var cross = BarButton(Line("M0,0 L8,8 M8,0 L0,8"), Strings.TerminalKeepOpen, () => AskAnswered?.Invoke(false));
        cross.HorizontalAlignment = HorizontalAlignment.Right;
        cross.VerticalAlignment = VerticalAlignment.Top;
        cross.Margin = new Thickness(0, -6, -8, 0);

        var title = Text(ask.Title, "TextName", "Ink", FontWeights.Medium);
        title.TextWrapping = TextWrapping.Wrap;
        title.Margin = new Thickness(0, 0, 28, 6);
        var body = Text(ask.Body, "TextNumber", "Muted", FontWeights.Normal);
        body.TextWrapping = TextWrapping.Wrap;
        body.LineHeight = 20;

        var column = new StackPanel();
        column.Children.Add(title);
        column.Children.Add(body);
        column.Children.Add(buttons);
        var inside = new Grid();
        inside.Children.Add(column);
        inside.Children.Add(cross);

        var card = new Border
        {
            Width = 420,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(20, 28, 20, 0),
            Padding = new Thickness(18, 16, 18, 16),
            CornerRadius = new CornerRadius(14),
            Background = Tokens.Brush("Surface"),
            BorderBrush = Tokens.Brush("InputLine"),
            BorderThickness = new Thickness(1),
            Effect = Tokens.Get<System.Windows.Media.Effects.Effect>("CardShadow"),
            Child = inside,
        };

        // What is under the panel is still there, only quieter.
        var cover = new Grid { Background = new SolidColorBrush(Color.FromArgb(0x99, 0x0A, 0x0A, 0x0A)) };
        cover.Children.Add(card);
        return cover;
    }

    // ---- small parts ----

    internal static void Field(TextBox box)
    {
        box.FontFamily = Tokens.Get<FontFamily>("Sans");
        box.FontSize = Tokens.Get<double>("TextName");
        box.Foreground = Tokens.Brush("Ink");
        box.CaretBrush = Tokens.Brush("Ink");
        box.Background = Tokens.Brush("Surface");
        box.BorderBrush = Tokens.Brush("InputLine");
        box.BorderThickness = new Thickness(1);
        box.Padding = new Thickness(9, 6, 9, 6);
        box.Resources[typeof(Border)] = new Style(typeof(Border)) { Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(8)) } };
    }

    internal static TextBlock Text(string text, string size, string tone, FontWeight weight) => new()
    {
        Text = text,
        FontFamily = Tokens.Get<FontFamily>("Sans"),
        FontSize = Tokens.Get<double>(size),
        FontWeight = weight,
        Foreground = Tokens.Brush(tone),
        VerticalAlignment = VerticalAlignment.Center,
    };

    internal static TextBlock Mono(string text, string tone) => new()
    {
        Text = text,
        FontFamily = Tokens.Get<FontFamily>("Mono"),
        FontSize = Tokens.Get<double>("TextTiny"),
        Foreground = Tokens.Brush(tone),
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// The width of the lines of the terminal's icons: as thin as the lines of the slayumind icon
    /// set, one unit in twenty-four, at the sizes the terminal draws them (D-332).
    internal const double LineWidth = 0.75;

    internal static Path Line(string data, string tone = "Muted") => new()
    {
        Data = Geometry.Parse(data),
        Stroke = Tokens.Brush(tone),
        StrokeThickness = LineWidth,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// The sign of "waits for you": two thin bars, drawn as thin as the lines of the slayumind icons
    /// (one in twenty-four), so they read as a quiet mark and not as a pause button (D-299, D-332).
    private static FrameworkElement Pause()
    {
        return Line("M0.5,0.5 L0.5,9.5 M4.5,0.5 L4.5,9.5", "Caution");
    }

    internal static FrameworkElement Dot(bool filled) => new Ellipse
    {
        Width = 6,
        Height = 6,
        Fill = filled ? Tokens.Brush("Muted") : Brushes.Transparent,
        Stroke = Tokens.Brush("Muted"),
        StrokeThickness = 1,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static UIElement WithChevron(string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var name = Text(text, "TextNumber", "Ink", FontWeights.Normal);
        name.MaxWidth = 160;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        row.Children.Add(name);
        var chevron = Line("M0,0 L4,4 L8,0");
        chevron.Margin = new Thickness(8, 1, 0, 0);
        row.Children.Add(chevron);
        return row;
    }

    private Button BarButton(UIElement icon, string tip, Action click)
    {
        var hover = new Border { CornerRadius = new CornerRadius(6), Background = Tokens.Brush("Raised"), Opacity = 0 };
        var face = new Grid { Width = 40, Height = 28 };
        face.Children.Add(hover);
        face.Children.Add(new Border { Child = icon, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });

        var button = new Button
        {
            Content = face,
            ToolTip = tip,
            Focusable = false,
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            Template = Bare(),
        };
        button.MouseEnter += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(1, Motion.Or0(Motion.Hover), Motion.Standard));
        button.MouseLeave += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(0, Motion.Or0(Motion.Hover), Motion.Standard));
        button.Click += (_, _) => click();
        WindowChromeHit(button);
        return button;
    }

    /// One button for the whole window: the filled one is the main action, the outlined one the
    /// second, the ghost one the quiet third (DESIGN, one fill per control).
    internal static Button Flat(string text, Action click, bool ghost, bool main = false, bool danger = false, bool plus = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var ink = main ? "Background" : danger ? "Destructive" : ghost ? "Muted" : "Ink";
        if (plus)
        {
            var sign = Line("M5,0 L5,10 M0,5 L10,5", ink);
            sign.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(sign);
        }

        row.Children.Add(Text(text, "TextNumber", ink, main ? FontWeights.Medium : FontWeights.Normal));

        var hover = new Border { CornerRadius = new CornerRadius(8), Background = Tokens.Brush("HoverLayer"), Opacity = 0 };
        var face = new Grid();
        face.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = main ? Tokens.Brush("Ink") : danger ? new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x64, 0x67)) : Brushes.Transparent,
            BorderBrush = main || danger || ghost ? Brushes.Transparent : Tokens.Brush("InputLine"),
            BorderThickness = new Thickness(1),
        });
        face.Children.Add(hover);
        face.Children.Add(new Border { Padding = new Thickness(12, 0, 12, 0), Child = row, VerticalAlignment = VerticalAlignment.Center });

        var button = new Button { Content = face, Height = 32, Cursor = Cursors.Hand, Template = Bare(), FocusVisualStyle = FocusRing() };
        button.MouseEnter += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(1, Motion.Or0(Motion.Hover), Motion.Standard));
        button.MouseLeave += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(0, Motion.Or0(Motion.Hover), Motion.Standard));
        button.Click += (_, _) => click();
        Motion.SetPress(button, true);
        WindowChromeHit(button);
        return button;
    }

    /// A button that is only an icon: 28 by 28, a soft fill on hover, the same press and focus as
    /// the other buttons (D-332).
    internal static Button Icon(UIElement icon)
    {
        var hover = new Border { CornerRadius = new CornerRadius(8), Background = Tokens.Brush("HoverLayer"), Opacity = 0 };
        var face = new Grid { Width = 28, Height = 28 };
        face.Children.Add(hover);
        face.Children.Add(icon);

        var button = new Button { Content = face, Cursor = Cursors.Hand, Template = Bare(), FocusVisualStyle = FocusRing() };
        button.MouseEnter += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(1, Motion.Or0(Motion.Hover), Motion.Standard));
        button.MouseLeave += (_, _) => hover.BeginAnimation(OpacityProperty, Motion.To(0, Motion.Or0(Motion.Hover), Motion.Standard));
        Motion.SetPress(button, true);
        WindowChromeHit(button);
        return button;
    }

    private static ControlTemplate Bare()
    {
        var template = new ControlTemplate(typeof(Button));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, new TemplateBindingExtension(HorizontalContentAlignmentProperty));
        template.VisualTree = presenter;
        return template;
    }

    /// The focus of the keyboard: an outline of 2 px, as in the settings (D-308).
    private static Style FocusRing()
    {
        var ring = new FrameworkElementFactory(typeof(Border));
        ring.SetValue(Border.BorderBrushProperty, Tokens.Brush("Ink"));
        ring.SetValue(Border.BorderThicknessProperty, new Thickness(2));
        ring.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        ring.SetValue(MarginProperty, new Thickness(-3));
        return new Style { Setters = { new Setter(TemplateProperty, new ControlTemplate { VisualTree = ring }) } };
    }

    /// The title bar belongs to the window for dragging; what can be pressed in it says so.
    private static void WindowChromeHit(IInputElement element) =>
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(element, true);
}
