using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// What Aiko puts right once at every start, before or after the shell is on screen. None of it
/// draws anything: it repairs what an update or a reinstall left stale.
static class StartupChores
{
    /// Our own line goes stale on its own: reinstalling moves the bridge, and installing Git
    /// changes the shell the line has to be written for. A stale line runs nothing and says
    /// nothing about it, so it is put right at startup.
    ///
    /// Only lines that are already ours are touched. Somebody who answered "not now" in the wizard
    /// keeps that answer, and a settings file with no line of ours in it is not ours to write to.
    public static void RepairOurStatusLines()
    {
        if (BridgePath.Current() is not { } bridge)
        {
            return;
        }

        foreach (var environment in SettingsStore.LoadEnvironments().Environments)
        {
            foreach (var folder in environment.ConfigDirectories)
            {
                if (ClaudeSettingsFile.RepairBridge(folder, bridge).Changed)
                {
                    Log.Write($"our status line was stale in {Path.GetFileName(folder)} and was put right");
                }
            }
        }
    }

    /// What an update of Aiko brings with it and Claude Code does not pick up by itself.
    public static void RefreshPluginsAndCommands()
    {
        // An update can bring a new persona text or move the marketplace. When the persona is
        // off everywhere this reads a few files and does nothing else.
        PluginSync.Request("startup");

        // An update brings a new shim. The copy in PATH is refreshed, and only when the person
        // set commands up: the folder never appears on its own.
        if (CommandFolder.IsSetUp)
        {
            CommandFolder.Sync(SettingsStore.LoadEnvironments());
        }
    }

    /// A session that never sent SessionEnd leaves its file behind (D-206). A day later it goes.
    public static void ClearStaleActivity()
    {
        var folder = ThisComputer.Folders.ActivityFolder;
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in new DirectoryInfo(folder).EnumerateFiles())
        {
            if (ActivityRecord.IsStale(file.LastWriteTimeUtc, DateTimeOffset.UtcNow))
            {
                try
                {
                    file.Delete();
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
