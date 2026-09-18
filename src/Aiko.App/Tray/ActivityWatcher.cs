using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// Watches the session files the bridge writes from the persona hooks (D-206). Like SnapshotWatcher,
/// no timers: the file system says when a session changed.
sealed class ActivityWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<string, ActivityRecord> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public ActivityWatcher()
    {
        var folder = ThisComputer.Folders.ActivityFolder;
        Directory.CreateDirectory(folder);

        // What is already there is history: it is remembered, so the next change compares with it,
        // but it raises nothing.
        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            if (Read(file) is { } record)
            {
                _sessions[Path.GetFileName(file)] = record;
            }
        }

        _watcher = new FileSystemWatcher(folder, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnChanged;
        _watcher.Deleted += OnDeleted;
    }

    /// Raised on a thread of the file system watcher, with what the file said before and says now.
    public event Action<ActivityRecord?, ActivityRecord>? Changed;

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (Read(e.FullPath) is not { } after)
        {
            return;
        }

        ActivityRecord? before;
        lock (_sessions)
        {
            _sessions.TryGetValue(e.Name ?? "", out before);
            if (before == after)
            {
                // One write often raises two events.
                return;
            }

            _sessions[e.Name ?? ""] = after;
        }

        Changed?.Invoke(before, after);
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        lock (_sessions)
        {
            _sessions.Remove(e.Name ?? "");
        }
    }

    /// The bridge moves a temporary file over this one, so a read can land mid-swap. A failed read
    /// is not an error: the move raises another event.
    private static ActivityRecord? Read(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return File.Exists(path) ? ActivityRecord.FromJson(File.ReadAllText(path)) : null;
            }
            catch (IOException)
            {
                Thread.Sleep(30);
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        return null;
    }

    public void Dispose()
    {
        _watcher.Changed -= OnChanged;
        _watcher.Created -= OnChanged;
        _watcher.Renamed -= OnChanged;
        _watcher.Deleted -= OnDeleted;
        _watcher.Dispose();
    }
}
