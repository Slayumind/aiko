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

/// The palette of sessions and the panels that float over the window.
sealed partial class TerminalPanel
{
    /// The palette at the top of the window, in the middle, over the terminal.
    public void ShowPalette(PaletteView palette)
    {
        _listPopup.IsOpen = false;
        _palettePopup.IsOpen = false;
        _palettePopup.Child = palette;
        palette.Width = Math.Max(240, Math.Min(PaletteView.CardWidth, ActualWidth - 32));
        _palettePopup.HorizontalOffset = Math.Max(0, (ActualWidth - palette.Width) / 2 - palette.Margin.Left);
        _palettePopup.VerticalOffset = BarHeight + 8;
        PopupZoom.Fit(_palettePopup);
        Motion.OpenPopup(_palettePopup, palette, above: false);
        palette.FocusField();
    }

    public void ClosePalette() => _palettePopup.IsOpen = false;

    public bool PaletteOpen => _palettePopup.IsOpen;

    private void ShowMain(TerminalView view)
    {
        // A terminal draws in a window of its own, over anything WPF puts on top of it. So a question
        // or a message takes the place of the terminals rather than covering them.
        var terminal = view.Main == MainView.Terminal && view.Ask is null ? view.Terminal : null;

        // Until its program draws a letter the terminal is hidden but keeps its place, so the size
        // reaches the pseudo console, and the loader stands over it (D-368).
        foreach (var child in _terminals.Children.OfType<TerminalSurface>())
        {
            child.Visibility = !ReferenceEquals(child, terminal) ? Visibility.Collapsed
                : child.Drawn ? Visibility.Visible
                : Visibility.Hidden;
        }

        if (terminal is not null && !_terminals.Children.Contains(terminal))
        {
            _terminals.Children.Insert(0, terminal);
            terminal.Visibility = terminal.Drawn ? Visibility.Visible : Visibility.Hidden;
        }

        _loader.Visibility = terminal is { Drawn: false } ? Visibility.Visible : Visibility.Collapsed;

        _stopped.Visibility = terminal is not null && view.Stopped ? Visibility.Visible : Visibility.Collapsed;
        // A program that ended takes no text: the bar to continue it stands in place of the editor.
        _editorRoom = terminal is not null && !view.Stopped;
        ShowEditor();
        var kind = view.Groups.SelectMany(group => group.Rows).FirstOrDefault(row => row.Id == view.SessionId)?.Kind;
        _keys.Text = kind == SessionKind.Shell ? Strings.TerminalKeysShell : Strings.TerminalKeysClaude;

        UIElement content = terminal is not null ? _terminalSide
            : view.Ask is { } ask ? Ask(ask)
            : view.Main == MainView.Terminal ? NotStarted(view)
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
            // The keys go where the text is typed: the editor, or the line of Claude Code (D-370).
            Dispatcher.BeginInvoke(() =>
            {
                if (Editor.Wanted)
                {
                    Editor.FocusText();
                }
                else
                {
                    terminal!.FocusTerminal();
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
        }

        _shownTerminal = view.SessionId;
    }

    private string? _shownTerminal;

    /// A session without its terminal says why and what to do about it (D-421).
    private UIElement NotStarted(TerminalView view)
    {
        var again = Flat(Strings.TerminalStartAgain, () => RetryAsked?.Invoke(), ghost: false, main: true);
        return view.Problem switch
        {
            StartProblem.FolderGone => Message(
                string.Format(CultureInfo.CurrentCulture, Strings.TerminalFolderGoneTitle, ShellAsk.FolderName(FolderOf(view))),
                view.Shell ? Strings.TerminalFolderGoneShell : Strings.TerminalFolderGoneBody,
                Flat(Strings.TerminalRemoveFromList, () => RowClosed?.Invoke(view.SessionId!), ghost: false)),
            StartProblem.Failed => Message(
                view.Shell ? Strings.TerminalShellNotStartedTitle : Strings.TerminalNotStartedTitle,
                string.Format(CultureInfo.CurrentCulture, Strings.TerminalStartFailedBody, view.ProblemCode),
                TwoButtons(again, Flat(Strings.TerminalOpenLog, () => LogAsked?.Invoke(), ghost: false))),
            _ => Message(
                Strings.TerminalNotStartedTitle,
                Strings.TerminalNotStartedBody,
                TwoButtons(again, Flat(Strings.TerminalHowToInstall, () => InstallPageAsked?.Invoke(), ghost: false))),
        };
    }

    private static string FolderOf(TerminalView view) =>
        view.Groups.FirstOrDefault(group => group.Rows.Any(row => row.Id == view.SessionId))?.Folder ?? "";

    private static StackPanel TwoButtons(Button first, Button second)
    {
        second.Margin = new Thickness(8, 0, 0, 0);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(first);
        row.Children.Add(second);
        return row;
    }
    private static UIElement Message(string? title, string body, UIElement? button)
    {
        var column = new StackPanel { MaxWidth = 520, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(40, 0, 40, 0) };
        if (title is not null)
        {
            var head = Text(title, "TextBody", "Ink", FontWeights.Medium);
            head.FontSize = Tokens.Get<double>("TextTitle");
            head.TextWrapping = TextWrapping.Wrap;
            head.Margin = new Thickness(0, 0, 0, 8);
            column.Children.Add(head);
        }

        var text = Text(body, "TextBody", "Muted", FontWeights.Normal);
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

        var cross = BarButton(Marks.Line("M0,0 L8,8 M8,0 L0,8"), null, () => AskAnswered?.Invoke(false));
        cross.HorizontalAlignment = HorizontalAlignment.Right;
        cross.VerticalAlignment = VerticalAlignment.Top;
        cross.Margin = new Thickness(0, -6, -8, 0);

        var title = Text(ask.Title, "TextBody", "Ink", FontWeights.Medium);
        title.TextWrapping = TextWrapping.Wrap;
        title.Margin = new Thickness(0, 0, 28, 6);
        var body = Text(ask.Body, "TextBody", "Muted", FontWeights.Normal);
        body.TextWrapping = TextWrapping.Wrap;
        body.LineHeight = 20;

        var column = new StackPanel();
        column.Children.Add(title);
        column.Children.Add(body);
        column.Children.Add(buttons);
        var inside = new Grid();
        inside.Children.Add(column);
        inside.Children.Add(cross);

        var card = new PixelBorder
        {
            Width = 420,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(20, 28, 20, 0),
            Padding = new Thickness(18, 16, 18, 16),
            Background = Tokens.Brush("Background"),
            BorderBrush = Tokens.Brush("Hairline"),
            BorderThickness = new Thickness(1),
            Effect = Tokens.FloatShadow,
            Child = inside,
        };

        // What is under the panel is still there, only quieter.
        var cover = new Grid { Background = Tokens.Brush("Scrim") };
        cover.Children.Add(card);
        return cover;
    }
}
