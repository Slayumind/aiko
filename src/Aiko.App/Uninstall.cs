using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// What Aiko does on its way out, before the installer removes the program folder.
///
/// The promise on the first run was that removing Aiko puts everything back, and this is where
/// that promise is kept. Nothing here asks anything: the user already answered by uninstalling.
static class Uninstall
{
    public static void Cleanup()
    {
        PutTheStatusLineBack();
        Startup.Set(false);

        Remove(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aiko"));
        Remove(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aiko"));
    }

    /// Every folder Aiko knows about, and then every Claude Code folder on the machine.
    ///
    /// The settings may be gone or may never have listed a folder the user added by hand, and a
    /// status line pointing at a program that no longer exists would break their prompt.
    private static void PutTheStatusLineBack()
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var environment in SettingsStore.LoadEnvironments().Environments)
        {
            foreach (var directory in environment.ConfigDirectories)
            {
                folders.Add(directory);
            }
        }

        foreach (var folder in ClaudeFolders.Find())
        {
            folders.Add(folder.FullPath);
        }

        foreach (var folder in folders)
        {
            ClaudeSettingsFile.RemoveBridge(folder);
        }
    }

    /// Only Aiko's own folders, and only ever these two.
    private static void Remove(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
