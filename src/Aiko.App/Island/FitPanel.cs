using System.Windows;
using System.Windows.Controls;

namespace Aiko.App;

/// Takes the size of its content, or of a smaller target, or anything between, and keeps the content
/// centred. The island uses it to close in around the face while the face replaces the rings, so the
/// face has the same room on every side (D-211).
sealed class FitPanel : Decorator
{
    public static readonly DependencyProperty FitProperty = DependencyProperty.Register(
        nameof(Fit), typeof(double), typeof(FitPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private Size _target;

    /// 0 is the size of the content, 1 the target.
    public double Fit
    {
        get => (double)GetValue(FitProperty);
        set => SetValue(FitProperty, value);
    }

    public Size Target
    {
        get => _target;
        set
        {
            _target = value;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null)
        {
            return default;
        }

        Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var full = Child.DesiredSize;
        var fit = Math.Clamp(Fit, 0, 1);
        return new Size(full.Width + ((_target.Width - full.Width) * fit), full.Height + ((_target.Height - full.Height) * fit));
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (Child is not null)
        {
            var full = Child.DesiredSize;
            Child.Arrange(new Rect((arrangeSize.Width - full.Width) / 2, (arrangeSize.Height - full.Height) / 2, full.Width, full.Height));
        }

        return arrangeSize;
    }
}
