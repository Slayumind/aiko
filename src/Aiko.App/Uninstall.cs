using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// What Aiko does on its way out, before the installer removes the program folder.
///
/// The promise on the first run was that removing Aiko puts everything back, and this is where
/// that promise is kept. Nothing here asks anything: the user already answered by uninstalling.
static class Uninstall
{
    /// Velopack gives the whole hook 30 seconds and then ends the process (D-205). The fast file
    /// work runs first, so a slow claude.exe can only cost the last, optional step.
    private static readonly TimeSpan PluginBudget = TimeSpan.FromSeconds(20);

    public static void Cleanup()
    {
        var deadline = DateTimeOffset.UtcNow + PluginBudget;

        var folders = ClaudeFoldersAikoMayHaveTouched();
        foreach (var folder in folders)
        {
            ClaudeSettingsFile.RemoveAiko(folder);
        }

        Startup.Set(false);

        // Before the folders go: the commands folder is one of them, and PATH must not keep
        // pointing at it. Profile functions come back on, because the commands that replaced them
        // are about to disappear.
        CommandFolder.RemoveFromPath();
        PowerShellProfiles.TurnOnAll();

        // Without their keys in settings.json the plugins already do not load. This also removes
        // Claude Code's own record of them, as far as the time allows.
        PluginSync.RemoveNow(folders, deadline);

        Remove(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aiko"));
        Remove(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aiko"));
    }

    /// Every folder Aiko knows about, and then every Claude Code folder on the machine.
    ///
    /// The settings may be gone or may never have listed a folder the user added by hand, and a
    /// status line pointing at a program that no longer exists would break their prompt.
    private static List<string> ClaudeFoldersAikoMayHaveTouched()
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

        return [.. folders];
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
