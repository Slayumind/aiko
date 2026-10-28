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
    private readonly TerminalPanel _panel = new();
    private readonly TerminalWindow _window;
    private readonly Dictionary<string, TerminalSurface> _terminals = [];

    /// Sessions of Claude Code whose program ended by itself (D-351): the row says "stopped" and
    /// Enter starts it again with --resume.
    private readonly HashSet<string> _exited = [];

    private TerminalSessions _sessions;
    private TerminalWindowModel _model;
    private string? _closing;
    private bool _closingWindow;
    private bool _quitting;

    /// limitsOf gives the latest limits of an environment by its name.
    public TerminalHost(EnvironmentSettings settings, TerminalSessions sessions, Func<string, LimitSnapshot?> limitsOf)
    {
        _settings = settings;
        _sessions = sessions;
        _limitsOf = limitsOf;
        _model = TerminalWindowModel.Opened(sessions, [.. settings.Environments.Select(environment => environment.Name)]);

        _window = new TerminalWindow(_panel, WindowPlace.Fit(sessions.Window, WorkAreas.All()));
        _window.MayClose = MayCloseWindow;
        _window.Closing += (_, e) =>
        {
            if (!e.Cancel)
            {
                _sessions = _sessions.Placed(_window.Place);
                Save();
            }
        };
        _window.StateChanged += (_, _) => InViewChanged?.Invoke(_window.InView);
        _window.IsVisibleChanged += (_, _) => InViewChanged?.Invoke(_window.InView);
        _window.Closed += (_, _) =>
        {
            InViewChanged?.Invoke(false);
            foreach (var terminal in _terminals.Values)
            {
                terminal.Close();
            }

            Closed?.Invoke();
        };

        _panel.TabPicked += name => Change(_model.ToEnvironment(name, _sessions));
        _panel.RowPicked += Pick;
        _panel.RowClosed += AskToClose;
        _panel.RowRenamed += (id, name) =>
        {
            _sessions = _sessions.Rename(id, name);
            Save();
            Draw();
        };
        _panel.CreateAsked += () => Create(SessionKind.Claude);
        _panel.ShellAsked += () => Create(SessionKind.Shell);
        _panel.ResumeAsked += Resume;
        _panel.AskAnswered += Answer;
    }

    public event Action? Closed;

    /// The window came on screen, or left it by being minimized, hidden or closed.
    public event Action<bool>? InViewChanged;

    internal TerminalPanel Panel => _panel;

    /// Whether a program still runs in a terminal of the window.
    public bool AnyRunning => _terminals.Values.Any(terminal => terminal.IsRunning);

    public void Show()
    {
        Draw();
        _window.Show();
        _window.Activate();
        InViewChanged?.Invoke(_window.InView);
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

    /// A new session in a folder the person picks, in the environment of the open tab. The quick
    /// pick of a folder and "+" at a group of the list come with the list of sessions (AIKO-411).
    private void Create(SessionKind kind)
    {
        var current = _model.Session(_sessions);
        var folder = kind == SessionKind.Shell && current is not null ? current.Folder : PickFolder(current?.Folder);
        if (folder is null || Environment() is not { } environment)
        {
            return;
        }

        var id = Guid.NewGuid().ToString();
        var name = kind == SessionKind.Shell ? Shell().Name : "";
        _sessions = _sessions
            .Add(id, environment.Name, folder, name, null, PermissionMode.Manual, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), kind)
            .Opened(id);
        Save();
        Change(_model.ToSession(id, _sessions));
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
            _closingWindow = false;
            Draw();
            return;
        }

        CloseSession(id);
    }

    private void Answer(bool stop)
    {
        var id = _closing;
        var window = _closingWindow;
        _closing = null;
        _closingWindow = false;

        if (stop && window)
        {
            _quitting = true;
            _window.Close();
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
        _terminals[session.Id] = terminal;

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

        var claudePlan = ClaudeLaunchPlan.For(new LaunchRequest(config, home, environment.Name, session.Id, session.Started), inherited);
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

        _exited.Add(id);
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

    // ---- the view ----

    private AikoEnvironment? Environment() => _settings.Environments.FirstOrDefault(one => one.Name == _model.Environment);

    private LiveTerminal? Live(string id) =>
        _exited.Contains(id) ? new LiveTerminal(TerminalActivity.Exited, "")
        : _terminals.TryGetValue(id, out var terminal) && terminal.IsRunning ? new LiveTerminal(TerminalActivity.Idle, "")
        : null;

    private void Draw() => _panel.Show(View());

    private TerminalView View()
    {
        var session = _model.Session(_sessions);

        AskView? ask = _closingWindow
            ? new AskView(Strings.TerminalCloseWindowTitle, Strings.TerminalCloseWindowBody)
            : _closing is { } id && _sessions.Find(id) is { } closing
                ? new AskView(string.Format(Strings.TerminalCloseTitle, closing.Name), Strings.TerminalCloseBody)
                : null;

        return new TerminalView(
            [.. _settings.Environments.Select(environment => new TabView(
                environment.Name,
                environment.Name == _model.Environment,
                SessionRows.Waiting(_sessions, environment.Name, Live)))],
            Badge(DateTimeOffset.Now),
            SessionRows.For(_sessions, _model.Environment, Live),
            _model.Environment,
            session?.Id,
            session?.Name,
            _model.View(_sessions),
            ask,
            session is null ? null : TerminalOf(session),
            DateTimeOffset.Now)
        {
            Stopped = session is not null && _exited.Contains(session.Id),
        };
    }

    private BadgeView? Badge(DateTimeOffset now) =>
        _limitsOf(_model.Environment) is { } snapshot && LimitBadge.For(snapshot, now) is { } badge
            ? new BadgeView(badge.Percent, badge.Tone, badge.Kind)
            : null;


    private void Save() => SettingsStore.SaveTerminal(_sessions);

    /// The shell of a session and the name its row starts with.
    private sealed record ShellProgram(string CommandLine, string Name);

    /// Git Bash when it is there, as Claude Code itself prefers it (RESEARCH); otherwise PowerShell 7,
    /// otherwise Windows PowerShell.
    private static ShellProgram Shell()
    {
        var gitBash = System.Environment.GetEnvironmentVariable("CLAUDE_CODE_GIT_BASH_PATH") is { Length: > 0 } set && File.Exists(set)
            ? set
            : new[]
            {
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe"),
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "bin", "bash.exe"),
            }.FirstOrDefault(File.Exists);

        return gitBash is not null ? new ShellProgram($"\"{gitBash}\" --login -i", "bash")
            : FindOnPath("pwsh.exe") is { } pwsh ? new ShellProgram($"\"{pwsh}\" -NoLogo", "pwsh")
            : new ShellProgram("powershell.exe -NoLogo", "powershell");
    }

    private static string? FindOnPath(string file) =>
        (System.Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(folder => Path.Combine(folder.Trim(), file))
            .FirstOrDefault(File.Exists);

    private static string CommandLine(string program, IReadOnlyList<string> arguments) =>
        string.Join(' ', [$"\"{program}\"", .. arguments.Select(argument => argument.Contains(' ') ? $"\"{argument}\"" : argument)]);

    private static Dictionary<string, string?> Variables(ClaudeLaunchPlan plan) =>
        plan.Variables.ToDictionary(change => change.Name, change => change.Value, StringComparer.OrdinalIgnoreCase);
}
