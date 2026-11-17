using System.IO;

namespace Aiko.App;

/// Watches one folder of JSON files that the bridge writes. No timers: the file system says when a
/// file changed, and in between Aiko sleeps.
///
/// A file is raised only when its text is new: one write often brings two events. When Windows loses
/// events, as it does when many files change at once and its buffer overflows, every file is read
/// again, so a number or a face never stays where it stopped.
sealed class JsonFolderWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<string, string> _texts = new(StringComparer.OrdinalIgnoreCase);

    /// Reads what is in the folder now. Watching starts with Start, after the owner listens.
    public JsonFolderWatcher(string folder)
    {
        Folder = folder;
        Directory.CreateDirectory(folder);
        foreach (var path in Directory.EnumerateFiles(folder, "*.json"))
        {
            if (ReadText(path) is { } text)
            {
                _texts[Path.GetFileName(path)] = text;
            }
        }

        _watcher = new FileSystemWatcher(folder, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnChanged;
        _watcher.Deleted += OnDeleted;
        _watcher.Error += OnError;
    }

    public string Folder { get; }

    /// The files that were there before watching started: file name and text.
    public IReadOnlyList<(string Name, string Text)> AtStart => [.. _texts.Select(pair => (pair.Key, pair.Value))];

    /// A file has a new text: its name and the text. Raised on a thread of the watcher.
    public event Action<string, string>? Written;

    /// A file is gone: its name. Raised on a thread of the watcher.
    public event Action<string>? Removed;

    public void Start() => _watcher.EnableRaisingEvents = true;

    /// The bridge moves a temporary file over this one, so a read can land mid-swap. Null for a file
    /// that is gone or still being replaced: the move raises another event.
    public static string? ReadText(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
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
        _watcher.Error -= OnError;
        _watcher.Dispose();
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => Take(e.FullPath);

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        var name = e.Name ?? "";
        lock (_texts)
        {
            if (!_texts.Remove(name))
            {
                return;
            }
        }

        Removed?.Invoke(name);
    }

    /// Events were lost. The watcher is started again in case it stopped, and every file is read.
    private void OnError(object sender, ErrorEventArgs e)
    {
        Log.Write($"folder watch: {Path.GetFileName(Folder)} lost events ({e.GetException().GetType().Name}), reading the folder again");
        try
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.EnableRaisingEvents = true;
            foreach (var path in Directory.EnumerateFiles(Folder, "*.json"))
            {
                Take(path);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            Log.Write($"folder watch: {Path.GetFileName(Folder)} could not be read again ({error.GetType().Name})");
        }
    }

    private void Take(string path)
    {
        if (ReadText(path) is not { } text)
        {
            return;
        }

        var name = Path.GetFileName(path);
        lock (_texts)
        {
            if (_texts.TryGetValue(name, out var before) && before == text)
            {
                return;
            }

            _texts[name] = text;
        }

        Written?.Invoke(name, text);
    }
}
