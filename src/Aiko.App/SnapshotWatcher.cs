using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// Watches the folder the bridge writes into. No timers: the file system says when something
/// changed, and in between Aiko sleeps.
sealed class SnapshotWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<string, LimitSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public SnapshotWatcher()
    {
        Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aiko",
            "environments");
        Directory.CreateDirectory(Folder);

        foreach (var file in Directory.EnumerateFiles(Folder, "*.json"))
        {
            Read(file);
        }

        _watcher = new FileSystemWatcher(Folder, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnChanged;
    }

    public string Folder { get; }

    public event Action? Updated;

    public IReadOnlyCollection<LimitSnapshot> Snapshots
    {
        get
        {
            lock (_snapshots)
            {
                return _snapshots.Values.ToList();
            }
        }
    }

    /// Keyed by the file name, which is the name of the Claude Code config folder. Matching those
    /// files with the environments the user named is the core's job, not ours.
    public IReadOnlyDictionary<string, LimitSnapshot> ByFile
    {
        get
        {
            lock (_snapshots)
            {
                return new Dictionary<string, LimitSnapshot>(_snapshots, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public LimitSnapshot For(string environment)
    {
        lock (_snapshots)
        {
            return _snapshots.TryGetValue(environment, out var snapshot)
                ? snapshot
                : LimitSnapshot.NoData(environment);
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (Read(e.FullPath))
        {
            Updated?.Invoke();
        }
    }

    /// The bridge moves a temporary file over this one, so a read can land mid-swap. A failed
    /// read is not an error: the next event brings the same file again.
    private bool Read(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var snapshot = SnapshotFile.FromJson(File.ReadAllText(path), name);
                lock (_snapshots)
                {
                    _snapshots[name] = snapshot;
                }
                return true;
            }
            catch (IOException)
            {
                Thread.Sleep(30);
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
        return false;
    }

    public void Dispose()
    {
        _watcher.Changed -= OnChanged;
        _watcher.Created -= OnChanged;
        _watcher.Renamed -= OnChanged;
        _watcher.Dispose();
    }
}
