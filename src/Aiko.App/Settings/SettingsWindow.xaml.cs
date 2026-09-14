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
        Panel.WizardRequested += () =>
        {
            WizardRequested?.Invoke();
            Close();
        };
        Panel.ReopenRequested += () =>
        {
            _reopen = true;
            Close();
        };
        Panel.DragHandle.MouseLeftButtonDown += OnDragHandlePressed;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    /// "Quit Aiko" closes the whole app, not this window, so the tray decides what to do.
    public event Action? QuitRequested;

    public event Action? WizardRequested;

    /// True when the window closed only to come back in another language.
    public bool ShouldReopen => _reopen;

    private bool _reopen;

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
