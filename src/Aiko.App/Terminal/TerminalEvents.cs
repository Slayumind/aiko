using System.IO;
using System.Windows.Threading;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The last hook events of the sessions of the terminal (D-352, D-360): the files the bridge
/// writes, one per session. No timers: the file system says when a session did something.
sealed class TerminalEvents : IDisposable
{
    private readonly string _folder = ThisComputer.Folders.TerminalEventsFolder;
    private readonly Dispatcher _dispatcher;
    private readonly JsonFolderWatcher _watcher;

    /// The files already there belong to sessions of an earlier run, so only what is written from
    /// now on is raised.
    public TerminalEvents(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _watcher = new JsonFolderWatcher(_folder);
        _watcher.Written += OnWritten;
        _watcher.Start();
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

    private void OnWritten(string name, string text)
    {
        var id = Path.GetFileNameWithoutExtension(name);
        if (TerminalEvent.FileName(id) is null || TerminalEvent.FromJson(text) is not { } read)
        {
            return;
        }

        _dispatcher.BeginInvoke(() => Changed?.Invoke(id, read));
    }
}
