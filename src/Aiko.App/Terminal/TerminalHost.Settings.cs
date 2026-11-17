using System.Diagnostics;
using System.IO;
using System.Windows;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The settings inside the terminal window (D-410) and the zoom of the window (D-384).
sealed partial class TerminalHost
{
    /// The settings come on the place of the list and the terminal, at a page when one is asked for.
    /// Open already, they move to that page.
    public void OpenSettings(string? page = null)
    {
        if (_settingsPanel is { } open)
        {
            if (page is not null && page != open.CurrentPage)
            {
                open.Open(page);
            }
        }
        else
        {
            _settingsPanel = NewSettings(page);
            _panel.ShowSettings(_settingsPanel);
        }

        Show();
        _window.Focus();
    }

    /// The checklist of the settings at one item, for the pages that open once at the start.
    public void OpenChecklist(ChecklistItem item)
    {
        OpenSettings(SettingsPanel.ChecklistPageKey);
        _settingsPanel?.OpenChecklist(item);
    }

    /// Back to the same session, as it was.
    public void CloseSettings()
    {
        if (_settingsPanel is not { } settings)
        {
            return;
        }

        settings.Leave();
        _settingsPanel = null;
        _panel.ShowSettings(null);
        Draw();
        _window.Dispatcher.BeginInvoke(FocusOpenTerminal, System.Windows.Threading.DispatcherPriority.Input);
        SettingsClosed?.Invoke();
    }

    private SettingsPanel NewSettings(string? page)
    {
        var settings = new SettingsPanel(page);
        settings.QuitRequested += () => QuitAsked?.Invoke();
        settings.AskBeforeStopping = AskToStop;
        settings.AskBeforeRemovingEnvironment = AskToRemoveEnvironment;
        settings.SettingsChanged += () =>
        {
            // The zoom and voice input of this window are set on the Terminal page.
            var now = SettingsStore.Load();
            UseZoom(now.WindowZoom);
            _voice.Use(now);
            SettingsChanged?.Invoke();
        };

        // A new language: the settings are made again in it, at the same page.
        settings.ReopenRequested += () =>
        {
            var at = settings.CurrentPage;
            settings.Leave();
            _settingsPanel = NewSettings(at);
            _panel.ShowSettings(_settingsPanel);
        };
        return settings;
    }

    /// A choice the person made in the window, kept across starts, and the window drawn with it.
    /// The zoom of the window and the font of every terminal in it (D-384).
    private void UseZoom(int stored)
    {
        var zoom = WindowZoom.Read(stored);
        _window.UseZoom(zoom);
        TerminalSurface.Zoom = zoom;
        foreach (var terminal in _terminals.Values)
        {
            terminal.UseZoom();
        }
    }

    /// A step of the zoom from the keys, kept in the settings.
    private void Zoom(Func<int, int> step)
    {
        var settings = SettingsStore.Change(now => now with { WindowZoom = step(WindowZoom.Read(now.WindowZoom)) });
        UseZoom(settings.WindowZoom);
    }

    private void Remember(Func<AppSettings, AppSettings> change)
    {
        SettingsStore.Change(change);
        Draw();
    }

    // ---- doors for --try-stop ----

    /// What the window asks about now, or null.
    internal StopReason? AskingForCheck => _stopping?.Reason;

    internal void AnswerForCheck(bool stop) => Answer(stop);

    internal bool ShownForCheck => _window.IsVisible;

    internal string? SettingsPageForCheck => _settingsPanel?.CurrentPage;

    internal TerminalSessions SessionsForCheck => _sessions;
}
