using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Aiko.App;

/// Opens its content in both directions from nothing, the way the percentage slides out beside a ring
/// when the island unfolds (D-161).
///
/// Like RevealPanel it animates a share from 0 to 1 and measures that share of the content, but in
/// width and height at once: along a top edge the island grows sideways, along a side edge downwards,
/// and either way the other direction must not jump. The words fade in over the second half.
sealed class UnfoldPanel : Decorator
{
    private static readonly DependencyProperty ShareProperty = DependencyProperty.Register(
        "Share", typeof(double), typeof(UnfoldPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure, OnShareChanged));

    private bool _open;

    public UnfoldPanel()
    {
        ClipToBounds = true;
    }

    private double Share => (double)GetValue(ShareProperty);

    public bool IsOpen => _open;

    public void SetOpen(bool open, bool animate)
    {
        _open = open;
        var share = open ? 1.0 : 0.0;

        if (!animate || !Motion.IsOn)
        {
            BeginAnimation(ShareProperty, null);
            SetValue(ShareProperty, share);
            return;
        }

        BeginAnimation(ShareProperty, Motion.To(share, Motion.Expand, Motion.Standard), HandoffBehavior.SnapshotAndReplace);
    }

    private static void OnShareChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        var panel = (UnfoldPanel)target;
        if (panel.Child is not null)
        {
            panel.Child.Opacity = Math.Clamp(((double)e.NewValue - 0.4) / 0.6, 0, 1);
        }
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null)
        {
            return default;
        }

        Child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Child.Opacity = Math.Clamp((Share - 0.4) / 0.6, 0, 1);
        return new Size(Child.DesiredSize.Width * Share, Child.DesiredSize.Height * Share);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        // The content keeps its full size and slides into view instead of being squeezed.
        Child?.Arrange(new Rect(0, 0, Child.DesiredSize.Width, Child.DesiredSize.Height));
        return arrangeSize;
    }
}
