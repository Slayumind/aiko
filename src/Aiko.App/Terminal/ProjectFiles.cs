using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

namespace Aiko.App;

/// The files of a session's folder for "@", as paths relative to it with "/".
///
/// A git folder answers with git ls-files: the tracked files and the new ones it does not ignore.
/// Any other folder is walked, without the folders nobody mentions (.git, node_modules, bin, obj),
/// up to MostFiles. The list is made per folder on a thread of its own; until it is ready, "@"
/// shows nothing. A list older than FreshFor is shown once more while a new one is made, so files
/// made during the session come into "@" too.
static class ProjectFiles
{
    private const int MostFiles = 5000;
    private static readonly TimeSpan GitWait = TimeSpan.FromSeconds(3);
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase) { ".git", "node_modules", "bin", "obj", ".vs", ".idea" };

    private static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(30);

    private sealed record Listing(Lazy<Task<IReadOnlyList<string>>> Files, DateTime Made);

    private static readonly ConcurrentDictionary<string, Listing> Lists = new(StringComparer.OrdinalIgnoreCase);

    private static Listing Start(string folder) =>
        new(new Lazy<Task<IReadOnlyList<string>>>(() => Task.Run(() => Read(folder))), DateTime.UtcNow);

    /// The list if it is ready, or null; ready is called on the window's thread once it is.
    public static IReadOnlyList<string>? Get(string folder, Action ready)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var listing = Lists.GetOrAdd(folder, Start);
        var list = listing.Files.Value;
        if (list.IsCompletedSuccessfully)
        {
            if (DateTime.UtcNow - listing.Made > FreshFor)
            {
                // The next "@" gets the new list; this one shows what is known.
                var fresh = Start(folder);
                if (Lists.TryUpdate(folder, fresh, listing))
                {
                    _ = fresh.Files.Value;
                }
            }

            return list.Result;
        }

        var window = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        list.ContinueWith(_ => window.BeginInvoke(ready), TaskScheduler.Default);
        return null;
    }

    private static IReadOnlyList<string> Read(string folder) => FromGit(folder) ?? Walk(folder);

    private static List<string>? FromGit(string folder)
    {
        if (!Directory.Exists(Path.Combine(folder, ".git")) && !File.Exists(Path.Combine(folder, ".git")))
        {
            return null;
        }

        try
        {
            // Names as they are: by default git writes names outside ASCII as quoted octal escapes.
            var start = new ProcessStartInfo("git", ["-c", "core.quotePath=false", "-C", folder, "ls-files", "--cached", "--others", "--exclude-standard"])
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };
            using var git = Process.Start(start);
            if (git is null)
            {
                return null;
            }

            _ = git.StandardError.ReadToEndAsync();
            var files = new List<string>();
            // Read to the end even past the limit: a full pipe would hold git until it is killed,
            // and the list would fall back to the walk that knows nothing of .gitignore.
            while (git.StandardOutput.ReadLine() is { } line)
            {
                if (line.Length > 0 && files.Count < MostFiles)
                {
                    files.Add(line);
                }
            }

            if (!git.WaitForExit(GitWait))
            {
                git.Kill();
            }

            return git.HasExited && git.ExitCode == 0 ? files : null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return null;
        }
    }

    private static List<string> Walk(string folder)
    {
        var files = new List<string>();
        var waiting = new Queue<string>([folder]);
        while (waiting.Count > 0 && files.Count < MostFiles)
        {
            var here = waiting.Dequeue();
            try
            {
                foreach (var file in Directory.EnumerateFiles(here))
                {
                    files.Add(Path.GetRelativePath(folder, file).Replace('\\', '/'));
                    if (files.Count >= MostFiles)
                    {
                        break;
                    }
                }

                foreach (var inner in Directory.EnumerateDirectories(here))
                {
                    if (!Skipped.Contains(Path.GetFileName(inner)))
                    {
                        waiting.Enqueue(inner);
                    }
                }
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException)
            {
            }
        }

        return files;
    }
}
