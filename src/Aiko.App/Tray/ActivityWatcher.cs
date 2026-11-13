using Aiko.Core;

namespace Aiko.App;

/// Watches the session files the bridge writes from the persona hooks (D-206). No timers: the file
/// system says when a session changed.
sealed class ActivityWatcher : IDisposable
{
    private readonly JsonFolderWatcher _watcher;
    private readonly Dictionary<string, ActivityRecord> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public ActivityWatcher()
    {
        _watcher = new JsonFolderWatcher(ThisComputer.Folders.ActivityFolder);

        // What is already there is history: it is remembered, so the next change compares with it,
        // but it raises nothing.
        foreach (var (name, text) in _watcher.AtStart)
        {
            if (ActivityRecord.FromJson(text) is { } record)
            {
                _sessions[name] = record;
            }
        }

        _watcher.Written += OnWritten;
        _watcher.Removed += OnRemoved;
        _watcher.Start();
    }

    /// Raised on a thread of the file system watcher, with what the file said before and says now.
    public event Action<ActivityRecord?, ActivityRecord>? Changed;

    public void Dispose() => _watcher.Dispose();

    private void OnWritten(string name, string text)
    {
        if (ActivityRecord.FromJson(text) is not { } after)
        {
            return;
        }

        ActivityRecord? before;
        lock (_sessions)
        {
            _sessions.TryGetValue(name, out before);
            if (before == after)
            {
                return;
            }

            _sessions[name] = after;
        }

        Changed?.Invoke(before, after);
    }

    private void OnRemoved(string name)
    {
        lock (_sessions)
        {
            _sessions.Remove(name);
        }
    }
}
