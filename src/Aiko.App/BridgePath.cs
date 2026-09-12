using System.IO;

namespace Aiko.App;

/// Where Aiko.Bridge.exe is. The wizard writes this path into the Claude Code settings, so it has
/// to be the path that will still work tomorrow.
static class BridgePath
{
    private const string BridgeExe = "Aiko.Bridge.exe";

    public static string? Current() => NextToTheApp() ?? InTheBuildFolder();

    /// After installing, the bridge sits beside the app in the same folder, and Velopack keeps
    /// that folder at the same place across updates.
    private static string? NextToTheApp()
    {
        var folder = Path.GetDirectoryName(Environment.ProcessPath);
        if (folder is null)
        {
            return null;
        }

        var path = Path.Combine(folder, BridgeExe);
        return SafeExists(path) ? path : null;
    }

    /// While developing, the two projects build into folders of their own. Walking up to the src
    /// folder and back down is only for that case; an installed Aiko never gets here.
    private static string? InTheBuildFolder()
    {
        var folder = Path.GetDirectoryName(Environment.ProcessPath);

        while (folder is not null)
        {
            var candidate = Path.Combine(folder, "src", "Aiko.Bridge", "bin");
            if (Directory.Exists(candidate))
            {
                return Newest(candidate);
            }
            folder = Path.GetDirectoryName(folder);
        }

        return null;
    }

    private static string? Newest(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, BridgeExe, SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool SafeExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
