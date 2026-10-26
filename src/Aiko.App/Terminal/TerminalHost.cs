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
    private readonly VoiceDictation _voice;
    private readonly Func<IConversationProcess> _newProcess;
    private readonly Func<string, LimitSnapshot?> _limitsOf;
    private readonly TerminalPanel _panel = new();
    private readonly TerminalWindow _window;
    private readonly Dictionary<string, ConversationRunner> _talks = [];

    /// What a conversation said before its process started this time, read from the file of
    /// Claude Code (D-303). The process adds what comes after.
    private readonly Dictionary<string, Conversation> _histories = [];

    /// The commands of the menu of "/" and the models, by the name of the environment.
    private readonly Dictionary<string, ClaudeOffer> _offers = [];

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
        _voice = new VoiceDictation(_panel.Input, VoiceContextNow);
        _voice.SettingsAsked += () => SettingsAsked?.Invoke();

        // Voice input is turned on in the settings window; the window reads it again when it is used.
        _window.Activated += (_, _) => _voice.Use(SettingsStore.Load());
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
        _window.Closed += (_, _) =>
        {
            _voice.Close();
            InViewChanged?.Invoke(false);
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
        _panel.AskAnswered += Answer;
        _panel.SignInAsked += SignIn;
        _panel.SendAsked += (text, images) =>
        {
            // The first message of a new session makes it (D-342).
            if (_model.FormOpen && _form is { Folder: >= 0 })
            {
                Create(text, images);
            }
            else if (_model.Session(_sessions) is { } session)
            {
                Send(session.Id, text, images);
                Draw();
            }
        };
        _panel.StopAsked += () => Current()?.Interrupt();
        _panel.ModeAsked += NextMode;
        _panel.ModelAsked += NextModel;
        _panel.Chosen += (requestId, choice) => Current()?.Choose(requestId, choice);
    }

    public event Action? Closed;

    /// Voice input needs its model or key: the settings window should open.
    public event Action? SettingsAsked;

    /// The window came on screen, or left it by being minimized or closed.
    public event Action<bool>? InViewChanged;

    internal TerminalPanel Panel => _panel;

    public void Show()
    {
        Draw();
        _window.Show();
        _window.Activate();
        InViewChanged?.Invoke(_window.InView);
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

        // Another folder brings the mode its settings name; another model keeps the mode chosen.
        _form = FormFor(folder ?? _form.Folder, model ?? _form.Model, _form.Folders, folder is null ? _form.Mode : null);
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

    private FormView? FormFor(int? folder, int? model, IReadOnlyList<FolderChoice>? known = null, PermissionMode? mode = null)
    {
        if (Environment() is not { } environment)
        {
            return null;
        }

        var folders = known ?? NewSessionChoices.Folders(_sessions, environment, TerminalStore.FoldersInside);
        var chosen = folders.Count == 0 ? -1 : Math.Clamp(folder ?? 0, 0, folders.Count - 1);
        var path = chosen >= 0 ? folders[chosen].Path : null;
        var offer = OfferOf(environment.Name, path);
        var models = NewSessionChoices.Offered(offer.Models);
        var values = models.Select(one => one.Value).ToList();
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var config = environment.ConfigDirectories.FirstOrDefault() ?? ClaudeConfigFolder.Resolve(null, home);

        return new FormView(
            environment.Name,
            folders,
            chosen,
            path is null ? null : NewSessionChoices.BoundElsewhere(path, environment, _settings),
            Math.Clamp(model ?? values.IndexOf(NewSessionChoices.DefaultModel(_sessions, environment.Name, values)), 0, models.Count - 1),
            path is null ? "" : NewSessionChoices.DefaultName(_sessions, environment.Name, path))
        {
            Models = models,
            Mode = mode ?? (path is null ? PermissionMode.Manual : TerminalStore.DefaultMode(path, config)),
            Commands = offer.Commands,
        };
    }

    /// The first message makes the session: its name comes from the message (D-342).
    private void Create(string message, IReadOnlyList<AttachedImage> images)
    {
        if (_form is not { Folder: >= 0 } form)
        {
            return;
        }

        var id = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var folder = form.Folders[form.Folder].Path;
        var name = SessionName.FromMessage(message, form.DefaultName);
        _sessions = _sessions
            .Add(id, form.Environment, folder, name, form.Models[form.Model].Value, form.Mode, now)
            .Opened(id);
        Save();

        _form = null;
        _model = _model.WithForm(false).ToSession(id, _sessions);
        Send(id, message, images);
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

        _histories.Remove(id);

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

    private void Send(string id, string message, IReadOnlyList<AttachedImage>? images = null)
    {
        if (TalkFor(id) is not { } talk)
        {
            return;
        }

        var running = _talks
            .Where(one => one.Value.IsRunning)
            .Select(one => new RunningTalk(one.Key, one.Value.State.IsBusy, _sessions.Find(one.Key)?.LastUsed ?? 0))
            .ToList();
        if (ProcessCap.ToStop(running, id) is { } idle && _talks.Remove(idle, out var stopped))
        {
            // Its next process starts from the file again, which then holds this run too.
            stopped.Close();
            _histories.Remove(idle);
        }

        _sessions = _sessions.Used(id, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).MarkStarted(id);
        Save();
        talk.Send(message, images ?? []);
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
        ReadHistory(id);
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
        talk.Changed = () =>
        {
            KeepModel(id, talk.State);
            Draw();
        };
        _talks[id] = talk;
        return talk;
    }

    /// The full id Claude Code named is kept, so the session opens with it next time. While a
    /// model is asked, the session already keeps that one.
    private void KeepModel(string id, Conversation state)
    {
        if (state.AskedModel.Length == 0
            && state.Model.Length > 0
            && _sessions.Find(id) is { } session
            && session.Model != state.Model)
        {
            _sessions = _sessions.SetModel(id, state.Model);
            Save();
        }
    }

    /// The next mode of the session. It is kept for the next start, and a running Claude Code
    /// takes it at once.
    private void NextMode()
    {
        // A new session takes the mode for its start.
        if (_model.FormOpen && _form is { } form)
        {
            _form = form with { Mode = PermissionModes.Next(form.Mode) };
            Draw();
            return;
        }

        if (_model.Session(_sessions) is not { } session)
        {
            return;
        }

        var talk = Current();
        var next = PermissionModes.Next(talk?.State.Mode ?? session.Mode);
        _sessions = _sessions.SetMode(session.Id, next);
        Save();
        talk?.SetMode(next);
        Draw();
    }

    /// The next model of the session. It is kept for the next start, and a running Claude Code
    /// takes it from the next turn.
    private void NextModel()
    {
        // A new session goes round the models it offers.
        if (_model.FormOpen && _form is { Models.Count: > 0 } form)
        {
            Refill(null, (form.Model + 1) % form.Models.Count);
            return;
        }

        if (_model.Session(_sessions) is not { } session)
        {
            return;
        }

        var talk = Current();
        var shown = talk?.State.LatestModel is { Length: > 0 } running ? running : session.Model;
        var offered = NewSessionChoices.Offered(OfferOf(session.Environment, session.Folder).Models);
        var next = NewSessionChoices.NextModel(shown, [.. offered.Select(one => one.Value)]);
        _sessions = _sessions.SetModel(session.Id, next);
        Save();
        talk?.SetModel(next);
        Draw();
    }

    private ConversationRunner? Current() =>
        _model.Session(_sessions) is { } session && _talks.TryGetValue(session.Id, out var talk) ? talk : null;

    private void ReadHistory(string id)
    {
        var session = _sessions.Find(id);
        if (session is not { Started: true }
            || _settings.Environments.FirstOrDefault(one => one.Name == session.Environment) is not { } environment)
        {
            _histories[id] = Conversation.Empty(session?.Mode ?? PermissionMode.Manual);
            return;
        }

        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        _histories[id] = TerminalStore.History(
            environment.ConfigDirectories.FirstOrDefault() ?? ClaudeConfigFolder.Resolve(null, home),
            session.Folder,
            session.Id,
            session.Mode);
    }

    private TalkView? Talk(TerminalSession? session)
    {
        if (session is null)
        {
            return null;
        }

        // The window opens on a session nobody picked yet: its past is read here, once.
        if (!_histories.ContainsKey(session.Id))
        {
            ReadHistory(session.Id);
        }

        var past = _histories[session.Id].Items;
        if (!_talks.TryGetValue(session.Id, out var talk))
        {
            return new TalkView(session.Id, past, false, false, false, session.Mode, ModelName.Label(null, session.Model), session.Environment)
            {
                Commands = CommandsOf(session),
                Folder = session.Folder,
            };
        }

        var state = talk.State;
        return new TalkView(
            session.Id,
            [.. past, .. state.Items],
            state.IsBusy,
            state.HasOpenCard,
            state.Compacting,
            state.Mode,
            ModelName.Label(state.AskedModel, state.Model.Length > 0 ? state.Model : session.Model),
            session.Environment)
        {
            Commands = CommandsOf(session),
            Folder = session.Folder,
        };
    }

    private IReadOnlyList<SlashCommand> CommandsOf(TerminalSession session) => OfferOf(session.Environment, session.Folder).Commands;

    /// The commands of the menu of "/" and the models of an environment. An environment is asked
    /// once, when its first session or its form is shown; until the answer comes, the offer is
    /// empty: the menu stays shut and the form has its own three models.
    private ClaudeOffer OfferOf(string environmentName, string? folder)
    {
        if (_offers.TryGetValue(environmentName, out var known))
        {
            return known;
        }

        _offers[environmentName] = ClaudeOffer.None;
        if (_settings.Environments.FirstOrDefault(one => one.Name == environmentName) is { } environment
            && ClaudeLauncher.FindClaude() is { } claude)
        {
            var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            new CommandLoader(_newProcess, work => _window.Dispatcher.BeginInvoke(work)).Load(
                new CommandSetup(
                    claude,
                    folder is not null && Directory.Exists(folder) ? folder : home,
                    environment.ConfigDirectories.FirstOrDefault() ?? ClaudeConfigFolder.Resolve(null, home),
                    home,
                    [.. System.Environment.GetEnvironmentVariables().Keys.Cast<string>()]),
                offer =>
                {
                    _offers[environmentName] = offer;

                    // An open form takes the models it did not have yet.
                    if (_form is { } form && form.Environment == environmentName)
                    {
                        _form = FormFor(form.Folder, null, form.Folders);
                    }

                    Draw();
                });
        }

        return ClaudeOffer.None;
    }

    // ---- the view ----

    private VoiceContext? VoiceContextNow() =>
        _model.Session(_sessions) is { } session
            ? new VoiceContext(
                session.Folder,
                VoiceHint.Files(Talk(session)?.Items ?? []),
                _settings.Environments.FirstOrDefault(one => one.Name == session.Environment)?.Persona == true)
            : null;

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
            signedOut ? null : Talk(session),
            DateTimeOffset.Now);
    }

    private BadgeView? Badge(DateTimeOffset now) =>
        _limitsOf(_model.Environment) is { } snapshot && LimitBadge.For(snapshot, now) is { } badge
            ? new BadgeView(badge.Percent, badge.Tone, badge.Kind)
            : null;

    private void Save() => SettingsStore.SaveTerminal(_sessions);
}
