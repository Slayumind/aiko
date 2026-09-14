using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Aiko.App;

public partial class CardWindow : Window
{
    public CardWindow()
    {
        InitializeComponent();

        Panel.CloseRequested += Close;
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
        PlaceAbove(iconInPixels);
        Opacity = 1;
        Motion.Appear(Panel);
    }

    /// Puts the card above the tray icon. The rectangle comes from Windows in real pixels, and
    /// WPF works in its own units, so it is divided by the scaling of this screen.
    public void PlaceAbove(Rect iconInPixels)
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
        if (top < work.Top)
        {
            top = icon.Bottom;
        }

        Left = Math.Clamp(left, work.Left, Math.Max(work.Left, work.Right - ActualWidth));
        Top = Math.Clamp(top, work.Top, Math.Max(work.Top, work.Bottom - ActualHeight));
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
