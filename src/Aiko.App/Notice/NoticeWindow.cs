using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// What one notice says: its kind, the name of the session or the environment, the grey word after
/// it, and for a limit the ring.
sealed record NoticeLine(NoticeKind Kind, string Name, string Word, int? Percent = null, LimitTone Tone = LimitTone.Normal);

/// One notice in its own small window (D-377): a line of 36 units next to the tray or the island,
/// with the mark of the state in a dark circle, the name and a grey word. It comes out of a light
/// blur, stays six seconds, and a click on it opens what it is about. The window is made for the
/// notice and closed after it; it never takes the keyboard.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class NoticeWindow : Window
{
    public const double LineHeight = 36;

    /// Room around the line for its shadow: the window is larger than what is seen.
    public const double Air = 12;

    private static readonly TimeSpan Stay = TimeSpan.FromSeconds(6);
    private static readonly Duration ArriveTime = new(TimeSpan.FromMilliseconds(240));
    private static readonly Duration Leave = new(TimeSpan.FromMilliseconds(160));

    private readonly Border _line;
    private readonly DispatcherTimer _timer;
    private readonly TranslateTransform _shift = new();
    private bool _leaving;

    public NoticeWindow(NoticeLine notice)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        Title = "Aiko";

        _line = LineFor(notice);
        _line.RenderTransform = _shift;
        Content = _line;

        _timer = new DispatcherTimer { Interval = Stay };
        _timer.Tick += (_, _) => Go();
        // While the mouse rests on it, the notice waits to be read.
        _line.MouseEnter += (_, _) => _timer.Stop();
        _line.MouseLeave += (_, _) =>
        {
            if (!_leaving)
            {
                _timer.Start();
            }
        };
        _line.MouseLeftButtonUp += (_, _) =>
        {
            Clicked?.Invoke();
            Go();
        };
        Closed += (_, _) => _timer.Stop();
    }

    /// The notice was clicked: what it is about should open.
    public event Action? Clicked;

    /// Shown at its place. from is where it comes from: +1 from below, -1 from above.
    public void Arrive(int from)
    {
        _timer.Start();
        if (!Motion.IsOn)
        {
            return;
        }

        var easing = Motion.Standard;
        _line.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, ArriveTime) { EasingFunction = easing });
        _shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8 * from, 0, ArriveTime) { EasingFunction = easing });
        var blur = new BlurEffect { Radius = 6 };
        var inner = (FrameworkElement)_line.Child;
        inner.Effect = blur;
        var clear = new DoubleAnimation(6, 0, ArriveTime) { EasingFunction = easing };
        clear.Completed += (_, _) => inner.Effect = null;
        blur.BeginAnimation(BlurEffect.RadiusProperty, clear);
    }

    /// Moves to a new place in the stack.
    public void MoveTo(double top)
    {
        if (!Motion.IsOn || !IsVisible)
        {
            Top = top;
            return;
        }

        BeginAnimation(TopProperty, Motion.To(top, Motion.Expand, Motion.Standard));
    }

    /// Fades out and closes. Raised once.
    public void Go()
    {
        if (_leaving)
        {
            return;
        }

        _leaving = true;
        _timer.Stop();
        Gone?.Invoke(this);
        if (!Motion.IsOn)
        {
            Close();
            return;
        }

        var fade = new DoubleAnimation(0, Leave) { EasingFunction = Motion.Exit };
        fade.Completed += (_, _) => Close();
        _line.BeginAnimation(OpacityProperty, fade);
    }

    /// The notice leaves the stack: by time, by a click, or pushed out by a newer one.
    public event Action<NoticeWindow>? Gone;

    /// The line itself, with room for its shadow around it. Also drawn by --snapshot-notice.
    internal static Border LineFor(NoticeLine notice)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(Sign(notice));
        var name = TerminalPanel.Text(notice.Name, "TextNumber", "Ink", FontWeights.Medium);
        name.Margin = new Thickness(10, 0, 0, 0);
        name.MaxWidth = 240;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        row.Children.Add(name);
        var word = TerminalPanel.Text(notice.Word, "TextNumber", "Muted", FontWeights.Normal);
        word.Margin = new Thickness(8, 0, 0, 0);
        row.Children.Add(word);

        return new Border
        {
            Height = LineHeight,
            Padding = new Thickness(12, 0, 14, 0),
            Margin = new Thickness(Air),
            CornerRadius = new CornerRadius(12),
            Background = Tokens.Brush("Surface"),
            BorderBrush = Tokens.Brush("Hairline"),
            BorderThickness = new Thickness(1),
            Effect = Tokens.Get<Effect>("CardShadow"),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = row,
        };
    }

    /// The mark of the state on its own, a little larger than in the list so it stands next to text
    /// of 13 units, with no circle under it (the owner's pick after D-377).
    private static FrameworkElement Sign(NoticeLine notice)
    {
        FrameworkElement mark = notice.Kind switch
        {
            NoticeKind.Waiting => Marks.Pause(),
            NoticeKind.Done => SiteIcons.Draw(SiteIcons.Check, 12, "Positive"),
            NoticeKind.Stopped => Marks.Line("M0,0 L8,8 M8,0 L0,8", "Destructive"),
            _ => HeaderStripView.Ring(notice.Percent, notice.Tone, 12, 1.5),
        };
        mark.HorizontalAlignment = HorizontalAlignment.Center;
        mark.VerticalAlignment = VerticalAlignment.Center;
        mark.LayoutTransform = new ScaleTransform(1.25, 1.25);
        return new Grid { Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center, Children = { mark } };
    }
}
