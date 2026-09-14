using System.Windows.Threading;
using Aiko.Core;

namespace Aiko.App;

/// Asks the usage API for the environments where direct mode is switched on.
///
/// Direct mode exists for people who work in the IDE panel or in Claude Desktop, where the status
/// line is never run, and for the weekly limit of the heavy model, which the status line does not
/// carry. It is off until the user turns it on.
sealed class DirectPoller : IDisposable
{
    /// How often the clock is looked at. Not how often the server is asked: that is at least
    /// three minutes per account, and longer after an error.
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(20);

    /// Accounts are asked one after another, not all at once.
    private static readonly TimeSpan Stagger = TimeSpan.FromSeconds(15);

    private readonly UsageClient _client = new();
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, Account> _accounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, LimitSnapshot> _latest = new(StringComparer.OrdinalIgnoreCase);

    private bool _asking;

    public DirectPoller(Dispatcher dispatcher)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = Tick };
        _timer.Tick += OnTick;
    }

    /// One round at a time.
    ///
    /// The clock ticks every twenty seconds, and a round waits on one request per due account, up
    /// to ten seconds each. With three accounts due at once the rounds overlapped and asked the
    /// server again while the first round was still waiting for it. The tick runs on the UI
    /// thread, so a plain flag is enough.
    private async void OnTick(object? sender, EventArgs e)
    {
        if (_asking)
        {
            return;
        }

        _asking = true;
        try
        {
            await RoundAsync().ConfigureAwait(true);
        }
        finally
        {
            _asking = false;
        }
    }

    /// Raised when an account answered with fresh numbers.
    public event Action? Reported;

    /// The newest answer per environment, by the name the user gave it.
    public IReadOnlyDictionary<string, LimitSnapshot> Latest => _latest;

    /// Called at startup and whenever the settings change: environments come and go, and direct
    /// mode is switched per environment.
    public void Follow(EnvironmentSettings settings)
    {
        var wanted = settings.Environments.Where(e => e.DirectMode).ToList();

        foreach (var gone in _accounts.Keys.Except(wanted.Select(e => e.Name), StringComparer.OrdinalIgnoreCase).ToList())
        {
            _accounts.Remove(gone);
            _latest.Remove(gone);
        }

        // What the last run found, for the environments that still exist. The card shows it at
        // once, marked with its time, instead of waiting three minutes for the first answer. Read
        // here rather than in the constructor because only now do we know which names are real:
        // a cached file for an environment that was renamed or switched off must not come back.
        foreach (var (name, snapshot) in DirectCache.ReadFor(wanted.Select(e => e.Name)))
        {
            if (!_latest.ContainsKey(name))
            {
                _latest[name] = snapshot;
            }
        }

        var now = DateTimeOffset.Now;
        var place = 0;

        foreach (var environment in wanted)
        {
            if (!_accounts.ContainsKey(environment.Name))
            {
                _accounts[environment.Name] = new Account(environment.Name, environment.ConfigDirectories)
                {
                    // The first round is spread out, so two accounts do not knock at once.
                    NextAt = now + (Stagger * place),
                };
            }
            place++;
        }

        if (_accounts.Count == 0)
        {
            _timer.Stop();
            return;
        }

        if (!_timer.IsEnabled)
        {
            _timer.Start();
            Log.Write($"direct mode on for {_accounts.Count} environment(s)");
        }
    }

    private async Task RoundAsync()
    {
        var now = DateTimeOffset.Now;
        var answered = false;

        foreach (var account in _accounts.Values.ToList())
        {
            if (account.NextAt > now)
            {
                continue;
            }

            answered |= await AskAsync(account, now).ConfigureAwait(true);
        }

        if (answered)
        {
            Reported?.Invoke();
        }
    }

    private async Task<bool> AskAsync(Account account, DateTimeOffset now)
    {
        // Read again every time, and only into memory: the token changes when Claude Code
        // refreshes it, and we keep no copy of our own.
        var credential = account.Directories
            .Select(Credentials.Read)
            .FirstOrDefault(found => found is not null);

        if (credential is null)
        {
            account.Wait(account.Backoff.AfterFailure(), now);
            return false;
        }

        if (credential.IsExpiredAt(now))
        {
            // An expired token is not an error and not ours to fix. Claude Code refreshes it on
            // its own; until then the card shows what it already has, marked with its time.
            account.Wait(account.Backoff.AfterFailure(), now);
            Log.Write($"direct mode for {account.Name}: the token has expired, waiting for Claude Code");
            return false;
        }

        var answer = await _client.AskAsync(credential.AccessToken, CancellationToken.None).ConfigureAwait(true);

        switch (answer.Outcome)
        {
            case AskOutcome.Fine when answer.Report.HasData:
                var fresh = LimitSnapshot.FromDirectMode(account.Name, now, answer.Report);
                _latest[account.Name] = fresh;
                DirectCache.Write(fresh);
                account.Wait(account.Backoff.AfterSuccess(), now);

                // Percentages only. The token never appears here, and neither does the body.
                Log.Write(
                    $"direct mode for {account.Name}: " +
                    string.Join(", ", answer.Report.Windows.Select(w => $"{w.Kind} {w.Percent}%")) +
                    (answer.Report.Model is { } model ? $", {model.ModelName} {model.Percent}%" : string.Empty));
                return true;

            case AskOutcome.Fine:
                account.Wait(account.Backoff.AfterSuccess(), now);
                return false;

            case AskOutcome.TooMany:
                account.Wait(account.Backoff.AfterTooManyRequests(answer.RetryAfter), now);
                Log.Write($"direct mode for {account.Name}: too many requests, waiting {account.Backoff.Delay.TotalSeconds:0} s");
                return false;

            default:
                account.Wait(account.Backoff.AfterFailure(), now);
                Log.Write($"direct mode for {account.Name}: {answer.Problem}, waiting {account.Backoff.Delay.TotalSeconds:0} s");
                return false;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _client.Dispose();
    }

    private sealed class Account(string name, IReadOnlyList<string> directories)
    {
        public string Name { get; } = name;

        public IReadOnlyList<string> Directories { get; } = directories;

        public PollBackoff Backoff { get; private set; } = PollBackoff.Start;

        public DateTimeOffset NextAt { get; set; }

        public void Wait(PollBackoff backoff, DateTimeOffset now)
        {
            Backoff = backoff;
            NextAt = backoff.NextAttemptAfter(now);
        }
    }
}
