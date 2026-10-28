using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Aiko.App;

/// The last row of the feed while Claude works (D-332): "working" in grey with a light that runs
/// along it in 1.4 seconds, as in opencode and Cline. It appears after half a second, so a short
/// turn ends without it blinking, and names the time of the turn after thirty seconds, as Zed
/// does. With animations off in Windows the words stand still.
sealed class WorkingRow : Grid
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan Sweep = TimeSpan.FromMilliseconds(1400);
    private static readonly TimeSpan TimeAfter = TimeSpan.FromSeconds(30);

    private readonly TextBlock _time = TerminalPanel.Mono("", "Muted");
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TranslateTransform _band = new();

    /// When the turn began. The feed builds this row again whenever an item lands above it, so
    /// the row cannot keep the time itself: it blinked and lost the time after every step.
    internal static DateTime TurnStarted { get; set; } = DateTime.UtcNow;

    private static TimeSpan Turn => DateTime.UtcNow - TurnStarted;

    public WorkingRow()
    {
        Margin = new Thickness(0, 10, 0, 12);

        // Without motion there is no fade to wait for: the row is there at once.
        Opacity = Motion.IsOn ? 0 : 1;
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var words = TerminalPanel.Mono(Strings.TerminalWorkingNow, "Muted");
        words.Foreground = Light(_band);
        Children.Add(words);

        _time.Margin = new Thickness(12, 0, 0, 0);
        _time.HorizontalAlignment = HorizontalAlignment.Left;
        SetColumn(_time, 1);
        Children.Add(_time);

        _tick.Tick += (_, _) => ShowTime();
        Loaded += (_, _) => Start();
        Unloaded += (_, _) =>
        {
            _tick.Stop();
            _band.BeginAnimation(TranslateTransform.XProperty, null);
        };
    }

    /// Grey words and a lighter band in the middle of them; the band slides from the right edge
    /// to the left one while the row is on screen, and stops when it leaves.
    private static Brush Light(TranslateTransform band)
    {
        var muted = Tokens.Get<Color>("MutedColor");
        var ink = Tokens.Get<Color>("InkColor");
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            GradientStops =
            {
                new GradientStop(muted, 0),
                new GradientStop(muted, 0.4),
                new GradientStop(ink, 0.5),
                new GradientStop(muted, 0.6),
                new GradientStop(muted, 1),
            },
            RelativeTransform = band,
        };
        return brush;
    }

    private void Start()
    {
        // A row built again in the middle of a turn is there at once; only a new turn fades in.
        var wait = Motion.IsOn ? Delay - Turn : TimeSpan.Zero;
        if (wait > TimeSpan.Zero)
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Motion.Or0(Motion.Expand)) { BeginTime = wait });
        }
        else
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
        }

        if (Motion.IsOn)
        {
            _band.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(1, -1, new Duration(Sweep)) { RepeatBehavior = RepeatBehavior.Forever });
        }

        ShowTime();
        _tick.Start();
    }

    private void ShowTime()
    {
        var time = Turn;
        _time.Text = time < TimeAfter
            ? ""
            : time.TotalMinutes < 1
                ? string.Format(CultureInfo.InvariantCulture, "{0}s", (int)time.TotalSeconds)
                : string.Format(CultureInfo.InvariantCulture, "{0}m {1}s", (int)time.TotalMinutes, time.Seconds);
    }
}
