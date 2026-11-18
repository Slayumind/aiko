using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Aiko.Core;
using Aiko.Core.Terminal;
using static Aiko.App.Widgets;

namespace Aiko.App;

/// The inside of the terminal window (D-292, D-299): the title bar with the tabs of the
/// environments, the list of sessions and the place of the conversation.
///
/// It draws a TerminalView and reports what the person did. It decides nothing: the host answers
/// every event with a new view. It is a control and not a window, so a snapshot can draw it.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed partial class TerminalPanel : UserControl
{
    public const double BarHeight = 44;
    private const double ListWidth = 252;

    /// The title bar stays black over the grey body of the window (D-392).
    private readonly Grid _bar = new() { Height = BarHeight, Background = Tokens.Brush("Background") };
    private readonly TerminalPills _pills = new();
    private readonly StackPanel _right = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly ColumnDefinition _listColumn = new() { Width = GridLength.Auto };
    private readonly RotateTransform _chevronTurn = new();
    private double _listTarget = double.NaN;
    private readonly DockPanel _list = new();
    private readonly StackPanel _rows = new();

    /// The rows with a layer over them for the line of a drag (D-400).
    private readonly RowDrag _drag;
    private readonly Grid _main = new();
    private readonly Popup _listPopup = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Bottom };

    /// A palette is a window of its own: nothing drawn in this one can cover a real terminal.
    private readonly Popup _palettePopup = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Relative };
    private readonly Button _listToggle;
    private readonly Button _chevron;
    private readonly Button _gear;

    /// The magnifier left of the gear: the palette of conversations, which only a shortcut opened
    /// before (D-421).
    private readonly Button _search;

    /// The gear stays lit while the settings are open, as a pressed segment.
    private readonly Border _gearLit = new() { CornerRadius = new CornerRadius(6), Background = Tokens.Brush("Raised"), Visibility = Visibility.Collapsed };

    /// A quiet dot on the gear while a newer version is out (D-391); the General page says which.
    private readonly System.Windows.Shapes.Ellipse _gearDot = new()
    {
        Width = 6,
        Height = 6,
        Fill = Tokens.Brush("Positive"),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Margin = new Thickness(0, 6, 11, 0),
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed,
    };

    public void ShowUpdateDot(bool newer) => _gearDot.Visibility = newer ? Visibility.Visible : Visibility.Collapsed;

    /// The settings, while they are open, on the place of the list and the terminal.
    private readonly ContentControl _settingsHost = new() { Visibility = Visibility.Collapsed, Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };

    /// The open header under the title bar (D-373, D-376). It is drawn again only when it opens or
    /// the active environment changes: the numbers in it change too often to start its motion each
    /// time.
    private readonly Border _strip = new() { ClipToBounds = true, Visibility = Visibility.Collapsed, Background = Tokens.Brush("Background") };

    /// The body is moved back by the height of the moving header, so the terminal stands still
    /// under it while the header grows or folds over it (D-390).
    private readonly TranslateTransform _bodyShift = new();

    /// The list of sessions is drawn by WPF and does not need to stand still like the terminal: it
    /// takes back the shift of the body and moves with the edge of the header on every frame.
    /// Standing still, it jumped at the end of opening and at the start of folding.
    private readonly TranslateTransform _listShift = new();
    private readonly HeaderStripView _header;
    private string? _stripFor;

    /// Everything under the title bar and the header: the list and the terminal. While the header
    /// opens or closes it keeps one height, so the terminal changes its size once and not on
    /// every frame (each size is a resize of the pseudo console and a full redraw of Claude Code).
    private readonly Grid _body = new HeldGrid();

    /// A grid that is drawn whole when it is taller than its place. WPF cuts an element held taller
    /// than its place to the size of that place, and the cut moves with the element: when the header
    /// opened, the body was held at its old height and moved up under the header, so its bottom part
    /// was cut away, and a black band as tall as the header stood at the bottom of the terminal until
    /// the header stood. The window itself still cuts what goes past its edge.
    private sealed class HeldGrid : Grid
    {
        protected override Geometry? GetLayoutClip(Size layoutSlotSize) =>
            ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null;
    }

    /// How long the header takes to grow, the cards to go out, and the header to fold.
    private static readonly Duration Grow = new(TimeSpan.FromMilliseconds(320));
    private static readonly Duration Fold = new(TimeSpan.FromMilliseconds(200));
    private static readonly Duration Close = new(TimeSpan.FromMilliseconds(300));
    private static readonly TimeSpan PillsBack = TimeSpan.FromMilliseconds(160);
    private bool _listHidden;
    private readonly Path _maximizeIcon = Marks.Line("");
    private readonly Button _maximize;

    private TerminalView? _view;
    private bool _narrow;

    /// The session whose name is being changed in the list, or null.
    private string? _renaming;

    /// The real terminals of the sessions that were opened (D-349). They stay in the tree once added,
    /// and only the one of the chosen session is visible: a terminal taken out of the tree would lose
    /// its window and its screen.
    private readonly Grid _terminals = new();
    private readonly TerminalLoader _loader = new();

    /// Under a terminal whose Claude Code ended by itself (D-351).
    private readonly DockPanel _stopped = new() { Visibility = Visibility.Collapsed };
    private readonly Button _resume;
    private readonly DockPanel _terminalSide;

    /// The editor under the terminal (D-354): the keyboard is always here.
    public TerminalEditor Editor { get; } = new();

    /// The keys of the chosen session, one quiet line under it (D-421): the editor, voice and the
    /// palette for Claude Code; the request, the search in the output and the palette for a command
    /// line. Nothing else in the window names them.
    private readonly TextBlock _keys = new()
    {
        FontFamily = Tokens.Get<FontFamily>("Mono"),
        FontSize = Tokens.Get<double>("TextTiny"),
        Foreground = Tokens.Brush("Muted"),
        TextTrimming = TextTrimming.CharacterEllipsis,
        Margin = new Thickness(16, 4, 16, 6),
    };

    /// Whether the open session has room for the editor: a terminal that runs.
    private bool _editorRoom;

    private void ShowEditor() =>
        Editor.Visibility = _editorRoom && Editor.Wanted ? Visibility.Visible : Visibility.Collapsed;

    public TerminalPanel()
    {
        _drag = new RowDrag(_rows);
        _drag.Moved += (id, before) => RowMoved?.Invoke(id, before);

        // The list held still while a row was dragged; the latest view comes now.
        _drag.Ended += () =>
        {
            if (_view is { } view)
            {
                ShowList(view);
            }
        };

        Background = Tokens.Brush("Surface");
        FontFamily = Tokens.Get<FontFamily>("Mono");
        Focusable = false;
        PreviewMouseDown += EndRenameOnPressAway;

        // Every scroll bar of the window is the thin one of the settings: the list, the feed and
        // the field.
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Aiko.App;component/Theme/Controls.xaml", UriKind.Absolute),
        });
        Resources[typeof(ScrollViewer)] = Resources["QuietScroll"];

        // The terminal control has a scroll bar of its own, not inside a ScrollViewer: it takes the
        // same thin one by its type.
        Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = Resources["ThinScrollBar"];

        // The list icon: in a wide window it hides the column of sessions, in a narrow one it opens
        // the list over the terminal (D-376).
        _listToggle = BarButton(ListIcon(), Strings.TerminalSessionList, ToggleList);
        _listToggle.Margin = new Thickness(6, 0, 2, 0);
        _listPopup.PlacementTarget = _listToggle;
        // The chevron is a small mark: its button is as narrow as it is tall, so the hover shows
        // where a click lands and does not reach towards the pills.
        _chevron = BarButton(Marks.Line("M0,0 L4,4 L8,0"), Strings.TerminalShowLimits, () => HeaderToggled?.Invoke(), ChevronWidth);
        _search = BarButton(Marks.Line("M4.5,0.5 A4,4 0 1 1 4.49,0.5 Z M7.6,7.6 L11,11"), Strings.TerminalFindConversation, () => PaletteAsked?.Invoke());
        _gear = BarButton(GearIcon(), Strings.Settings, () => SettingsAsked?.Invoke());
        ((Grid)_gear.Content).Children.Insert(0, _gearLit);
        ((Grid)_gear.Content).Children.Add(_gearDot);
        // The list under the button, left edges together, as every drop-down of Aiko (DropDown).
        _listPopup.Placement = PlacementMode.Custom;
        _listPopup.CustomPopupPlacementCallback = (_, target, _) => [new CustomPopupPlacement(new Point(0, target.Height), PopupPrimaryAxis.Horizontal)];

        _maximize = BarButton(_maximizeIcon, Strings.TerminalMaximize, () => MaximizeAsked?.Invoke());
        var minimize = BarButton(Marks.Line("M0,5 L10,5"), Strings.TerminalMinimize, () => MinimizeAsked?.Invoke());
        var close = BarButton(Marks.Line("M0,0 L10,10 M10,0 L0,10"), Strings.TerminalCloseWindow, () => CloseAsked?.Invoke());
        Buttons = [minimize, _maximize, close];
        ShowMaximized(false);

        var left = new StackPanel { Orientation = Orientation.Horizontal };
        // The chevron stands before the pills, so it keeps its place when they fold away.
        left.Children.Add(_listToggle);
        left.Children.Add(_chevron);
        left.Children.Add(_pills.Root);
        // Two columns: the buttons on the right keep their room, and the pills on the left get what
        // is left. In one cell they slid under the gear in a narrow window.
        _bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        left.ClipToBounds = true;
        _bar.Children.Add(left);
        Grid.SetColumn(_right, 1);
        _bar.Children.Add(_right);
        var hairline = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom, Background = Tokens.Brush("Hairline") };
        Grid.SetColumnSpan(hairline, 2);
        _bar.Children.Add(hairline);
        _pills.Refit = FitPills;
        _bar.SizeChanged += (_, _) => FitPills();

        _header = new HeaderStripView(environment => TabPicked?.Invoke(environment), environment => SignInAsked?.Invoke(environment));
        _header.Settled += () =>
        {
            // The header has its new height after a switch. While it still grows or folds, the
            // end of that motion lets the body go.
            if (_stripFor is not null && !_strip.HasAnimatedProperties)
            {
                ReleaseBody();
            }
        };
        _strip.Child = new Border
        {
            BorderBrush = Tokens.Brush("Hairline"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = _header.Root,
        };

        _body.ColumnDefinitions.Add(_listColumn);
        _body.ColumnDefinitions.Add(new ColumnDefinition());
        var listSide = new Grid();
        listSide.Children.Add(_list);
        listSide.Children.Add(new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Right, Background = Tokens.Brush("Hairline") });
        _body.Children.Add(listSide);
        Grid.SetColumn(_main, 1);
        _body.Children.Add(_main);

        var all = new DockPanel();
        DockPanel.SetDock(_bar, Dock.Top);
        all.Children.Add(_bar);
        DockPanel.SetDock(_strip, Dock.Top);
        all.Children.Add(_strip);
        // The settings stand on the place of the list and the terminal; the title bar stays (D-374).
        var area = new Grid();
        area.Children.Add(_body);
        area.Children.Add(_settingsHost);
        all.Children.Add(area);
        // The moving header is drawn over the body that stands still under it.
        Panel.SetZIndex(_strip, 1);
        Panel.SetZIndex(_bar, 2);
        _body.RenderTransform = _bodyShift;
        listSide.RenderTransform = _listShift;
        Content = all;

        _resume = Flat(Strings.TerminalResume, () => ResumeAsked?.Invoke(), ghost: false, main: true);
        DockPanel.SetDock(_resume, Dock.Right);
        _stopped.Children.Add(_resume);
        var stoppedLine = Text(Strings.TerminalStoppedLine, "TextNumber", "Muted", FontWeights.Normal);
        stoppedLine.VerticalAlignment = VerticalAlignment.Center;
        stoppedLine.TextWrapping = TextWrapping.Wrap;
        _stopped.Children.Add(stoppedLine);
        _stopped.Margin = new Thickness(16, 10, 16, 12);
        _loader.Visibility = Visibility.Collapsed;
        _terminals.Children.Add(_loader);
        var terminalSide = new DockPanel();
        DockPanel.SetDock(_keys, Dock.Bottom);
        terminalSide.Children.Add(_keys);
        DockPanel.SetDock(_stopped, Dock.Bottom);
        terminalSide.Children.Add(_stopped);
        DockPanel.SetDock(Editor, Dock.Bottom);
        terminalSide.Children.Add(Editor);
        Editor.WantedChanged += ShowEditor;
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

    /// The buttons of the window, for the hit test of the window.
    public IReadOnlyList<Button> Buttons { get; }

    public event Action<string>? TabPicked;
    public event Action<string>? RowPicked;

    /// A session was dragged in its folder: its id, and the session it now stands before, or null
    /// for the end of the folder (D-400).
    public event Action<string, string?>? RowMoved;
    public event Action<string>? RowClosed;

    /// The id of a session and the new name typed for it.
    public event Action<string, string>? RowRenamed;
    /// A new session in a folder the person picks (D-350).
    public event Action? CreateAsked;

    /// A new session in the folder of a group, with no question: Claude Code or a command line.
    public event Action<string, SessionKind>? GroupCreateAsked;

    /// Opens the folder of a group in Explorer; false when the folder is gone from the disk.
    public Func<string, bool>? FolderOpenAsked { get; set; }

    /// The palette closed: by Esc, by a click beside it, or by a choice.
    public event Action? PaletteClosed;

    /// Continue a session whose Claude Code ended by itself (D-351).
    public event Action? ResumeAsked;
    public event Action<bool>? AskAnswered;
    public event Action? MinimizeAsked;

    /// The chevron or a ring: open or close the header (D-373).
    public event Action? HeaderToggled;

    /// The list icon or Ctrl+B in a wide window: hide or show the column of sessions (D-376).
    public event Action? ListToggled;

    /// The gear in the title bar.
    public event Action? SettingsAsked;

    /// The magnifier in the title bar.
    public event Action? PaletteAsked;

    /// "Start again" on a session whose terminal did not start.
    public event Action? RetryAsked;

    /// "How to install" when Claude Code is not on the computer.
    public event Action? InstallPageAsked;

    /// "Open the log" under a session that did not start: the words say the log has the details.
    public event Action? LogAsked;

    /// "Sign in" on the card of an environment whose account is not connected.
    public event Action<string>? SignInAsked;

    /// The settings come on the place of the list and the terminal, or go, and the session is there
    /// again as it was. The terminal is a window of its own and cannot lie under anything, so it is
    /// hidden while the settings are open.
    public void ShowSettings(FrameworkElement? settings)
    {
        // The way back is in the title bar: a pill, the lit gear, or Esc, which the tip of the gear
        // names. The menu of the settings is as wide as the list, so the edge between them does
        // not move. The first run has no session to go back to and nothing to switch: its title bar
        // keeps only the window buttons (D-394).
        if (settings is SettingsPanel { IsFirstRun: true })
        {
            _gear.Visibility = Visibility.Collapsed;
            _listToggle.Visibility = Visibility.Collapsed;
            _chevron.Visibility = Visibility.Collapsed;
        }
        else if (settings is SettingsPanel panel)
        {
            panel.FitMenu(ListWidth);
        }

        _settingsHost.Content = settings;
        var shown = settings is not null;
        _settingsHost.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        _body.Visibility = shown ? Visibility.Hidden : Visibility.Visible;
        _gearLit.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        _gear.ToolTip = shown ? Strings.SettingsCloseTip : Strings.Settings;
        if (shown && Motion.IsOn)
        {
            _settingsHost.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, Motion.Expand) { EasingFunction = Motion.Standard });
        }

        if (!shown)
        {
            Dispatcher.BeginInvoke(() =>
            {
                foreach (var terminal in _terminals.Children.OfType<TerminalSurface>())
                {
                    terminal.Refit();
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
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

    // ---- the place of the terminal ----

    /// A terminal of a closed session leaves the panel.
    public void Release(TerminalSurface terminal) => _terminals.Children.Remove(terminal);

    // ---- small parts ----

    /// A number after the name of a pill, in the small mono type of Mono and on the name's baseline.
    /// The space before it is a mono space of the same size, about six units, as the gap was.
    private static void AddNumber(TextBlock name, string number, string tone)
    {
        var family = Tokens.Get<FontFamily>("Mono");
        var size = Tokens.Get<double>("TextTiny");
        name.Inlines.Add(new System.Windows.Documents.Run(" ") { FontFamily = family, FontSize = size });
        name.Inlines.Add(new System.Windows.Documents.Run(number)
        {
            FontFamily = family,
            FontSize = size,
            FontWeight = FontWeights.Normal,
            Foreground = Tokens.Brush(tone),
        });
    }

    /// The waiting mark in the box of a pill's ring, so the name does not move when one turns
    /// into the other.
    private static FrameworkElement WaitingMark()
    {
        var pause = Marks.Pause();
        pause.HorizontalAlignment = HorizontalAlignment.Center;
        return new Grid { Width = 14, Height = 14, Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center, Children = { pause } };
    }

    private const double ChevronWidth = 28;

    /// tip is null for a cross that only puts a thing away: what it does is plain without words.
    private Button BarButton(UIElement icon, string? tip, Action click, double width = 40)
    {
        var hover = new Border { CornerRadius = new CornerRadius(6), Background = Tokens.Brush("Raised"), Opacity = 0 };
        var face = new Grid { Width = width, Height = 28 };
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
}
