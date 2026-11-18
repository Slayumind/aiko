using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The state of the chosen environment in the window: the line under the title bar has its colour,
/// and the frame of the terminal names it with a sign and a word (D-435, D-441).
sealed partial class TerminalPanel
{
    /// A question or a limit shows at once. Work starts and ends between the steps of an agent, so
    /// the other colours wait a moment and the line does not blink.
    private static readonly TimeSpan SignalPause = TimeSpan.FromMilliseconds(400);
    private static readonly Duration SignalFade = new(TimeSpan.FromMilliseconds(300));

    private readonly SolidColorBrush _stateBrush = new(Tokens.Colour("HairlineColor"));
    private readonly TitledFrame _terminalFrame = new() { Padding = new Thickness(8, 4, 8, 6), Margin = new Thickness(12, 8, 12, 0) };
    private readonly TitledFrame _editorFrame = new() { Padding = new Thickness(0), Margin = new Thickness(12, 10, 12, 0) };
    private DispatcherTimer? _signalPause;
    private Color _signalColour = Tokens.Colour("HairlineColor");
    private string? _frameLabels;

    private void ShowSignal(TerminalView view)
    {
        var tab = view.Tabs.FirstOrDefault(candidate => candidate.Selected);
        var rows = view.Groups.SelectMany(group => group.Rows).ToList();
        var signal = WindowSignals.Of(rows.Select(row => row.State), tab?.Tone ?? LimitTone.Unknown);
        ShowFrameLabels(view, rows.FirstOrDefault(row => row.Id == view.SessionId), signal, tab?.Percent);

        // With the header open its card carries the state, so the line stays neutral (D-439).
        var colour = Tokens.Colour(view.Header.Open ? "HairlineColor" : ColourOf(signal));
        if (colour == _signalColour)
        {
            return;
        }

        _signalColour = colour;
        _signalPause?.Stop();
        _signalPause = null;
        if (signal is WindowSignal.Waiting or WindowSignal.LimitNearEnd || !Motion.IsOn)
        {
            FadeSignal(colour);
            return;
        }

        var pause = new DispatcherTimer { Interval = SignalPause };
        pause.Tick += (_, _) =>
        {
            pause.Stop();
            _signalPause = null;
            FadeSignal(colour);
        };
        _signalPause = pause;
        pause.Start();
    }

    private void FadeSignal(Color colour)
    {
        if (!Motion.IsOn)
        {
            _stateBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            _stateBrush.Color = colour;
            return;
        }

        _stateBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(colour, SignalFade) { EasingFunction = Motion.Standard });
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

    private static string ColourOf(WindowSignal signal) => signal switch
    {
        WindowSignal.Working => "PositiveColor",
        WindowSignal.Waiting => "CautionColor",
        WindowSignal.Stopped => "DestructiveColor",
        WindowSignal.LimitNearEnd => "LookLimitColor",
        _ => "HairlineColor",
    };
}
