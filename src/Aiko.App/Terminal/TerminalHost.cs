using System.IO;
using System.Windows;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The terminal behind its window (D-292, D-293, D-349): the sessions of the file, a real terminal
/// for each session that was opened, and what is chosen. Every event of the panel ends in a new view.
///
/// The terminals live while Aiko runs (D-351). Closing the window only hides it while a program
/// still runs in it, so the agents work on and their screens stay as they were; with nothing running
/// the window really closes. After a restart of Aiko a session of Claude Code comes back with
/// --resume, and a shell opens again in the same folder.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class TerminalHost
{
    private EnvironmentSettings _settings;
    private readonly Func<string, LimitSnapshot?> _limitsOf;
    private readonly Func<CardModel> _cardOf;
    private readonly TerminalPanel _panel = new();
    private readonly TerminalWindow _window;
    private readonly Dictionary<string, TerminalSurface> _terminals = [];

    /// Sessions of Claude Code whose program ended by itself (D-351): the row says "stopped" and
    /// Enter starts it again with --resume.
    private readonly Dictionary<string, long> _exited = [];

    // What each running session of Claude Code does (D-352): its title, its last hook event, and
    // when the person last looked at it and typed into it, in milliseconds.
    private readonly TerminalEvents _events;
    private readonly Dictionary<string, ClaudeTitle> _titles = [];
    private readonly Dictionary<string, TerminalEvent> _lastEvents = [];
    private readonly Dictionary<string, long> _seenAt = [];
    private readonly Dictionary<string, long> _answeredAt = [];

    /// What each terminal did at the last drawing, to see what changed since (D-409).
    private readonly Dictionary<string, TerminalActivity> _activities = [];

    private WindowKeys? _keys;
    private VoiceDictation _voice = null!;

    // The conversations of the environments, for the palette; read when it opens.
    private readonly ConversationShelf _shelf = new();
    private IReadOnlyList<Conversation> _conversations = [];

    private TerminalSessions _sessions;
    private TerminalWindowModel _model;
    private string? _closing;
    private bool _quitting;

    /// Aiko asked to quit while programs run: what to do once the person agrees.
    private Action? _quitAfter;

    /// limitsOf gives the latest limits of an environment by its name; cardOf the words of the
    /// card of limits, which the open header shows for the active environment (D-376). iconRows:
    /// what the tray icon draws, for the taskbar button (D-409). A check keeps nothing:
    /// terminal.json is not written.
    public TerminalHost(EnvironmentSettings settings, TerminalSessions sessions, Func<string, LimitSnapshot?> limitsOf, Func<CardModel> cardOf, Func<(CardRow? Ring, CardRow? Dot)> iconRows, bool keepsNothing = false)
    {
        _keepsNothing = keepsNothing;
        _settings = settings;
        _sessions = sessions;
        _limitsOf = limitsOf;
        _cardOf = cardOf;
        _model = TerminalWindowModel.Opened(sessions, [.. settings.Environments.Select(environment => environment.Name)]);

        _window = new TerminalWindow(_panel, WindowPlace.Fit(sessions.Window, WorkAreas.All()));
        Button = new TaskbarButton(_window, iconRows);
        Button.Update();
        _window.MayClose = MayCloseWindow;
        _events = new TerminalEvents(_window.Dispatcher);
        _events.Changed += EventCame;
        _window.SourceInitialized += (_, _) =>
        {
            _keys = new WindowKeys(new System.Windows.Interop.WindowInteropHelper(_window).Handle, KeyPressed);
            // A click in the terminal selects there, and the keys come back to the editor.
            _keys.ClickedInside += () =>
            {
                // In a session of Claude Code the keys stay with the terminal (D-370).
                if (_panel.Editor.Wanted)
                {
                    _window.Dispatcher.BeginInvoke(FocusOpenTerminal, System.Windows.Threading.DispatcherPriority.Input);
                }
            };
            _keys.Dropped += Dropped;
            _keys.TerminalTab = shift =>
            {
                // Tab takes the grey hint of Claude Code, Shift+Tab changes its mode.
                if (OpenTerminal() is not { } terminal)
                {
                    return false;
                }

                terminal.Type(shift ? "\u001b[Z" : "\t");
                return true;
            };
        };
        UseZoom(SettingsStore.Load().WindowZoom);
        _window.Activated += (_, _) =>
        {
            var now = SettingsStore.Load();
            _voice.Use(now);

            // The settings may have changed since the window was last in focus.
            UseZoom(now.WindowZoom);

            // Back in focus, the open session is seen: its "done" goes, as the person looks at it.
            Draw();
        };
        _window.Closing += (_, e) =>
        {
            if (!e.Cancel)
            {
                _sessions = _sessions.Placed(_window.Place);
                Save();
            }
        };
        _window.IsVisibleChanged += (_, _) => OnTaskbarChanged?.Invoke(_window.IsVisible);
        _window.Closed += (_, _) =>
        {
            OnTaskbarChanged?.Invoke(false);
            foreach (var terminal in _terminals.Values)
            {
                terminal.Close();
            }

            _events.Dispose();
            _keys?.Dispose();
            _voice.Close();
            Closed?.Invoke();
        };

        // A pill is also the way back from the settings: to the sessions of that environment.
        _panel.TabPicked += name =>
        {
            CloseSettings();
            Change(_model.ToEnvironment(name, _sessions));
        };
        _panel.RowPicked += Pick;
        _panel.RowClosed += AskToClose;
        _panel.RowMoved += (id, before) =>
        {
            _sessions = _sessions.Moved(id, before);
            Save();
            Draw();
        };
        _panel.RowRenamed += (id, name) =>
        {
            _sessions = _sessions.Rename(id, name);
            Save();
            Draw();
        };
        _panel.CreateAsked += PickFolderQuickly;
        _panel.GroupCreateAsked += StartIn;
        _panel.FolderOpenAsked = OpenInExplorer;
        _panel.PaletteClosed += FocusOpenTerminal;
        WireEditor();
        _panel.ResumeAsked += Resume;
        _panel.AskAnswered += Answer;
        _panel.HeaderToggled += () => Remember(settings => settings with { HeaderOpen = !settings.HeaderOpen });
        _panel.ListToggled += () => Remember(settings => settings with { SessionListHidden = !settings.SessionListHidden });
        _panel.SignInAsked += SignIn;
        _panel.SettingsAsked += () =>
        {
            if (SettingsOpen)
            {
                CloseSettings();
            }
            else
            {
                OpenSettings();
            }
        };

        // Esc from anywhere in the settings, a text field included: what was typed is applied on the
        // way out, the same as leaving the field.
        _window.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape && !e.Handled && SettingsOpen)
            {
                e.Handled = true;
                CloseSettings();
            }
        };
    }

    /// Claude Code in a window of its own for this environment, where the sign in happens, as the
    /// card of limits did before (D-399).
    private void SignIn(string environment)
    {
        if (_settings.Environments.FirstOrDefault(e => e.Name == environment) is { } found)
        {
            ClaudeLauncher.Open(found.ConfigDirectories[0], System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile));
        }
    }

    // ---- the settings in the window (D-374) ----

    private SettingsPanel? _settingsPanel;

    public bool SettingsOpen => _settingsPanel is not null;

    /// A newer version is out, or not any more: the dot on the gear follows (D-391).
    public void ShowUpdate(bool newer) => _panel.ShowUpdateDot(newer);

    /// Raised after every change in the settings, so the tray, the island and direct mode follow it
    /// at once (D-179).
    public event Action? SettingsChanged;

    /// The settings went back to the session.
    public event Action? SettingsClosed;

    /// "Quit Aiko" on the General page: the shell decides, as programs may still run.
    public event Action? QuitAsked;

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

    public event Action? Closed;

    /// The window got its button on the taskbar, shown or minimized, or lost it by being hidden or
    /// closed (D-409).
    public event Action<bool>? OnTaskbarChanged;

    /// The button of the window on the taskbar: the picture of the tray and the sign of the sessions.
    public TaskbarButton Button { get; }


    internal TerminalPanel Panel => _panel;

    /// Whether a program still runs in a terminal of the window.
    public bool AnyRunning => _terminals.Values.Any(terminal => terminal.IsRunning);

    public void Show()
    {
        Draw();
        _window.Show();
        _window.Activate();
        OnTaskbarChanged?.Invoke(_window.IsVisible);
    }

    /// The settings changed while the window is open: a renamed environment takes its sessions and
    /// its tab with it, and a new one gets a tab. With no environment left the window closes.
    public void Follow(EnvironmentSettings settings)
    {
        var names = settings.Environments.Select(environment => environment.Name).ToList();
        if (names.Count == 0)
        {
            Close();
            return;
        }

        // The settings change for many other reasons: nothing of the terminal moves then.
        var before = _settings;
        if (settings.ToJson() == before.ToJson())
        {
            return;
        }

        _sessions = _sessions.Follow(before, settings);
        _settings = settings;

        var renamedTab = before.Environments.FirstOrDefault(environment => environment.Name == _model.Environment) is { } shown
            ? settings.Environments.FirstOrDefault(environment =>
                environment.ConfigDirectories.SequenceEqual(shown.ConfigDirectories, StringComparer.OrdinalIgnoreCase))?.Name
            : null;
        _model = names.Contains(_model.Environment) ? _model
            : renamedTab is not null ? _model with { Environment = renamedTab }
            : TerminalWindowModel.Opened(_sessions, names);

        Save();
        Draw();
    }

    /// Aiko is asked to quit while programs still run in the window (D-351): the window comes
    /// forward with the question, and quit runs only after the person agrees.
    public void AskToQuit(Action quit)
    {
        _quitAfter = quit;
        _closing = null;
        Activate();
        Draw();
    }

    /// Aiko quits: the window closes without asking, and every terminal ends with it.
    public void Close()
    {
        _quitting = true;
        _window.Close();
    }

    /// The window again: from the tray or the card, also after it was hidden with programs running.
    public void Activate()
    {
        if (!_window.IsVisible)
        {
            Show();
            return;
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }

    /// New limits arrived: the percentage in the title follows them.
    public void LimitsChanged() => Draw();

    // ---- what the person did ----

    private void Change(TerminalWindowModel model)
    {
        _model = model;
        Draw();
    }

    private void Pick(string id)
    {
        _sessions = _sessions.Opened(id);
        Save();
        Change(_model.ToSession(id, _sessions));
    }

    /// The id of the last line of the quick pick, the one that opens the folder dialog.
    private const string OtherFolder = "\u0000other";

    /// The quick pick of a folder for a new session (D-350): the folders of the environment, the
    /// latest used first, and the folder dialog as the last line. Enter starts Claude Code there,
    /// Ctrl+Enter a command line.
    private void PickFolderQuickly()
    {
        var environment = _model.Environment;
        var palette = new PaletteView(Strings.TerminalPickFolderHint, Strings.TerminalPickFolderKeys, query =>
        [
            new PaletteGroup("", [.. FolderPick.Find(_sessions, environment, query)
                .Select(hit => new PaletteRow(hit.Folder, hit.Name, hit.Parent, []))]),
            new PaletteGroup("", [new PaletteRow(OtherFolder, [new TextPart(Strings.TerminalPickOtherFolder, false)], "", [], Quiet: true)]),
        ]);
        palette.Closed += _panel.ClosePalette;
        palette.Picked += (folder, control) =>
        {
            _panel.ClosePalette();
            var kind = control ? SessionKind.Shell : SessionKind.Claude;
            if (folder != OtherFolder)
            {
                StartIn(folder, kind);
            }
            else if (PickFolder(_model.Session(_sessions)?.Folder) is { } picked)
            {
                StartIn(picked, kind);
            }
        };
        _panel.ShowPalette(palette);
    }

    /// Ctrl and the key, with Shift or without (D-353). False leaves the key to the program.
    private bool KeyPressed(System.Windows.Input.Key key, bool shift, bool inTerminal)
    {
        // Ctrl+, opens the settings and closes them (D-374), as in VS Code and Warp.
        if (key == System.Windows.Input.Key.OemComma && !shift)
        {
            if (SettingsOpen)
            {
                CloseSettings();
            }
            else
            {
                OpenSettings();
            }

            return true;
        }

        // While the settings are open the keys of the sessions wait: the fields of a page take
        // Ctrl+C and Ctrl+V themselves, and nothing changes behind the settings.
        if (SettingsOpen && key is not (System.Windows.Input.Key.OemPlus or System.Windows.Input.Key.Add
            or System.Windows.Input.Key.OemMinus or System.Windows.Input.Key.Subtract
            or System.Windows.Input.Key.D0 or System.Windows.Input.Key.NumPad0))
        {
            return false;
        }

        switch (key)
        {
            case System.Windows.Input.Key.G when !shift && _model.Session(_sessions)?.Kind == SessionKind.Claude:
                // A long text for Claude Code in the editor, as the rich input of Warp (D-370).
                _panel.Editor.Compose();
                return true;
            case System.Windows.Input.Key.V when !shift && inTerminal:
                // A picture goes as a picture, a file as its path, text as text (D-370).
                if (OpenTerminal() is { } pasteInto && _model.Session(_sessions) is { } pasted)
                {
                    _ = WriteInTurn(pasteInto, TerminalPaste.FromClipboard(ThisComputer.Folders.PicturesFolder(pasted.Id)));
                }

                return true;
            case System.Windows.Input.Key.C when !shift && inTerminal && OpenTerminal()?.SelectedText is { Length: > 0 } chosen:
                // With a selection Ctrl+C copies, as in Windows Terminal; without one it stops the program.
                CopyText(chosen);
                return true;
            case System.Windows.Input.Key.OemPlus or System.Windows.Input.Key.Add:
                // Ctrl+= and Ctrl++ make the window larger, as in Windows Terminal (D-384).
                Zoom(WindowZoom.Larger);
                return true;
            case System.Windows.Input.Key.OemMinus or System.Windows.Input.Key.Subtract when !shift:
                Zoom(WindowZoom.Smaller);
                return true;
            case System.Windows.Input.Key.D0 or System.Windows.Input.Key.NumPad0 when !shift:
                Zoom(_ => WindowZoom.Default);
                return true;
            case System.Windows.Input.Key.T when shift:
                PickFolderQuickly();
                return true;
            case System.Windows.Input.Key.K when shift:
                OpenSessionPalette();
                return true;
            case System.Windows.Input.Key.B when shift:
                // Ctrl+B belongs to Claude Code (a command to the background) and to bash (D-376).
                if (_panel.ToggleListFromKeys())
                {
                    Remember(settings => settings with { SessionListHidden = !settings.SessionListHidden });
                }

                return true;
            case System.Windows.Input.Key.Tab:
                GoRound(shift ? -1 : 1);
                return true;
            case System.Windows.Input.Key.Space when shift:
                _panel.Editor.PressVoice();
                return true;
            case System.Windows.Input.Key.C when shift:
                // Copies what is selected in the terminal, as in Windows Terminal.
                if (OpenTerminal()?.SelectedText is { Length: > 0 } selected)
                {
                    CopyText(selected);
                }

                return true;
            default:
                return false;
        }
    }

    private void GoRound(int step)
    {
        var next = _model.ToNeighbour(step, _sessions);
        if (next.SessionId is not { } id || next == _model)
        {
            return;
        }

        _sessions = _sessions.Opened(id);
        Save();
        Change(next);
        _window.Dispatcher.BeginInvoke(FocusOpenTerminal, System.Windows.Threading.DispatcherPriority.Input);
    }

    /// The palette of sessions (D-347, D-353): the sessions of Aiko at once, and the conversations
    /// Claude Code keeps in the folders of the environments as soon as they are read.
    public void OpenSessionPalette()
    {
        if (!_window.IsVisible)
        {
            Show();
        }

        var palette = new PaletteView(Strings.TerminalSearchHint, Strings.TerminalSearchKeys, query =>
        {
            var found = SessionSearch.Find(_sessions, _conversations, query);
            var now = DateTimeOffset.Now;
            return
            [
                new PaletteGroup(Strings.TerminalSearchOpen, [.. found.Open.Select(hit => SearchRow(hit, now, quiet: false))]),
                new PaletteGroup(Strings.TerminalSearchClosed, [.. found.Closed.Select(hit => SearchRow(hit, now, quiet: true))]),
                new PaletteGroup(Strings.TerminalSearchOutside, [.. found.Outside.Select(hit => SearchRow(hit, now, quiet: true))]),
            ];
        });
        palette.Closed += _panel.ClosePalette;
        palette.Picked += (id, _) =>
        {
            _panel.ClosePalette();
            OpenFound(id);
        };
        _panel.ShowPalette(palette);

        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var folders = _settings.Environments
            .Select(environment => (environment.Name, environment.ConfigDirectories.FirstOrDefault() ?? ClaudeConfigFolder.Resolve(null, home)))
            .ToList();
        _ = LoadConversations(folders, palette);
    }

    private async Task LoadConversations(IReadOnlyList<(string, string)> folders, PaletteView palette)
    {
        _conversations = await _shelf.LoadAsync(folders);
        if (_panel.PaletteOpen)
        {
            palette.Refresh();
        }
    }

    private static PaletteRow SearchRow(SearchHit hit, DateTimeOffset now, bool quiet)
    {
        var when = TerminalPanel.UsedWords(hit.LastUsed, now);
        // "Yesterday, 16:02" stands in the middle of the line here, so it starts small.
        if (when.Length > 0)
        {
            when = char.ToLower(when[0], System.Globalization.CultureInfo.CurrentCulture) + when[1..];
        }

        var where = hit.Source switch
        {
            null => hit.Environment,
            ConversationSource.Terminal => Strings.TerminalSourceTerminal,
            ConversationSource.VsCode => "VS Code",
            ConversationSource.Desktop => "Claude Desktop",
            _ => Strings.TerminalSourceOther,
        };
        var meta = string.Join(" · ", new[] { where, hit.Folder, when }.Where(part => part.Length > 0));
        return new PaletteRow(hit.Id, hit.Name, meta, hit.Snippet, quiet);
    }

    /// A choice of the palette: a session of the list opens, a closed one comes back, and a
    /// conversation started outside Aiko joins the list and continues with --resume.
    private void OpenFound(string id)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (_sessions.Find(id) is not null)
        {
            _sessions = _sessions.Reopen(id, now);
        }
        else if (_conversations.FirstOrDefault(conversation => conversation.Id == id) is { } conversation)
        {
            _sessions = _sessions.Found(conversation, now);
        }

        if (_sessions.Find(id) is not { } session)
        {
            return;
        }

        Save();
        Change(_model.ToEnvironment(session.Environment, _sessions).ToSession(id, _sessions));
    }

    // ---- the editor (D-354, D-369) ----

    private void WireEditor()
    {
        var editor = _panel.Editor;
        editor.Passed += sequence => OpenTerminal()?.Type(sequence);
        editor.Inserted += writes =>
        {
            if (OpenTerminal() is { } terminal)
            {
                _ = WriteInTurn(terminal, writes);
            }
        };
        editor.WantedChanged += () => _window.Dispatcher.BeginInvoke(FocusOpenTerminal, System.Windows.Threading.DispatcherPriority.Input);
        editor.Sent += writes =>
        {
            if (OpenTerminal() is { } terminal)
            {
                _ = WriteInTurn(terminal, writes);
            }
        };
        editor.TerminalSelection = () => OpenTerminal()?.SelectedText;
        _voice = new VoiceDictation(editor, VoiceContextNow);
        _voice.SettingsAsked += () => OpenSettings(SettingsPanel.TerminalPageKey);
    }

    /// The writes of one message, one after another with a breath between them: Claude Code takes
    /// a paste and the Enter after it as one thing when they come together (spike 2026-10).
    private static async Task WriteInTurn(TerminalSurface terminal, IReadOnlyList<string> writes)
    {
        for (var at = 0; at < writes.Count; at++)
        {
            if (at > 0)
            {
                await Task.Delay(60);
            }

            terminal.Type(writes[at]);
        }
    }

    private TerminalSurface? OpenTerminal() =>
        _model.Session(_sessions) is { } open && _terminals.TryGetValue(open.Id, out var terminal) ? terminal : null;

    private VoiceContext? VoiceContextNow() =>
        _model.Session(_sessions) is { } session
            ? new VoiceContext(
                session.Folder,
                [],
                _settings.Environments.FirstOrDefault(one => one.Name == session.Environment)?.Persona == true)
            : null;

    /// The pictures pasted for a session go with it.
    private static void RemovePictures(string id)
    {
        try
        {
            var folder = ThisComputer.Folders.PicturesFolder(id);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Claude Code may still read one; the folder of a closed session is not used again.
        }
    }

    /// The editor takes the keys again, after a palette, a dialog or a click in the terminal: the
    /// keyboard is always there (D-354, D-369).
    private void FocusOpenTerminal()
    {
        if (_panel.Editor.Wanted)
        {
            _panel.Editor.FocusText();
        }
        else
        {
            OpenTerminal()?.FocusTerminal();
        }
    }

    /// Files dropped on the terminal: into the line of Claude Code, or into the editor of a command line.
    private void Dropped(IReadOnlyList<string> paths)
    {
        if (_panel.Editor.Wanted)
        {
            _panel.Editor.DropFiles(paths);
        }
        else if (OpenTerminal() is { } terminal)
        {
            _ = WriteInTurn(terminal, TerminalPaste.ForFiles(paths));
            terminal.FocusTerminal();
        }
    }

    private static void CopyText(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Another program holds the clipboard.
        }
    }

    /// A new session in a folder, in the environment of the open tab, with no question.
    private void StartIn(string folder, SessionKind kind)
    {
        if (Environment() is not { } environment)
        {
            return;
        }

        var id = Guid.NewGuid().ToString();
        var name = kind == SessionKind.Shell ? Shell().Name : "";
        _sessions = _sessions
            .Add(id, environment.Name, folder, name, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), kind)
            .Opened(id);
        Save();
        Change(_model.ToSession(id, _sessions));
    }

    /// The folder of a project in Explorer (D-408). False when the folder is gone from the disk:
    /// Explorer would open some other folder in its place.
    private static bool OpenInExplorer(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return false;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{folder.TrimEnd('\\')}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Explorer did not start; the folder is there, so there is nothing to say about it.
        }

        return true;
    }

    private string? PickFolder(string? start)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.TerminalChooseFolder };
        if (start is not null && Directory.Exists(start))
        {
            dialog.InitialDirectory = start;
        }

        return dialog.ShowDialog(_window) == true ? dialog.FolderName : null;
    }

    private void AskToClose(string id)
    {
        if (_terminals.TryGetValue(id, out var terminal) && terminal.IsRunning)
        {
            _closing = id;
            _quitAfter = null;
            Draw();
            return;
        }

        CloseSession(id);
    }

    private void Answer(bool stop)
    {
        var id = _closing;
        var quit = _quitAfter;
        _closing = null;
        _quitAfter = null;

        if (stop && quit is not null)
        {
            // The window must not hide itself on the way out.
            _quitting = true;
            quit();
        }
        else if (stop && id is not null)
        {
            CloseSession(id);
        }
        else
        {
            Draw();
        }
    }

    private void CloseSession(string id)
    {
        if (_terminals.Remove(id, out var terminal))
        {
            _panel.Release(terminal);
            terminal.Close();
        }

        _exited.Remove(id);
        Forget(id);
        _events.Forget(id);
        _panel.Editor.Forget(id);
        RemovePictures(id);
        _sessions = _sessions.Close(id);
        Save();
        Change(_model.After(_sessions));
    }

    /// The window is asked to close. With programs running it only hides (D-351), unless Aiko quits
    /// or the person agreed to stop them.
    private bool MayCloseWindow()
    {
        if (_quitting || !AnyRunning)
        {
            return true;
        }

        _window.Hide();
        return false;
    }

    // ---- the terminals ----

    /// The terminal of a session, started the first time the session is shown.
    private TerminalSurface? TerminalOf(TerminalSession session)
    {
        if (_terminals.TryGetValue(session.Id, out var known))
        {
            return known;
        }

        if (Launch(session) is not { } launch)
        {
            return null;
        }

        var terminal = new TerminalSurface(launch);
        terminal.Exited += code => Ended(session.Id, code);
        terminal.DrawnFirst += () =>
        {
            // The loader gives way to the terminal; the keys stay with the editor.
            Draw();
            if (_model.Session(_sessions)?.Id == session.Id)
            {
                _panel.Editor.FocusText();
            }
        };
        if (session.Kind == SessionKind.Claude)
        {
            terminal.TitleChanged += title => TitleCame(session.Id, title);
            terminal.Typed += () => Typed(session.Id);
        }

        _terminals[session.Id] = terminal;
        // An event left from an earlier run is not news.
        Forget(session.Id);
        _events.Forget(session.Id);
        _seenAt[session.Id] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var shell = session.Kind == SessionKind.Shell;
        TerminalLog.Started(session.Environment, shell, session.Started);
        if (!shell && !session.Started)
        {
            // From now on the conversation is Claude Code's, and the next start resumes it.
            _sessions = _sessions.MarkStarted(session.Id);
        }

        _sessions = _sessions.Used(session.Id, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Save();
        return terminal;
    }

    /// Whether Claude Code saved the conversation, in any project folder of the environment: the
    /// folder of the session may have moved since, and a long name gets a tail of Claude Code's own.
    private static bool ConversationOnDisk(string config, string id)
    {
        var projects = Path.Combine(config, "projects");
        try
        {
            return Directory.Exists(projects)
                && Directory.EnumerateDirectories(projects).Any(folder => File.Exists(Path.Combine(folder, id + ".jsonl")));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Unsure: resuming is what a started session did before.
            return true;
        }
    }

    private PtyLaunch? Launch(TerminalSession session)
    {
        if (_settings.Environments.FirstOrDefault(one => one.Name == session.Environment) is not { } environment)
        {
            return null;
        }

        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var config = environment.ConfigDirectories.FirstOrDefault() ?? ClaudeConfigFolder.Resolve(null, home);
        var folder = Directory.Exists(session.Folder) ? session.Folder : home;
        var inherited = System.Environment.GetEnvironmentVariables().Keys.Cast<string>().ToList();

        if (session.Kind == SessionKind.Shell)
        {
            var plan = ClaudeLaunchPlan.ForShell(config, home, inherited);
            var shell = Shell();
            return new PtyLaunch(shell.CommandLine, folder, Variables(plan));
        }

        if (ClaudeLauncher.FindClaude() is not { } claude)
        {
            return null;
        }

        var claudePlan = ClaudeLaunchPlan.For(
            new LaunchRequest(
                config, home, environment.Name, session.Id, session.Started, TerminalEvents.HooksFile(),
                ThisComputer.Folders.PicturesFolder(session.Id), session.Started && ConversationOnDisk(config, session.Id)),
            inherited);
        return new PtyLaunch(CommandLine(claude, claudePlan.Arguments), folder, Variables(claudePlan));
    }

    /// Claude Code ended by itself: its row says so, and Enter brings it back (D-351). A shell that
    /// ended leaves the list.
    private void Ended(string id, int code)
    {
        if (_sessions.Find(id) is not { } session)
        {
            return;
        }

        TerminalLog.Exited(session.Environment, session.Kind == SessionKind.Shell, code);
        if (session.Kind == SessionKind.Shell)
        {
            CloseSession(id);
            return;
        }

        _exited[id] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Draw();
    }

    /// A stopped Claude Code starts again in the same terminal place, with --resume.
    private void Resume()
    {
        if (_model.Session(_sessions) is not { } session || !_exited.Remove(session.Id))
        {
            return;
        }

        if (_terminals.Remove(session.Id, out var old))
        {
            _panel.Release(old);
            old.Close();
        }

        Draw();
    }

    // ---- what the sessions do ----

    /// Claude Code set its title. The turning mark changes every second while a turn goes on, so
    /// the list is drawn only when working or the name changed.
    private void TitleCame(string id, string text)
    {
        if (ClaudeTitle.Parse(text) is not { } title || (_titles.TryGetValue(id, out var before) && before == title))
        {
            return;
        }

        _titles[id] = title;
        if (title.SessionName is { } name && _sessions.Titled(id, name) is var titled && titled != _sessions)
        {
            _sessions = titled;
            Save();
        }

        Draw();
    }

    private void EventCame(string id, TerminalEvent terminalEvent)
    {
        if (!_terminals.ContainsKey(id))
        {
            return;
        }

        _lastEvents[id] = terminalEvent;
        Draw();
    }

    /// The person typed into the session: a question it asked is answered.
    private void Typed(string id)
    {
        var waiting = Live(id)?.Activity == TerminalActivity.Waiting;
        _answeredAt[id] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (waiting)
        {
            Draw();
        }
    }

    private void Forget(string id)
    {
        _titles.Remove(id);
        _lastEvents.Remove(id);
        _seenAt.Remove(id);
        _answeredAt.Remove(id);
        _activities.Remove(id);
    }

    // ---- the view ----

    private AikoEnvironment? Environment() => _settings.Environments.FirstOrDefault(one => one.Name == _model.Environment);

    private LiveTerminal? Live(string id)
    {
        if (_exited.ContainsKey(id))
        {
            return new LiveTerminal(TerminalActivity.Exited, "");
        }

        if (!_terminals.TryGetValue(id, out var terminal) || !terminal.IsRunning)
        {
            return null;
        }

        var last = _lastEvents.GetValueOrDefault(id);
        var activity = TerminalPulse.Of(_titles.GetValueOrDefault(id), last, _seenAt.GetValueOrDefault(id), _answeredAt.GetValueOrDefault(id));
        // The end of a turn says nothing the mark does not: the line shows the time again.
        return new LiveTerminal(activity, last is null || last.Kind == TerminalEventKind.Finished ? "" : EventLine(last));
    }

    /// The second line of a row (D-352): what the session did last, in the words of the person.
    private static string EventLine(TerminalEvent last)
    {
        var format = last.Kind switch
        {
            TerminalEventKind.Thinking => Strings.TerminalEventThinking,
            TerminalEventKind.Reading => Strings.TerminalEventReading,
            TerminalEventKind.Editing => Strings.TerminalEventEditing,
            TerminalEventKind.Running => Strings.TerminalEventRunning,
            TerminalEventKind.Searching => Strings.TerminalEventSearching,
            TerminalEventKind.Browsing => Strings.TerminalEventBrowsing,
            TerminalEventKind.Delegating => Strings.TerminalEventDelegating,
            TerminalEventKind.Using => Strings.TerminalEventUsing,
            TerminalEventKind.AsksPermission => Strings.TerminalEventAsksPermission,
            TerminalEventKind.AsksQuestion => Strings.TerminalEventAsksQuestion,
            TerminalEventKind.Failed => Strings.TerminalEventFailed,
            _ => Strings.TerminalEventOutOfLimit,
        };

        // A format without its detail drops the colon too: "running" rather than "running: ".
        return last.Detail.Length > 0 || !format.Contains("{0}", StringComparison.Ordinal)
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, format, last.Detail)
            : format.Replace(": {0}", "", StringComparison.Ordinal).Replace(" {0}", "", StringComparison.Ordinal);
    }

    private void Draw()
    {
        // The open session in a window in focus is seen: a turn that ends there is no news.
        if (TerminalPulse.Sees(_window.InView, _window.IsActive) && _model.Session(_sessions) is { } open)
        {
            _seenAt[open.Id] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        // The editor writes for the open session; a command line gets its own separator for Tab.
        if (_model.Session(_sessions) is { } session)
        {
            _panel.Editor.Show(new EditorSession(
                session.Id,
                session.Kind,
                session.Folder,
                session.Kind == SessionKind.Shell && Shell().Name != "bash" ? '\\' : '/',
                ThisComputer.Folders.PicturesFolder(session.Id)));
        }

        _panel.Show(View());
        Notice();
    }

    /// How many sessions wait, ended a turn, stopped and work (D-364, D-409): for the island and the
    /// tray. Raised when it changes; the taskbar button takes its sign here.
    public event Action<SessionCounts>? SessionsChanged;

    private SessionCounts _counts = SessionCounts.None;

    /// A session started to wait and the sound is due (D-375). The shell plays it.
    public event Action? WaitingStarted;

    /// What changed in the sessions since the last drawing: the counts, and the sound of a session
    /// that starts to wait (D-409). Everything that changes a session draws the window, so this is
    /// the one place to look.
    private void Notice()
    {
        var counts = SessionCounts.Of(_terminals.Keys
            .Select(id => (id, Activity: Counted(id)))
            .Where(pair => pair.Activity is not null)
            .Select(pair => (pair.id, pair.Activity!.Value)));
        if (counts != _counts)
        {
            _counts = counts;
            Button.ShowBadge(counts.Badge);
            SessionsChanged?.Invoke(counts);
        }

        var sounds = false;
        var sound = SettingsStore.Load().NotifySound;
        var inFocus = TerminalPulse.Sees(_window.InView, _window.IsActive);
        var open = _model.Session(_sessions)?.Id;
        foreach (var id in _terminals.Keys)
        {
            if (Live(id)?.Activity is not { } now)
            {
                continue;
            }

            TerminalActivity? before = _activities.TryGetValue(id, out var was) ? was : null;
            _activities[id] = now;
            sounds |= Notices.Sounds(before, now, sound, inFocus, id == open);
        }

        if (sounds)
        {
            WaitingStarted?.Invoke();
        }
    }

    /// What a session counts as: a stop the person has seen since is no longer news.
    private TerminalActivity? Counted(string id)
    {
        var activity = Live(id)?.Activity;
        return activity == TerminalActivity.Exited
            && !SessionCounts.StoppedIsNews(_exited.GetValueOrDefault(id), _seenAt.GetValueOrDefault(id))
            ? TerminalActivity.Idle
            : activity;
    }

    /// A click on the mark of "waits" on the island: the window comes forward on its session.
    public void OpenSession(string id)
    {
        Activate();
        if (_sessions.Find(id) is not { } session)
        {
            return;
        }

        _sessions = _sessions.Opened(id);
        Save();
        Change(_model.ToEnvironment(session.Environment, _sessions).ToSession(id, _sessions));
    }

    private TerminalView View()
    {
        var session = _model.Session(_sessions);

        AskView? ask = _quitAfter is not null
            ? new AskView(Strings.TerminalQuitTitle, Strings.TerminalQuitBody, Strings.TerminalStopAndQuit, Strings.TerminalDontQuit)
            : _closing is { } id && _sessions.Find(id) is { } closing
                ? new AskView(string.Format(Strings.TerminalCloseTitle, closing.Name), Strings.TerminalCloseBody, Strings.TerminalStopAndClose, Strings.TerminalKeepOpen)
                : null;

        var now = DateTimeOffset.Now;
        return new TerminalView(
            [.. _settings.Environments.Select(environment => Tab(environment.Name, now))],
            Header(now),
            SessionRows.For(_sessions, _model.Environment, Live),
            _model.Environment,
            session?.Id,
            session?.Name,
            _model.View(_sessions),
            ask,
            session is null ? null : TerminalOf(session),
            DateTimeOffset.Now)
        {
            Stopped = session is not null && _exited.ContainsKey(session.Id),
        };
    }

    private TabView Tab(string environment, DateTimeOffset now)
    {
        var badge = _limitsOf(environment) is { } snapshot ? LimitBadge.For(snapshot, now) : null;
        return new TabView(
            environment,
            environment == _model.Environment,
            SessionRows.Waiting(_sessions, environment, Live),
            badge?.Percent,
            badge?.Tone ?? LimitTone.Unknown);
    }

    /// The open header (D-373, D-376). The card is read only when the header is open: it is the
    /// same model the card of limits draws.
    private HeaderView Header(DateTimeOffset now)
    {
        var app = SettingsStore.Load();
        if (!app.HeaderOpen)
        {
            return new HeaderView(false, app.SessionListHidden, []);
        }

        var blocks = _cardOf().Blocks;
        var columns = HeaderStrip.Columns(
            [.. _settings.Environments.Select(environment => new HeaderInput(
                environment.Name,
                _limitsOf(environment.Name),
                SessionRows.Waiting(_sessions, environment.Name, Live)))],
            _model.Environment,
            now);

        return new HeaderView(true, app.SessionListHidden, [.. columns.Select(column =>
        {
            var environment = _settings.Environments.First(item => item.Name == column.Environment);
            return new HeaderColumnView(
                column,
                column.Active ? blocks.FirstOrDefault(block => block.Name == column.Environment) : null,
                environment.DirectMode);
        })]);
    }


    private void Save()
    {
        if (!_keepsNothing)
        {
            SettingsStore.SaveTerminal(_sessions);
        }
    }

    private readonly bool _keepsNothing;

    private static ShellLaunch Shell() => ShellLaunch.Find();

    private static string CommandLine(string program, IReadOnlyList<string> arguments) =>
        string.Join(' ', [$"\"{program}\"", .. arguments.Select(argument => argument.Contains(' ') ? $"\"{argument}\"" : argument)]);

    private static Dictionary<string, string?> Variables(ClaudeLaunchPlan plan) =>
        plan.Variables.ToDictionary(change => change.Name, change => change.Value, StringComparer.OrdinalIgnoreCase);
}
