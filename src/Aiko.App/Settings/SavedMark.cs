using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Aiko.App;

/// "Saved", for a moment, after a change. With no Save button this is how the person knows the
/// change took (D-179). The timer runs only while the word is on screen.
sealed class SavedMark : StackPanel
{
    private static readonly TimeSpan Shown = TimeSpan.FromSeconds(1.6);

    private readonly DispatcherTimer _timer = new() { Interval = Shown };
    private readonly ScaleTransform _tickScale = new(1, 1);

    public SavedMark()
    {
        Orientation = Orientation.Horizontal;
        Opacity = 0;
        IsHitTestVisible = false;

        Children.Add(new Path
        {
            Data = Geometry.Parse("M 0,4 L 3,7 L 9,1"),
            Stroke = Tokens.Brush("Positive"),
            StrokeThickness = 1.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Width = 10,
            Height = 8,
            Margin = new Thickness(0, 1, 5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _tickScale,
        });
        Children.Add(new TextBlock
        {
            Text = Strings.Saved,
            FontFamily = Tokens.Get<FontFamily>("Sans"),
            FontSize = Tokens.Get<double>("TextSmall"),
            Foreground = Tokens.Brush("Muted"),
            VerticalAlignment = VerticalAlignment.Center,
        });

        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            BeginAnimation(OpacityProperty, Motion.To(0, Motion.Settle, Motion.Standard));
        };
    }

    public void Show()
    {
        _timer.Stop();
        BeginAnimation(OpacityProperty, Motion.To(1, Motion.Hover, Motion.Standard));
        if (Motion.IsOn)
        {
            var pop = new System.Windows.Media.Animation.DoubleAnimation(0.6, 1, Motion.Settle) { EasingFunction = Motion.Spring };
            _tickScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            _tickScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }

        _timer.Start();
    }

    public void Stop() => _timer.Stop();
}
