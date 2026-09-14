using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Aiko.Core;

namespace Aiko.App;

/// How Aiko moves, in one place (DESIGN.md, «Язык движения», D-161).
///
/// Everything here runs only in answer to the mouse, the keyboard or new data. Nothing loops, and
/// an animation that has finished leaves no timer behind. Motion is off when Windows is asked to
/// show fewer animations, and for --snapshot, which would otherwise catch a window half faded in.
static class Motion
{
    public static readonly Duration Press = new(TimeSpan.FromMilliseconds(90));
    public static readonly Duration Hover = new(TimeSpan.FromMilliseconds(120));
    public static readonly Duration Expand = new(TimeSpan.FromMilliseconds(180));
    public static readonly Duration Settle = new(TimeSpan.FromMilliseconds(280));

    public const double PressedScale = 0.97;
    public const double AppearShift = 4;

    private static bool TurnedOff;

    public static bool IsOn => !TurnedOff && SystemParameters.ClientAreaAnimation;

    /// For snapshots: they draw a window at once and read the pixels.
    public static void TurnOff() => TurnedOff = true;

    public static IEasingFunction Standard { get; } = Freeze(new BezierEase(CubicBezier.Standard));

    public static IEasingFunction Spring { get; } = Freeze(new BezierEase(CubicBezier.Spring));

    public static Duration Or0(Duration duration) => IsOn ? duration : new Duration(TimeSpan.Zero);

    public static DoubleAnimation To(double value, Duration duration, IEasingFunction easing) =>
        new(value, Or0(duration)) { EasingFunction = easing };

    public static ColorAnimation To(Color value, Duration duration) =>
        new(value, Or0(duration)) { EasingFunction = Standard };

    // ---- Motion.Press: a small press-in on anything clickable ----

    public static readonly DependencyProperty PressProperty = DependencyProperty.RegisterAttached(
        "Press", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnPressChanged));

    public static bool GetPress(DependencyObject element) => (bool)element.GetValue(PressProperty);

    public static void SetPress(DependencyObject element, bool value) => element.SetValue(PressProperty, value);

    private static void OnPressChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not UIElement element || e.NewValue is not true)
        {
            return;
        }

        var scale = new ScaleTransform(1, 1);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = scale;

        element.PreviewMouseLeftButtonDown += (_, _) => ScaleTo(scale, PressedScale, Press, Standard);
        element.PreviewMouseLeftButtonUp += (_, _) => ScaleTo(scale, 1, Settle, Spring);
        element.MouseLeave += (_, _) => ScaleTo(scale, 1, Settle, Spring);
    }

    private static void ScaleTo(ScaleTransform scale, double value, Duration duration, IEasingFunction easing)
    {
        var animation = To(value, duration, easing);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    // ---- Motion.Appear: fades in and rises a few pixels when it becomes visible ----

    public static readonly DependencyProperty AppearProperty = DependencyProperty.RegisterAttached(
        "Appear", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnAppearChanged));

    public static bool GetAppear(DependencyObject element) => (bool)element.GetValue(AppearProperty);

    public static void SetAppear(DependencyObject element, bool value) => element.SetValue(AppearProperty, value);

    private static void OnAppearChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not FrameworkElement element || e.NewValue is not true)
        {
            return;
        }

        element.IsVisibleChanged += (_, change) =>
        {
            if (change.NewValue is true)
            {
                Appear(element);
            }
        };
    }

    public static void Appear(UIElement element)
    {
        if (!IsOn)
        {
            element.Opacity = 1;
            return;
        }

        var shift = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
        element.RenderTransform = shift;

        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, Expand) { EasingFunction = Standard });
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(AppearShift, 0, Expand) { EasingFunction = Standard });
    }

    // ---- Motion.Switch: the knob slides and the track changes colour ----

    public const double KnobTravel = 14;

    public static readonly DependencyProperty SwitchProperty = DependencyProperty.RegisterAttached(
        "Switch", typeof(bool), typeof(Motion), new PropertyMetadata(false, OnSwitchChanged));

    public static bool GetSwitch(DependencyObject element) => (bool)element.GetValue(SwitchProperty);

    public static void SetSwitch(DependencyObject element, bool value) => element.SetValue(SwitchProperty, value);

    private static void OnSwitchChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not System.Windows.Controls.Primitives.ToggleButton toggle || e.NewValue is not true)
        {
            return;
        }

        // The template has already moved the knob and lit the track by the time these run. Only a
        // switch on screen plays the way there, from where it was.
        toggle.Checked += (_, _) => PlayFlip(toggle);
        toggle.Unchecked += (_, _) => PlayFlip(toggle);
    }

    private static void PlayFlip(System.Windows.Controls.Primitives.ToggleButton toggle)
    {
        if (!toggle.IsLoaded || !IsOn
            || toggle.Template?.FindName("Knob", toggle) is not FrameworkElement knob
            || toggle.Template.FindName("TrackOn", toggle) is not UIElement trackOn)
        {
            return;
        }

        var on = toggle.IsChecked == true;
        var shift = knob.RenderTransform as TranslateTransform ?? new TranslateTransform();
        knob.RenderTransform = shift;

        shift.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(on ? -KnobTravel : KnobTravel, 0, Settle) { EasingFunction = Spring });
        trackOn.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(on ? 0 : 1, on ? 1 : 0, Expand) { EasingFunction = Standard });
    }

    private static T Freeze<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}

/// A WPF easing that follows a CSS cubic-bezier curve.
sealed class BezierEase : EasingFunctionBase
{
    private readonly CubicBezier _curve;

    public BezierEase(CubicBezier curve)
    {
        _curve = curve;
        EasingMode = EasingMode.EaseIn;
    }

    // EaseIn hands the time straight through, so the curve itself is the whole shape.
    protected override double EaseInCore(double normalizedTime) => _curve.Ease(normalizedTime);

    protected override Freezable CreateInstanceCore() => new BezierEase(_curve);
}
