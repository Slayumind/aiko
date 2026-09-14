using System.Windows;
using System.Windows.Input;

namespace Aiko.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow() : this(null)
    {
    }

    public SettingsWindow(string? page)
    {
        InitializeComponent();

        _panel = new SettingsPanel(page);
        Motion.SetAppear(_panel, true);
        Content = _panel;

        _panel.CloseRequested += Close;
        _panel.QuitRequested += () => QuitRequested?.Invoke();
        _panel.SettingsChanged += () => SettingsChanged?.Invoke();
        _panel.WizardRequested += () =>
        {
            WizardRequested?.Invoke();
            Close();
        };
        _panel.ReopenRequested += () =>
        {
            ReopenPage = _panel.CurrentPage;
            Close();
        };
        _panel.DragHandle.MouseLeftButtonDown += OnDragHandlePressed;

        // Esc closes from anywhere in the window, a text field included: what was typed is applied
        // on the way out, the same as leaving the field.
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && !e.Handled)
            {
                Close();
            }
        };
        Closing += (_, _) => _panel.Leave();
    }

    private readonly SettingsPanel _panel;

    /// For --try-settings, which works the pages the way a person would.
    internal SettingsPanel Panel => _panel;

    /// "Quit Aiko" closes the whole app, not this window, so the tray decides what to do.
    public event Action? QuitRequested;

    public event Action? WizardRequested;

    /// Raised after every saved change, so Aiko can follow it while the window is still open.
    public event Action? SettingsChanged;

    /// The page to open again when the window closed only to come back in another language.
    public string? ReopenPage { get; private set; }

    /// Opened from the tray menu item that asks for an update check.
    public void CheckUpdatesNow() => _panel.StartUpdateCheck();

    private void OnDragHandlePressed(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
