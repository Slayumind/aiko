using System.IO;
using System.Windows.Threading;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The last hook events of the sessions of the terminal (D-352, D-360): the files the bridge
/// writes, one per session. Like ActivityWatcher, no timers: the file system says when a session
/// did something.
sealed class TerminalEvents : IDisposable
{
    private readonly string _folder = ThisComputer.Folders.TerminalEventsFolder;
    private readonly Dispatcher _dispatcher;
    private readonly FileSystemWatcher _watcher;

    public TerminalEvents(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        Directory.CreateDirectory(_folder);
        _watcher = new FileSystemWatcher(_folder, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnChanged;
    }

    /// A session did something: its id and the event. Raised on the window's thread.
    public event Action<string, TerminalEvent>? Changed;

    /// The settings file with the hook of the terminal, written when it is not there or names
    /// another bridge. Empty when there is no bridge or the file cannot be written: the session
    /// then starts without the hook, and its row knows only what the title says.
    public static string HooksFile()
    {
        if (BridgePath.Current() is not { } bridge)
        {
            return "";
        }

        var path = ThisComputer.Folders.TerminalHooksFile;
        var text = TerminalHooks.SettingsJson(bridge);
        try
        {
            if (!File.Exists(path) || File.ReadAllText(path) != text)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
            }

            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    /// The session was closed: its file goes, so a session started later never sees it.
    public void Forget(string id)
    {
        if (TerminalEvent.FileName(id) is not { } file)
        {
            return;
        }

        try
        {
            File.Delete(Path.Combine(_folder, file));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The bridge writes it right now; the next start of Aiko leaves it unused.
        }
    }

    public void Dispose() => _watcher.Dispose();

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        var id = Path.GetFileNameWithoutExtension(e.Name ?? "");
        if (TerminalEvent.FileName(id) is null || Read(e.FullPath) is not { } read)
        {
            return;
        }

        _dispatcher.BeginInvoke(() => Changed?.Invoke(id, read));
    }

    /// Null for a file that is gone or still being replaced; the next event of the watcher reads it again.
    private static TerminalEvent? Read(string path)
    {
        try
        {
            return File.Exists(path) ? TerminalEvent.FromJson(File.ReadAllText(path)) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
