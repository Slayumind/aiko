using System.Windows.Threading;
using Aiko.Core;

namespace Aiko.App;

/// Asks slayumind.org once a day whether a newer Aiko exists, and says that one copy ran — each
/// when the user has said it may. Two switches, one request (D-228).
///
/// The switch for this has existed since the first version and nothing ever read it: the only
/// check was the button in the settings window. A switch that does nothing is worse than no
/// switch, because it is also a promise.
///
/// Nothing pops up. The answer goes into the tray tooltip and into the settings window, which is
/// as loud as a tray app should be about a version number.
sealed class UpdateWatch : IDisposable
{
    /// Not at once: the first minutes after login belong to the machine, not to us.
    private static readonly TimeSpan FirstCheck = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan BetweenChecks = TimeSpan.FromHours(24);

    private readonly DispatcherTimer _timer;
    private readonly CancellationTokenSource _stopping = new();
    private bool _asking;

    public UpdateWatch(Dispatcher dispatcher)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = FirstCheck };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// The newer version, once one is known. Null while Aiko is the latest, while the answer has
    /// not come yet, and whenever the check could not be made.
    public string? NewerVersion { get; private set; }

    public event Action? Found;

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _stopping.Cancel();
        _stopping.Dispose();
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        // The first interval is short; every one after it is a day.
        _timer.Interval = BetweenChecks;

        var settings = SettingsStore.Load();

        // Either switch is a reason to make the request, and the request is the same one. With
        // update checks off and the count on, the version in the answer is simply not read: the
        // switch promises that Aiko does not go looking for a new version, and it does not.
        if (_asking || (!settings.CheckUpdates && !settings.SendStats))
        {
            return;
        }

        _asking = true;
        try
        {
            var info = await UpdateRun.AskAsync(_stopping.Token).ConfigureAwait(true);

            if (_stopping.IsCancellationRequested || !settings.CheckUpdates)
            {
                return;
            }

            // Only a definite "there is a newer one" is worth showing. "Could not check" says
            // nothing about the version, and saying nothing is the honest answer.
            var newer = info.CompareWith(AppVersion.Current()) == UpdateState.Available
                ? info.Latest
                : null;

            if (newer == NewerVersion)
            {
                return;
            }

            NewerVersion = newer;
            Log.Write(newer is null ? "update check: nothing newer" : $"update check: {newer} is out");
            Found?.Invoke();
        }
        finally
        {
            _asking = false;
        }
    }
}
