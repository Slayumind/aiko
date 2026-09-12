using System.Windows;
using System.Windows.Input;

namespace Aiko.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        Panel.CloseRequested += Close;
        Panel.QuitRequested += () => QuitRequested?.Invoke();
        Panel.DragHandle.MouseLeftButtonDown += OnDragHandlePressed;
    }

    /// "Quit Aiko" closes the whole app, not this window, so the tray decides what to do.
    public event Action? QuitRequested;

    /// Opened from the tray menu item that asks for an update check.
    public void CheckUpdatesNow() => Panel.StartUpdateCheck();

    private void OnDragHandlePressed(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
