using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// The limits the bridge writes, one file per Claude Code config folder.
sealed class SnapshotWatcher : IDisposable
{
    private readonly JsonFolderWatcher _watcher;
    private readonly Dictionary<string, LimitSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public SnapshotWatcher()
    {
        Folder = ThisComputer.Folders.SnapshotsFolder;
        Directory.CreateDirectory(Folder);
        RemoveLeftovers();

        _watcher = new JsonFolderWatcher(Folder);
        foreach (var (name, text) in _watcher.AtStart)
        {
            Keep(name, text);
        }

        _watcher.Written += (name, text) =>
        {
            Keep(name, text);
            Updated?.Invoke();
        };
        _watcher.Start();
    }

    public string Folder { get; }

    public event Action? Updated;

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

    /// Temporary files of bridges that stopped in the middle of a write (AIKO-267).
    private void RemoveLeftovers()
    {
        var files = new DirectoryInfo(Folder).GetFiles("*.tmp")
            .Select(file => (file.Name, (DateTimeOffset)file.LastWriteTimeUtc));
        foreach (var stale in SnapshotLeftovers.Stale(files, DateTimeOffset.UtcNow))
        {
            try
            {
                File.Delete(Path.Combine(Folder, stale));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Another run holds it; the next start tries again.
            }
        }
    }

    private void Keep(string file, string text)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var snapshot = SnapshotFile.FromJson(text, name);
        lock (_snapshots)
        {
            _snapshots[name] = snapshot;
        }
    }

    public void Dispose() => _watcher.Dispose();
}
