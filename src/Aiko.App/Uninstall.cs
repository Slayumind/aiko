using System.Diagnostics;
using System.IO;
using Aiko.Core;
using Velopack.Locators;

namespace Aiko.App;

/// What Aiko does on its way out, before the program folder goes.
///
/// The promise on the first run was that removing Aiko puts everything back, and this is where
/// that promise is kept. Two ways in: the installer's hook, when the person removes Aiko from
/// Installed apps, and the settings page. Nothing here asks anything: the person already answered.
static class Uninstall
{
    /// Velopack gives the whole hook 30 seconds and then ends the process (D-205). The fast file
    /// work runs first, so a slow claude.exe can only cost the last, optional step.
    private static readonly TimeSpan PluginBudget = TimeSpan.FromSeconds(20);

    /// Whether there is an uninstaller to call. A copy unpacked from the portable zip has none,
    /// and Windows does not let a running program delete itself.
    public static bool HasUninstaller => Uninstaller() is not null;

    /// The way out through the settings page. The same cleanup the installer's hook runs, and then
    /// the uninstaller, which takes the program, the shortcuts and the entry in Installed apps.
    ///
    /// The hook runs Cleanup once more from the copy being removed. That costs nothing: with the
    /// settings already put back there is no work left to find.
    public static void RemoveFromApp()
    {
        // Both of these before Cleanup: afterwards the folder the log lives in is gone, and one
        // more line would make it again.
        var uninstaller = Uninstaller();
        Log.Write(uninstaller is null
            ? "uninstall: asked from the app, this copy has no uninstaller"
            : "uninstall: asked from the app");

        Cleanup();

        if (uninstaller is null)
        {
            // Nothing to call: show the person the folder that is left and let them delete it.
            Show(AppContext.BaseDirectory);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(uninstaller, "uninstall") { UseShellExecute = false });
        }
        catch (Exception)
        {
            Show(AppContext.BaseDirectory);
        }
    }

    /// The Update.exe of this install, or null when this copy was not installed by the installer.
    private static string? Uninstaller()
    {
        try
        {
            // The one thing that matters: an Update.exe next to this copy. A build folder and the
            // portable zip have none, and that is exactly when there is nothing to uninstall.
            var updateExe = VelopackLocator.Current.UpdateExePath;
            return updateExe is not null && File.Exists(updateExe) ? updateExe : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

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

        Remove(ThisComputer.Folders.SettingsFolder);
        Remove(ThisComputer.Folders.LocalFolder);
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

    /// Only Aiko's own folders, and only the ones UninstallPlan names. Everything else — a Claude
    /// Code folder above all — is refused here rather than trusted to the caller.
    private static void Remove(string folder)
    {
        if (!UninstallPlan.MayDelete(ThisComputer.Folders, folder))
        {
            Log.Write($"uninstall: refused to delete {folder}");
            return;
        }

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

    /// Opens a folder in Explorer, for the copy Aiko cannot remove itself.
    private static void Show(string folder)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder.TrimEnd('\\')}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception)
        {
        }
    }
}
