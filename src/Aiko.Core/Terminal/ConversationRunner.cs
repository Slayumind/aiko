namespace Aiko.Core.Terminal;

/// What one conversation needs to start Claude Code. Started says that Claude Code already knows
/// the conversation: it was opened before, in this run of Aiko or in an earlier one.
public sealed record RunnerSetup(
    string Executable,
    string WorkingDirectory,
    string ConfigDirectory,
    string UserProfile,
    string EnvironmentName,
    string SessionId,
    bool Started,
    PermissionMode Mode,
    string? Model,
    IReadOnlyList<string> InheritedVariables);

/// Runs one conversation: keeps its state, starts Claude Code when the first message is sent and
/// passes lines both ways (D-304).
///
/// Everything here happens on one thread, the one of the window. A process calls back on a thread
/// of its own; those calls only fill a list, and post brings the work home. Lines that came
/// together are taken together, so the window hears about them once.
public sealed class ConversationRunner(
    RunnerSetup setup,
    Func<IConversationProcess> newProcess,
    Action<Action> post,
    ITerminalLog log)
{
    private readonly object _gate = new();
    private List<Arrival> _arrived = [];
    private bool _posted;
    private bool _taking;

    private IConversationProcess? _process;

    /// Grows with every start and every stop: what an older process still says is dropped.
    private int _run;

    private bool _known = setup.Started;

    /// Whether the process that runs now has said it is ready for a turn.
    private bool _ready;
    private int _linesRead;
    private int _linesWritten;

    public Conversation State { get; private set; } = Conversation.Empty(setup.Mode);

    /// Called on the thread of the window after the state has changed.
    public Action? Changed { get; set; }

    public bool IsRunning => _process is not null;

    public void Send(string text, IReadOnlyList<AttachedImage> images) => Apply(State.Send(text, images), startIfNeeded: true);

    public void Choose(string requestId, CardChoice choice) => Apply(State.Choose(requestId, choice), startIfNeeded: false);

    public void Interrupt() => Apply(State.Interrupt(), startIfNeeded: false);

    /// Without a process nothing is written: the next start carries the mode in its arguments.
    public void SetMode(PermissionMode mode) => Apply(State.SetMode(mode), startIfNeeded: false);

    public void SetModel(string model) => Apply(State.SetModel(model), startIfNeeded: false);

    public void MarkRead()
    {
        State = State.MarkRead();
        Changed?.Invoke();
    }

    /// Ends Claude Code. The conversation stays and can go on later with a new process.
    public void Close()
    {
        if (_process is null)
        {
            return;
        }

        _run++;
        _process.Stop();
        _process = null;
        log.Stopped(setup.EnvironmentName, _linesRead, _linesWritten);

        State = State.ProcessExited();
        Changed?.Invoke();
    }

    private void Apply(Outcome outcome, bool startIfNeeded)
    {
        // The state first: a scripted process answers before Write returns.
        State = outcome.State;

        if (outcome.Written.Count > 0 && _process is null && startIfNeeded && !Start())
        {
            State = State.ProcessExited();
        }

        Write(outcome.Written);
        Changed?.Invoke();
    }

    private bool Start()
    {
        var plan = ClaudeLaunchPlan.For(
            new LaunchRequest(
                setup.ConfigDirectory,
                setup.UserProfile,
                setup.EnvironmentName,
                setup.SessionId,
                _known,
                State.Mode,
                State.LatestModel is { Length: > 0 } model ? model : setup.Model),
            setup.InheritedVariables);

        var started = newProcess();
        var mine = ++_run;
        _linesRead = 0;
        _linesWritten = 0;
        _ready = false;

        // A scripted process answers from inside its start. What it says waits until the start
        // is over and logged, as it would with a real process on a thread of its own.
        var resumed = _known;
        _process = started;
        _taking = true;
        var ok = started.Start(
            new ProcessStart(setup.Executable, plan.Arguments, plan.Variables, setup.WorkingDirectory),
            line => Arrive(new Arrival(mine, line, 0)),
            code => Arrive(new Arrival(mine, null, code)));
        _taking = false;

        if (!ok)
        {
            _process = null;
            log.StartFailed(setup.EnvironmentName);
            return false;
        }

        log.Started(setup.EnvironmentName, resumed);
        Take();
        return true;
    }

    private void Write(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            if (_process is not { } running)
            {
                return;
            }

            // Counted first: a scripted process answers, and may even exit, before Write returns.
            _linesWritten++;
            if (!running.Write(line))
            {
                _linesWritten--;
            }
        }
    }

    /// Called on the thread of the process.
    private void Arrive(Arrival arrival)
    {
        lock (_gate)
        {
            _arrived.Add(arrival);
            if (_posted)
            {
                return;
            }
            _posted = true;
        }

        post(Take);
    }

    private void Take()
    {
        // A scripted process answers while its line is being taken. The loop below picks that up.
        if (_taking)
        {
            return;
        }

        _taking = true;
        var changed = false;
        while (true)
        {
            List<Arrival> batch;
            lock (_gate)
            {
                if (_arrived.Count == 0)
                {
                    _posted = false;
                    break;
                }
                batch = _arrived;
                _arrived = [];
            }

            foreach (var arrival in batch.Where(arrival => arrival.Run == _run))
            {
                Take(arrival);
                changed = true;
            }
        }
        _taking = false;

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    private void Take(Arrival arrival)
    {
        if (arrival.Line is not { } line)
        {
            _process = null;
            log.Exited(setup.EnvironmentName, arrival.ExitCode, _linesRead, _linesWritten);

            // Claude Code refuses a new id it has on disk and a resumed one it has not, and in
            // both cases leaves before it is ready. Which of the two happened is not known when
            // a process died early, so the next start tries the other way.
            if (!_ready)
            {
                _known = !_known;
            }
            State = State.ProcessExited();
            return;
        }

        _linesRead++;
        var streamEvent = StreamEvent.FromLine(line);

        // From here on Claude Code has the conversation on disk, and the next start resumes it.
        if (streamEvent is StreamEvent.Ready)
        {
            _ready = true;
            _known = true;
        }

        var outcome = State.On(streamEvent);
        State = outcome.State;
        Write(outcome.Written);
    }

    /// A line of a process, or its exit when the line is null.
    private sealed record Arrival(int Run, string? Line, int ExitCode);
}
