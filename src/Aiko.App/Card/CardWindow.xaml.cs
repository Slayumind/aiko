using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Aiko.App;

public partial class CardWindow : Window
{
    public CardWindow()
    {
        InitializeComponent();

        Panel.CloseRequested += FadeAndClose;
        Panel.SettingsRequested += () => SettingsRequested?.Invoke();
        Panel.OpenClaudeRequested += environment => OpenClaudeRequested?.Invoke(environment);
        Panel.DragHandle.MouseLeftButtonDown += OnDragHandlePressed;

        // Touching the card at all keeps it: reading it, dragging it, or opening the settings from
        // it. Somebody who reached for the card meant to use it.
        PreviewMouseDown += (_, _) => Pin();
    }

    public event Action? SettingsRequested;

    public event Action<string>? OpenClaudeRequested;

    /// A card opened by resting the mouse on the icon goes away when the mouse goes away. A card
    /// the user clicked for, or clicked on, stays until they close it.
    ///
    /// Before, every card stayed. Running the mouse along the taskbar left a window that had to be
    /// dismissed by hand, which is a strange price for a glance.
    public bool IsPinned { get; private set; }

    public void Pin() => IsPinned = true;

    public void Update(CardModel model) => Panel.Show(model);

    /// Shows the card already in place. The size is only known once the window has been laid out,
    /// so it starts see through: otherwise it would flash in the corner of the screen first.
    public void ShowAt(Rect iconInPixels)
    {
        Opacity = 0;
        Show();
        UpdateLayout();
        var above = PlaceAbove(iconInPixels);
        Opacity = 1;
        PopIn(above);
    }

    /// The card grows out of the icon: from a little smaller and a few pixels towards the icon, with
    /// the spring everything else in Aiko uses (D-161). Above the icon it grows from its bottom edge,
    /// below it from its top.
    private void PopIn(bool aboveTheIcon)
    {
        if (!Motion.IsOn)
        {
            return;
        }

        var scale = new ScaleTransform(PopFrom, PopFrom);
        var shift = new TranslateTransform(0, aboveTheIcon ? PopShift : -PopShift);
        Panel.RenderTransformOrigin = new Point(0.5, aboveTheIcon ? 1 : 0);
        Panel.RenderTransform = new TransformGroup { Children = { scale, shift } };

        var grow = new DoubleAnimation(PopFrom, 1, Motion.Settle) { EasingFunction = Motion.Spring };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, Motion.Settle) { EasingFunction = Motion.Spring });
        Panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Motion.Hover) { EasingFunction = Motion.Standard });
    }

    private const double PopFrom = 0.94;
    private const double PopShift = 6;

    /// True from the start of the fade: a click in that moment opens a new card instead of pinning
    /// the one on its way out.
    public bool IsClosing { get; private set; }

    /// Goes away the way it came, quicker: a short fade, then the window is really closed.
    public void FadeAndClose()
    {
        if (!Motion.IsOn || !IsVisible)
        {
            Close();
            return;
        }

        IsClosing = true;
        IsHitTestVisible = false;
        var fade = new DoubleAnimation(0, Motion.Hover) { EasingFunction = Motion.Standard };
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }

    /// Puts the card above the tray icon. The rectangle comes from Windows in real pixels, and
    /// WPF works in its own units, so it is divided by the scaling of this screen.
    /// Returns false when there was no room above and the card went below the icon.
    public bool PlaceAbove(Rect iconInPixels)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var icon = new Rect(
            iconInPixels.X / dpi.DpiScaleX,
            iconInPixels.Y / dpi.DpiScaleY,
            iconInPixels.Width / dpi.DpiScaleX,
            iconInPixels.Height / dpi.DpiScaleY);

        var left = icon.X + (icon.Width / 2) - (ActualWidth / 2);
        var top = icon.Y - ActualHeight;

        // The taskbar is not always at the bottom. If there is no room above the icon, the card
        // goes below it instead of hanging off the screen.
        var work = SystemParameters.WorkArea;
        var above = top >= work.Top;
        if (!above)
        {
            top = icon.Bottom;
        }

        Left = Math.Clamp(left, work.Left, Math.Max(work.Left, work.Right - ActualWidth));
        Top = Math.Clamp(top, work.Top, Math.Max(work.Top, work.Bottom - ActualHeight));
        return above;
    }

    /// The card is dragged by its header. Windows moves it for us, so there is no mouse maths.
    private void OnDragHandlePressed(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
