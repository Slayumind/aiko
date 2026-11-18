using System.Globalization;
using System.Windows;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The state of the chosen environment in the window: the frame of the terminal names it with a
/// sign and a word, and in the open header the card of the environment has its colour (D-452).
sealed partial class TerminalPanel
{
    private readonly TitledFrame _terminalFrame = new() { Padding = new Thickness(8, 4, 8, 6), Margin = new Thickness(12, 8, 12, 0) };
    private readonly TitledFrame _editorFrame = new() { Padding = new Thickness(0), Margin = new Thickness(12, 10, 12, 0) };
    private string? _frameLabels;

    private static WindowSignal SignalOf(TerminalView view) => WindowSignals.Of(
        view.Groups.SelectMany(group => group.Rows).Select(row => row.State),
        view.Tabs.FirstOrDefault(tab => tab.Selected)?.Tone ?? LimitTone.Unknown);

    private void ShowSignal(TerminalView view)
    {
        var tab = view.Tabs.FirstOrDefault(candidate => candidate.Selected);
        var row = view.Groups.SelectMany(group => group.Rows).FirstOrDefault(candidate => candidate.Id == view.SessionId);
        ShowFrameLabels(view, row, SignalOf(view), tab?.Percent);
    }

    private void ShowFrameLabels(TerminalView view, SessionRow? row, WindowSignal signal, int? percent)
    {
        var limit = signal == WindowSignal.LimitNearEnd && row?.State != RowState.Waiting;
        var key = string.Join('\n', view.SessionName, row?.State, limit, percent);
        if (key == _frameLabels)
        {
            return;
        }

        _frameLabels = key;
        var title = Widgets.Text(view.SessionName ?? "", "TextNumber", "Ink", FontWeights.Normal);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.MaxWidth = 360;
        _terminalFrame.Title = title;
        _terminalFrame.Aside = limit
            ? StatusMark.Create(StatusLook.LimitNearEnd, string.Format(CultureInfo.CurrentCulture, Strings.TerminalLimitMark, percent ?? 0))
            : row is null ? null : StatusMark.Of(row.State);
    }

    /// For the snapshot: the frames of the terminal and the editor, with no real terminal in them.
    internal void ShowTerminalFramesForSnapshot()
    {
        _main.Children.Clear();
        _main.Children.Add(_terminalSide);
        _terminalSide.Visibility = Visibility.Visible;
        _editorFrame.Visibility = Visibility.Visible;
    }
}
