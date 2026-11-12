using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The first run in the terminal window (D-394). Before the first environment there is no terminal
/// to open, so the same window comes with its own title bar and the checklist in the middle, one
/// step at a time. Finish hands the place over: this window closes where it stood, and the terminal
/// opens there with the environments just written.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class FirstRunHost
{
    private readonly TerminalPanel _panel = new();
    private readonly TerminalWindow _window;
    private SettingsPanel _settings;

    public FirstRunHost()
    {
        _window = new TerminalWindow(_panel, WindowPlace.Fit(SettingsStore.LoadTerminal().Window, WorkAreas.All()));
        _panel.Show(Empty());
        _settings = NewSettings(SettingsPanel.ChecklistPageKey);
        _panel.ShowSettings(_settings);
        _window.Closing += (_, _) => _settings.Leave();
        _window.Closed += (_, _) => Closed?.Invoke();
    }

    /// Raised after every change, so the tray, the island and direct mode follow it at once.
    public event Action? SettingsChanged;

    /// "Quit Aiko", from the checklist's own pages.
    public event Action? QuitAsked;

    /// Finish wrote the environments: the terminal takes the window.
    public event Action? Finished;

    public event Action? Closed;

    public bool IsVisible => _window.IsVisible;

    public void Show()
    {
        _window.Show();
        _window.Activate();
    }

    public void Activate()
    {
        _window.Activate();
    }

    public void Close() => _window.Close();

    private SettingsPanel NewSettings(string page)
    {
        var settings = new SettingsPanel(page, firstRun: true);
        settings.SettingsChanged += () => SettingsChanged?.Invoke();
        settings.QuitRequested += () => QuitAsked?.Invoke();
        settings.FirstRunFinished += () => Finished?.Invoke();

        // A new language: the checklist is made again in it.
        settings.ReopenRequested += () =>
        {
            settings.Leave();
            _settings = NewSettings(page);
            _panel.ShowSettings(_settings);
        };
        return settings;
    }

    /// A title bar with no environments yet: the window buttons and nothing to switch.
    private static TerminalView Empty() => new(
        [],
        new HeaderView(false, true, []),
        [],
        string.Empty,
        null,
        string.Empty,
        MainView.NoSessions,
        null,
        null,
        DateTimeOffset.Now);
}
