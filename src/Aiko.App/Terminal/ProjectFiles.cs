using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

namespace Aiko.App;

/// The files of a session's folder for "@", as paths relative to it with "/".
///
/// A git folder answers with git ls-files: the tracked files and the new ones it does not ignore.
/// Any other folder is walked, without the folders nobody mentions (.git, node_modules, bin, obj),
/// up to MostFiles. The list is made once per folder, on a thread of its own, and kept while the
/// window lives; until it is ready, "@" shows nothing.
static class ProjectFiles
{
    private const int MostFiles = 5000;
    private static readonly TimeSpan GitWait = TimeSpan.FromSeconds(3);
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase) { ".git", "node_modules", "bin", "obj", ".vs", ".idea" };

    private static readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<string>>>> Lists = new(StringComparer.OrdinalIgnoreCase);

    /// The list if it is ready, or null; ready is called on the window's thread once it is.
    public static IReadOnlyList<string>? Get(string folder, Action ready)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var list = Lists.GetOrAdd(folder, key => new Lazy<Task<IReadOnlyList<string>>>(() => Task.Run(() => Read(key)))).Value;
        if (list.IsCompletedSuccessfully)
        {
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
            var start = new ProcessStartInfo("git", ["-C", folder, "ls-files", "--cached", "--others", "--exclude-standard"])
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
            while (git.StandardOutput.ReadLine() is { } line && files.Count < MostFiles)
            {
                if (line.Length > 0)
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
