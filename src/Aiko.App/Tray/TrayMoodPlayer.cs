using System.Windows.Threading;
using Aiko.Core;

namespace Aiko.App;

/// Turns session and limit changes into a face for two seconds (D-199, D-211). What face and for how
/// long is decided in Aiko.Core.TrayMood; this class only listens and keeps the one short timer.
///
/// With the persona off everywhere nothing is watched at all, and the timer only runs while a face
/// is showing.
sealed class TrayMoodPlayer : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, int?> _percents = new(StringComparer.OrdinalIgnoreCase);

    private ActivityWatcher? _activity;
    private HashSet<string> _talking = new(StringComparer.OrdinalIgnoreCase);
    private FaceMoment? _showing;

    public TrayMoodPlayer(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = TrayMood.ShowFor };
        _timer.Tick += (_, _) => Hide();
    }

    /// The face to draw, or null for the rings. Raised on the dispatcher thread.
    public event Action<AikoFace?>? FaceChanged;

    public AikoFace? Showing => _showing?.Face;

    /// Called at startup and after every settings change.
    public void Follow(EnvironmentSettings environments)
    {
        _talking = environments.Environments
            .Where(e => e.Persona)
            .SelectMany(e => e.ConfigDirectories.Select(SnapshotName.For).Append(e.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (TrayMood.HasFace(environments) && _activity is null)
        {
            _activity = new ActivityWatcher();
            _activity.Changed += OnActivity;
        }
        else if (!TrayMood.HasFace(environments) && _activity is not null)
        {
            _activity.Changed -= OnActivity;
            _activity.Dispose();
            _activity = null;
            _percents.Clear();
            Hide();
        }
    }

    /// Called with every new limit snapshot, named by environment.
    public void OnLimits(IEnumerable<LimitSnapshot> snapshots)
    {
        if (_activity is null)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        foreach (var snapshot in snapshots.Where(s => _talking.Contains(s.Environment)))
        {
            var percent = TrayMood.HighestPercent(snapshot, now);
            _percents.TryGetValue(snapshot.Environment, out var before);
            _percents[snapshot.Environment] = percent;

            if (TrayMood.ForLimit(before, percent) is { } face)
            {
                Show(face);
            }
        }
    }

    private void OnActivity(ActivityRecord? before, ActivityRecord after) =>
        _dispatcher.BeginInvoke(() =>
        {
            if (_talking.Contains(after.Environment) && TrayMood.ForActivity(before, after, DateTimeOffset.Now) is { } face)
            {
                Show(face);
            }
        });

    private void Show(AikoFace face)
    {
        var now = DateTimeOffset.Now;
        var next = TrayMood.Next(_showing, face, now);
        if (next == _showing)
        {
            return;
        }

        var changed = next.Face != _showing?.Face;
        _showing = next;

        // Restarted, so a new face gets its full two seconds.
        _timer.Stop();
        _timer.Start();

        if (changed)
        {
            Log.Write($"face: {face}");
            FaceChanged?.Invoke(face);
        }
    }

    private void Hide()
    {
        _timer.Stop();
        if (_showing is null)
        {
            return;
        }

        _showing = null;
        FaceChanged?.Invoke(null);
    }

    public void Dispose()
    {
        _timer.Stop();
        if (_activity is not null)
        {
            _activity.Changed -= OnActivity;
            _activity.Dispose();
        }
    }
}
