using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Aiko.App;

/// In place of a terminal whose program has not drawn anything yet (D-368): three dots that light
/// up in turn, and a word. The dots move only while the loader is on screen, and stand still when
/// the system asks for less motion.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class TerminalLoader : StackPanel
{
    private static readonly Duration Beat = new(TimeSpan.FromMilliseconds(1200));

    private readonly List<Ellipse> _dots = [];

    public TerminalLoader()
    {
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        var dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        for (var i = 0; i < 3; i++)
        {
            var dot = new Ellipse { Width = 6, Height = 6, Fill = Tokens.Brush("Muted"), Margin = new Thickness(3, 0, 3, 0), Opacity = 0.35 };
            _dots.Add(dot);
            dots.Children.Add(dot);
        }

        Children.Add(dots);
        var word = TerminalPanel.Text(Strings.TerminalStarting, "TextNumber", "Muted", FontWeights.Normal);
        word.HorizontalAlignment = HorizontalAlignment.Center;
        word.Margin = new Thickness(0, 12, 0, 0);
        Children.Add(word);

        IsVisibleChanged += (_, _) => Play(IsVisible);
    }

    private void Play(bool on)
    {
        for (var i = 0; i < _dots.Count; i++)
        {
            if (!on || !Motion.IsOn)
            {
                _dots[i].BeginAnimation(OpacityProperty, null);
                continue;
            }

            // One dot after another: each lights up for a third of the beat.
            var pulse = new DoubleAnimationUsingKeyFrames { Duration = Beat, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(i * 200) };
            pulse.KeyFrames.Add(new LinearDoubleKeyFrame(0.35, KeyTime.FromPercent(0)));
            pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromPercent(0.25)));
            pulse.KeyFrames.Add(new LinearDoubleKeyFrame(0.35, KeyTime.FromPercent(0.5)));
            pulse.KeyFrames.Add(new LinearDoubleKeyFrame(0.35, KeyTime.FromPercent(1)));
            _dots[i].BeginAnimation(OpacityProperty, pulse);
        }
    }
}
