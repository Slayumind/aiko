using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Aiko.App;

/// Opens and closes its content by height, the way a settings row or a checklist item unfolds.
///
/// WPF cannot animate a height to "as tall as the content", so the panel animates a share from 0
/// to 1 and reports that share of the content's height when it is measured. The window around it
/// grows and shrinks with it.
sealed class RevealPanel : Decorator
{
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(RevealPanel), new PropertyMetadata(false, OnIsOpenChanged));

    private static readonly DependencyProperty ShareProperty = DependencyProperty.Register(
        "Share", typeof(double), typeof(RevealPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public RevealPanel()
    {
        ClipToBounds = true;
        Loaded += (_, _) => SetShare(IsOpen ? 1 : 0, animate: false);
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    private double Share => (double)GetValue(ShareProperty);

    private static void OnIsOpenChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        var panel = (RevealPanel)target;
        panel.SetShare((bool)e.NewValue ? 1 : 0, animate: panel.IsLoaded);
    }

    private void SetShare(double share, bool animate)
    {
        if (Child is not null)
        {
            Child.Visibility = share > 0 || IsOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        if (!animate || !Motion.IsOn)
        {
            BeginAnimation(ShareProperty, null);
            SetValue(ShareProperty, share);
            if (Child is not null)
            {
                Child.Opacity = share;
            }

            return;
        }

        var animation = Motion.To(share, Motion.Expand, Motion.Standard);
        animation.Completed += (_, _) =>
        {
            if (!IsOpen && Child is not null)
            {
                Child.Visibility = Visibility.Collapsed;
            }
        };

        BeginAnimation(ShareProperty, animation, HandoffBehavior.SnapshotAndReplace);
        Child?.BeginAnimation(OpacityProperty, Motion.To(share, Motion.Expand, Motion.Standard));
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null)
        {
            return default;
        }

        Child.Measure(new Size(constraint.Width, double.PositiveInfinity));
        return new Size(Child.DesiredSize.Width, Child.DesiredSize.Height * Share);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        // The content keeps its full height while the panel is shorter, so it slides into view
        // instead of being squeezed.
        Child?.Arrange(new Rect(0, 0, arrangeSize.Width, Child.DesiredSize.Height));
        return arrangeSize;
    }
}
