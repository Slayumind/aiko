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
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
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

    /// A palette is a window of its own: nothing drawn in this one can cover a real terminal.
    private readonly Popup _palettePopup = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Relative };
    private readonly Button _listButton;
    private readonly Path _maximizeIcon = Line("");
    private readonly Button _maximize;

    private TerminalView? _view;
    private bool _narrow;

    /// The session whose name is being changed in the list, or null.
    private string? _renaming;

    /// The real terminals of the sessions that were opened (D-349). They stay in the tree once added,
    /// and only the one of the chosen session is visible: a terminal taken out of the tree would lose
    /// its window and its screen.
    private readonly Grid _terminals = new();

    /// Under a terminal whose Claude Code ended by itself (D-351).
    private readonly DockPanel _stopped = new() { Visibility = Visibility.Collapsed };
    private readonly Button _resume;
    private readonly DockPanel _terminalSide;

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

        // The terminal control has a scroll bar of its own, not inside a ScrollViewer: it takes the
        // same thin one by its type.
        Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = Resources["ThinScrollBar"];

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

        _resume = Flat(Strings.TerminalResume, () => ResumeAsked?.Invoke(), ghost: false, main: true);
        DockPanel.SetDock(_resume, Dock.Right);
        _stopped.Children.Add(_resume);
        var stoppedLine = Text(Strings.TerminalStoppedLine, "TextNumber", "Muted", FontWeights.Normal);
        stoppedLine.VerticalAlignment = VerticalAlignment.Center;
        stoppedLine.TextWrapping = TextWrapping.Wrap;
        _stopped.Children.Add(stoppedLine);
        _stopped.Margin = new Thickness(16, 10, 16, 12);
        var terminalSide = new DockPanel();
        DockPanel.SetDock(_stopped, Dock.Bottom);
        terminalSide.Children.Add(_stopped);
        terminalSide.Children.Add(_terminals);
        _terminalSide = terminalSide;

        _palettePopup.PlacementTarget = this;
        _palettePopup.Closed += (_, _) =>
        {
            _palettePopup.Child = null;
            PaletteClosed?.Invoke();
        };

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
    /// A new session in a folder the person picks (D-350).
    public event Action? CreateAsked;

    /// A new session in the folder of a group, with no question: Claude Code or a command line.
    public event Action<string, SessionKind>? GroupCreateAsked;

    /// The palette closed: by Esc, by a click beside it, or by a choice.
    public event Action? PaletteClosed;

    /// Continue a session whose Claude Code ended by itself (D-351).
    public event Action? ResumeAsked;
    public event Action<bool>? AskAnswered;
    public event Action? MinimizeAsked;
    public event Action? MaximizeAsked;
    public event Action? CloseAsked;

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
            _rows.Children.Add(GroupHeader(group));
            foreach (var row in group.Rows)
            {
                _rows.Children.Add(Row(row, row.Id == view.SessionId && view.Main == MainView.Terminal, view.Now));
            }
        }

        // One button under the list: the quick pick of a folder, with its key (D-353).
        var key = Mono(Strings.TerminalNewSessionKey, "Muted");
        key.Margin = new Thickness(12, 0, 0, 0);
        var newSession = Flat(Strings.TerminalNewSession, () => CreateAsked?.Invoke(), ghost: false, plus: true, aside: key);
        newSession.HorizontalAlignment = HorizontalAlignment.Stretch;
        newSession.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        var create = new Border { Margin = new Thickness(8), Child = newSession };

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

    /// The folder of a group, and on hover two quiet buttons: a session of Claude Code and a command
    /// line in this folder, with no question (D-350).
    private UIElement GroupHeader(RowGroup group)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Opacity = 0 };
        foreach (var (icon, tip, kind) in new (string, string, SessionKind)[]
        {
            ("M5,0 L5,10 M0,5 L10,5", Strings.TerminalGroupNewSession, SessionKind.Claude),
            ("M0,1 L4,5 L0,9 M6,9 L11,9", Strings.TerminalGroupNewShell, SessionKind.Shell),
        })
        {
            var button = Icon(Line(icon));
            button.ToolTip = tip;
            button.Width = 22;
            button.Height = 22;
            ((FrameworkElement)button.Content).Width = 22;
            ((FrameworkElement)button.Content).Height = 22;
            button.Click += (_, _) => GroupCreateAsked?.Invoke(group.Folder, kind);
            // The keyboard reaches them too: a button in focus is shown.
            button.GotKeyboardFocus += (_, _) => buttons.Opacity = 1;
            button.LostKeyboardFocus += (_, _) => buttons.Opacity = 0;
            buttons.Children.Add(button);
        }

        var header = new DockPanel { Margin = new Thickness(10, 10, 4, 2), Background = Brushes.Transparent, ToolTip = group.Folder };
        DockPanel.SetDock(buttons, Dock.Right);
        header.Children.Add(buttons);
        var name = Mono(group.Name, "Muted");
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        header.Children.Add(name);
        header.MouseEnter += (_, _) => buttons.BeginAnimation(OpacityProperty, Motion.To(1, Motion.Or0(Motion.Hover), Motion.Standard));
        header.MouseLeave += (_, _) => buttons.BeginAnimation(OpacityProperty, Motion.To(0, Motion.Or0(Motion.Hover), Motion.Standard));
        return header;
    }

    private UIElement Row(SessionRow row, bool selected, DateTimeOffset now)
    {
        var top = new DockPanel();
        var mark = Mark(row.State);
        DockPanel.SetDock(mark, Dock.Right);
        top.Children.Add(mark);

        // What runs in the session (the mockup of D-362): the mark of Claude Code or "$".
        UIElement kind = row.Kind == SessionKind.Shell
            ? Mono("$", "Muted")
            : Line("M6,1 L6,11 M1,6 L11,6 M2.5,2.5 L9.5,9.5 M9.5,2.5 L2.5,9.5");
        var kindBox = new Border { Width = 14, Margin = new Thickness(0, 0, 8, 0), Child = kind };
        ((FrameworkElement)kind).HorizontalAlignment = HorizontalAlignment.Center;
        DockPanel.SetDock(kindBox, Dock.Left);
        top.Children.Add(kindBox);
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
            // Under the name, past the mark of the kind.
            line.Margin = new Thickness(22, 3, 0, 0);
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
        // A menu set on an element is not in its tree, so the styles of the panel are handed to it.
        var menu = new ContextMenu { Items = { name, close }, Style = (Style)FindResource(typeof(ContextMenu)) };
        menu.Resources[typeof(MenuItem)] = FindResource(typeof(MenuItem));
        return menu;
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

    /// What the terminal did last, or, for a sleeping session, when it was used.
    private static string SecondLine(SessionRow row, DateTimeOffset now) =>
        row.Line.Length > 0 ? row.Line : UsedWords(row.LastUsed, now);
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

    // ---- the place of the terminal ----

    /// A terminal of a closed session leaves the panel.
    public void Release(TerminalSurface terminal) => _terminals.Children.Remove(terminal);

    // ---- the palette ----

    /// The palette at the top of the window, in the middle, over the terminal.
    public void ShowPalette(PaletteView palette)
    {
        _listPopup.IsOpen = false;
        _palettePopup.IsOpen = false;
        _palettePopup.Child = palette;
        palette.Width = Math.Max(240, Math.Min(PaletteView.CardWidth, ActualWidth - 32));
        _palettePopup.HorizontalOffset = Math.Max(0, (ActualWidth - palette.Width) / 2 - palette.Margin.Left);
        _palettePopup.VerticalOffset = BarHeight + 8;
        _palettePopup.IsOpen = true;
        Motion.PopIn(palette, above: false);
        palette.FocusField();
    }

    public void ClosePalette() => _palettePopup.IsOpen = false;

    public bool PaletteOpen => _palettePopup.IsOpen;

    private void ShowMain(TerminalView view)
    {
        // A terminal draws in a window of its own, over anything WPF puts on top of it. So a question
        // or a message takes the place of the terminals rather than covering them.
        var terminal = view.Main == MainView.Terminal && view.Ask is null ? view.Terminal : null;
        foreach (UIElement child in _terminals.Children)
        {
            child.Visibility = ReferenceEquals(child, terminal) ? Visibility.Visible : Visibility.Collapsed;
        }

        if (terminal is not null && !_terminals.Children.Contains(terminal))
        {
            _terminals.Children.Add(terminal);
        }

        _stopped.Visibility = terminal is not null && view.Stopped ? Visibility.Visible : Visibility.Collapsed;

        UIElement content = terminal is not null ? _terminalSide
            : view.Ask is { } ask ? Ask(ask)
            : view.Main == MainView.Terminal ? Message(null, Strings.TerminalEmptyTalk, null)
            : Message(
                string.Format(CultureInfo.CurrentCulture, Strings.TerminalNoSessionsTitle, view.Environment),
                Strings.TerminalNoSessionsBody,
                Flat(Strings.TerminalCreateSession, () => CreateAsked?.Invoke(), ghost: false, main: true, plus: true));

        // The terminals stay in the tree from one draw to the next: taken out and put back, each
        // would build its window again.
        if (!ReferenceEquals(content, _terminalSide))
        {
            _terminalSide.Visibility = Visibility.Collapsed;
            _main.Children.Clear();
            _main.Children.Add(_terminalSide);
            _main.Children.Add(content);
            return;
        }

        _terminalSide.Visibility = Visibility.Visible;
        if (_main.Children.Count != 1 || !ReferenceEquals(_main.Children[0], _terminalSide))
        {
            _main.Children.Clear();
            _main.Children.Add(_terminalSide);
        }

        if (view.Stopped)
        {
            // Enter presses the button: the terminal of a program that ended takes no keys.
            Dispatcher.BeginInvoke(() => _resume.Focus(), System.Windows.Threading.DispatcherPriority.Input);
        }
        else if (_shownTerminal != view.SessionId)
        {
            Dispatcher.BeginInvoke(terminal!.FocusTerminal, System.Windows.Threading.DispatcherPriority.Input);
        }

        _shownTerminal = view.SessionId;
    }

    private string? _shownTerminal;
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

    private UIElement Ask(AskView ask)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        var stop = Flat(ask.Stop, () => AskAnswered?.Invoke(true), ghost: false, danger: true);
        var keep = Flat(ask.Keep, () => AskAnswered?.Invoke(false), ghost: false);
        keep.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(stop);
        buttons.Children.Add(keep);

        var cross = BarButton(Line("M0,0 L8,8 M8,0 L0,8"), ask.Keep, () => AskAnswered?.Invoke(false));
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
    /// aside stands at the right end of the button, such as the key of its action.
    internal static Button Flat(string text, Action click, bool ghost, bool main = false, bool danger = false, bool plus = false, UIElement? aside = null)
    {
        var row = new DockPanel();
        var ink = main ? "Background" : danger ? "Destructive" : ghost ? "Muted" : "Ink";
        if (aside is not null)
        {
            DockPanel.SetDock(aside, Dock.Right);
            row.Children.Add(aside);
        }

        if (plus)
        {
            var sign = Line("M5,0 L5,10 M0,5 L10,5", ink);
            sign.Margin = new Thickness(0, 0, 8, 0);
            DockPanel.SetDock(sign, Dock.Left);
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
