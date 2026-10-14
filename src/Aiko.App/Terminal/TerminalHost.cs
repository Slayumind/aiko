using System.IO;
using System.Windows;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The terminal behind its window (D-292, D-293, D-304): the sessions of the file, the
/// conversations in memory and what is chosen. Every event of the panel ends in a new view.
///
/// The window is made when the terminal is opened and is gone when it is closed; with it go the
/// processes of Claude Code. The conversations come back from their files the next time.
[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.17763")]
sealed class TerminalHost
{
    private readonly EnvironmentSettings _settings;
    private readonly Func<IConversationProcess> _newProcess;
    private readonly Func<string, LimitSnapshot?> _limitsOf;
    private readonly TerminalPanel _panel = new();
    private readonly TerminalWindow _window;
    private readonly Dictionary<string, ConversationRunner> _talks = [];

    private TerminalSessions _sessions;
    private TerminalWindowModel _model;
    private FormView? _form;
    private string? _closing;
    private bool _closingWindow;
    private bool _agreedToClose;

    /// limitsOf gives the latest limits of an environment by its name.
    public TerminalHost(
        EnvironmentSettings settings,
        TerminalSessions sessions,
        Func<IConversationProcess> newProcess,
        Func<string, LimitSnapshot?> limitsOf)
    {
        _settings = settings;
        _sessions = sessions;
        _newProcess = newProcess;
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
        _window.Closed += (_, _) =>
        {
            foreach (var talk in _talks.Values)
            {
                talk.Close();
            }

            Closed?.Invoke();
        };

        _panel.TabPicked += name => Change(_model.ToEnvironment(name, _sessions));
        _panel.RowPicked += Pick;
        _panel.RowClosed += AskToClose;
        _panel.CreateAsked += OpenForm;
        _panel.FolderPicked += index => Refill(index, null);
        _panel.OtherFolderAsked += PickOtherFolder;
        _panel.ModelPicked += index => Refill(null, index);
        _panel.Submitted += Create;
        _panel.FormCancelled += () => Change(_model.WithForm(false));
        _panel.AskAnswered += Answer;
        _panel.SignInAsked += SignIn;
    }

    public event Action? Closed;

    internal TerminalPanel Panel => _panel;

    public void Show()
    {
        Draw();
        _window.Show();
        _window.Activate();
    }

    /// Aiko quits: the window closes without asking, as the processes end with Aiko anyway.
    public void Close()
    {
        _agreedToClose = true;
        _window.Close();
    }

    public void Activate()
    {
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
        if (!model.FormOpen)
        {
            _form = null;
        }

        Draw();
    }

    private void Pick(string id)
    {
        _sessions = _sessions.Opened(id);
        Save();
        if (_talks.TryGetValue(id, out var talk))
        {
            talk.MarkRead();
        }

        Change(_model.ToSession(id, _sessions));
    }

    private void OpenForm()
    {
        _model = _model.WithForm(true);
        _form = FormFor(null, null);
        Draw();
    }

    /// The form again after a choice in it: another folder changes the hint and the name.
    private void Refill(int? folder, int? model)
    {
        if (_form is null)
        {
            return;
        }

        _form = FormFor(folder ?? _form.Folder, model ?? _form.Model, _form.Folders);
        Draw();
    }

    private void PickOtherFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Strings.TerminalChooseFolder };
        if (_form is null || dialog.ShowDialog(_window) != true)
        {
            Draw();
            return;
        }

        var path = SessionName.FolderPath(dialog.FolderName);
        var folders = _form.Folders.ToList();
        var at = folders.FindIndex(folder => folder.Path == path);
        if (at < 0)
        {
            folders.Insert(0, new FolderChoice(path, SessionName.FolderName(path), FolderSource.Recent));
            at = 0;
        }

        _form = FormFor(at, _form.Model, folders);
        Draw();
    }

    private FormView? FormFor(int? folder, int? model, IReadOnlyList<FolderChoice>? known = null)
    {
        if (Environment() is not { } environment)
        {
            return null;
        }

        var folders = known ?? NewSessionChoices.Folders(_sessions, environment, TerminalStore.FoldersInside);
        var chosen = folders.Count == 0 ? -1 : Math.Clamp(folder ?? 0, 0, folders.Count - 1);
        var path = chosen >= 0 ? folders[chosen].Path : null;

        return new FormView(
            environment.Name,
            folders,
            chosen,
            path is null ? null : NewSessionChoices.BoundElsewhere(path, environment, _settings),
            model ?? Math.Max(0, NewSessionChoices.Models.ToList().IndexOf(NewSessionChoices.DefaultModel(_sessions, environment.Name))),
            path is null ? "" : NewSessionChoices.DefaultName(_sessions, environment.Name, path));
    }

    private void Create(string name, string message)
    {
        if (_form is not { Folder: >= 0 } form)
        {
            return;
        }

        var id = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _sessions = _sessions
            .Add(id, form.Environment, form.Folders[form.Folder].Path, name, NewSessionChoices.Models[form.Model], PermissionMode.Manual, now)
            .Opened(id);
        Save();

        _form = null;
        _model = _model.WithForm(false).ToSession(id, _sessions);
        if (!string.IsNullOrWhiteSpace(message))
        {
            Send(id, message);
        }

        Draw();
    }

    private void AskToClose(string id)
    {
        if (_talks.TryGetValue(id, out var talk) && talk.State.IsBusy)
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
            _agreedToClose = true;
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
        if (_talks.Remove(id, out var talk))
        {
            talk.Close();
        }

        _sessions = _sessions.Close(id);
        Save();
        Change(_model.After(_sessions));
    }

    private bool MayCloseWindow()
    {
        if (_agreedToClose || !_talks.Values.Any(talk => talk.State.IsBusy))
        {
            return true;
        }

        _closing = null;
        _closingWindow = true;
        Draw();
        return false;
    }

    private void SignIn()
    {
        if (Environment() is { } environment && _model.Session(_sessions) is { } session)
        {
            ClaudeLauncher.Open(environment.ConfigDirectories.FirstOrDefault() ?? "", session.Folder);
        }
    }

    // ---- the conversations ----

    private void Send(string id, string message)
    {
        if (TalkFor(id) is not { } talk)
        {
            return;
        }

        var running = _talks
            .Where(one => one.Value.IsRunning)
            .Select(one => new RunningTalk(one.Key, one.Value.State.IsBusy, _sessions.Find(one.Key)?.LastUsed ?? 0))
            .ToList();
        if (ProcessCap.ToStop(running, id) is { } idle)
        {
            _talks[idle].Close();
        }

        _sessions = _sessions.Used(id, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).MarkStarted(id);
        Save();
        talk.Send(message, []);
    }

    private ConversationRunner? TalkFor(string id)
    {
        if (_talks.TryGetValue(id, out var known))
        {
            return known;
        }

        if (_sessions.Find(id) is not { } session
            || _settings.Environments.FirstOrDefault(one => one.Name == session.Environment) is not { } environment
            || ClaudeLauncher.FindClaude() is not { } claude)
        {
            return null;
        }

        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var talk = new ConversationRunner(
            new RunnerSetup(
                claude,
                Directory.Exists(session.Folder) ? session.Folder : home,
                environment.ConfigDirectories.FirstOrDefault() ?? ClaudeConfigFolder.Resolve(null, home),
                home,
                environment.Name,
                session.Id,
                session.Started,
                session.Mode,
                session.Model,
                [.. System.Environment.GetEnvironmentVariables().Keys.Cast<string>()]),
            _newProcess,
            work => _window.Dispatcher.BeginInvoke(work),
            new TerminalLog());
        talk.Changed = Draw;
        _talks[id] = talk;
        return talk;
    }

    // ---- the view ----

    private AikoEnvironment? Environment() => _settings.Environments.FirstOrDefault(one => one.Name == _model.Environment);

    private LiveTalk? Live(string id) =>
        _talks.TryGetValue(id, out var talk) ? new LiveTalk(TalkGlance.Of(talk.State), talk.IsRunning) : null;

    private void Draw() => _panel.Show(View());

    private TerminalView View()
    {
        var session = _model.Session(_sessions);
        var signedOut = session is not null && Live(session.Id)?.Glance.Activity == TalkActivity.SignedOut;

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
            signedOut,
            _form,
            ask,
            DateTimeOffset.Now);
    }

    private BadgeView? Badge(DateTimeOffset now) =>
        _limitsOf(_model.Environment) is { } snapshot && LimitBadge.For(snapshot, now) is { } badge
            ? new BadgeView(badge.Percent, badge.Tone, badge.Kind)
            : null;

    private void Save() => SettingsStore.SaveTerminal(_sessions);
}
