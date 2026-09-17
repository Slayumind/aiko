using System.IO;
using Aiko.Core;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Aiko.App;

/// The folder in PATH that holds the shim and the launch commands: %LOCALAPPDATA%\Aiko\bin.
///
/// It lives outside the install, because Velopack replaces the install folder on every update and
/// PATH has to point at something that stays. The shim is copied in from the install, and each
/// command is a hard link to that copy: one file on disk, many names.
///
/// Nothing here runs until the person sets up a command or a folder binding (D-158). Removing Aiko
/// takes the folder out of PATH again.
static class CommandFolder
{
    public static readonly string Folder = ThisComputer.Folders.CommandsFolder;

    private const string EnvironmentKey = "Environment";
    private const string PathValue = "Path";

    public static bool IsSetUp => File.Exists(Path.Combine(Folder, CommandLinks.ShimFileName));

    /// The shim published with this version of the app, or null while developing.
    public static string? ShimInInstall()
    {
        var folder = Path.GetDirectoryName(Environment.ProcessPath);
        var path = folder is null ? null : Path.Combine(folder, "shim", CommandLinks.ShimFileName);
        return path is not null && File.Exists(path) ? path : null;
    }

    /// Makes the folder hold the current shim and exactly the commands of these environments.
    /// Returns false when there is no shim to copy, which only happens in a development build.
    public static bool Sync(EnvironmentSettings settings)
    {
        var source = ShimInInstall();
        if (source is null)
        {
            Log.Write("commands: no shim beside the app, nothing set up");
            return false;
        }

        try
        {
            Directory.CreateDirectory(Folder);
            ClearLeftovers();

            var shim = Path.Combine(Folder, CommandLinks.ShimFileName);
            var shimChanged = CopyIfDifferent(source, shim);

            var plan = CommandLinks.Plan(settings, Directory.EnumerateFiles(Folder));

            // A new shim means every link still points at the old file's content. Links are cheap,
            // so all of them are made again.
            var toRemove = shimChanged ? LinksIn(Folder) : plan.ToRemove;
            var toAdd = shimChanged
                ? settings.Environments.Select(e => CommandLinks.FileNameFor(e.Command)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : plan.ToAdd;

            foreach (var name in toRemove)
            {
                RemoveOrPutAside(Path.Combine(Folder, name));
            }

            foreach (var name in toAdd)
            {
                Link(shim, Path.Combine(Folder, name));
            }

            Log.Write($"commands: shim {(shimChanged ? "copied" : "current")}, {toAdd.Count} added, {toRemove.Count} removed");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"commands: could not set up the folder ({e.GetType().Name})");
            return false;
        }
    }

    /// Puts the folder first in the user PATH. New terminals and newly started IDEs see it; the
    /// ones already open keep the PATH they started with.
    public static void AddToPath() => ChangeUserPath(value => UserPathList.AddToFront(value, Folder));

    public static void RemoveFromPath() => ChangeUserPath(value => UserPathList.Remove(value, Folder));

    public static bool IsInPath() => UserPathList.Contains(ReadUserPath(), Folder);

    /// Everything Aiko put here goes: the PATH entry and the folder.
    public static void Remove()
    {
        RemoveFromPath();
        try
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A command still running keeps its file. The folder is out of PATH, so it does no harm.
        }
    }

    private static void ChangeUserPath(Func<string?, string> change)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(EnvironmentKey);
            var before = ReadUserPath(key);
            var after = change(before);
            if (after == (before ?? ""))
            {
                return;
            }

            // ExpandString keeps entries such as %USERPROFILE%\bin working after the write.
            key.SetValue(PathValue, after, RegistryValueKind.ExpandString);
            AnnounceEnvironmentChange();
            Log.Write("commands: user PATH changed");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Write($"commands: could not change the user PATH ({e.GetType().Name})");
        }
    }

    private static string? ReadUserPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(EnvironmentKey);
        return key is null ? null : ReadUserPath(key);
    }

    /// Read raw, without expanding %VARIABLES%: writing back an expanded value would quietly turn
    /// the person's own entries into fixed paths.
    private static string? ReadUserPath(RegistryKey key) =>
        key.GetValue(PathValue, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;

    /// Explorer, and every program started from it after this, reads the new PATH once told.
    private static unsafe void AnnounceEnvironmentChange()
    {
        fixed (char* name = EnvironmentKey)
        {
            PInvoke.SendMessageTimeout(
                HWND.HWND_BROADCAST,
                PInvoke.WM_SETTINGCHANGE,
                default,
                (nint)name,
                SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG,
                1000,
                out _);
        }
    }

    private static bool CopyIfDifferent(string source, string target)
    {
        var from = new FileInfo(source);
        var to = new FileInfo(target);
        if (to.Exists && to.Length == from.Length && to.LastWriteTimeUtc == from.LastWriteTimeUtc)
        {
            return false;
        }

        RemoveOrPutAside(target);
        File.Copy(source, target);
        File.SetLastWriteTimeUtc(target, from.LastWriteTimeUtc);
        return true;
    }

    private static IReadOnlyList<string> LinksIn(string folder) =>
        Directory.EnumerateFiles(folder, "*.exe")
            .Select(Path.GetFileName)
            .Where(n => n is not null && !n.Equals(CommandLinks.ShimFileName, StringComparison.OrdinalIgnoreCase))
            .Select(n => n!)
            .ToList();

    private static unsafe void Link(string shim, string link)
    {
        if (File.Exists(link))
        {
            return;
        }

        fixed (char* linkName = link)
        fixed (char* target = shim)
        {
            if (!PInvoke.CreateHardLink(new PCWSTR(linkName), new PCWSTR(target)))
            {
                // A folder on a file system without hard links still works, one full copy per command.
                File.Copy(shim, link);
            }
        }
    }

    /// A file in use cannot be deleted on Windows, but it can be renamed. Somebody may have Claude
    /// Code open through a command right now; that session keeps running, and the leftover is
    /// cleared on the next sync.
    private static void RemoveOrPutAside(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            File.Move(path, path + "." + Guid.NewGuid().ToString("N")[..8] + ".old");
        }
    }

    private static void ClearLeftovers()
    {
        foreach (var old in Directory.EnumerateFiles(Folder, "*.old"))
        {
            try
            {
                File.Delete(old);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
