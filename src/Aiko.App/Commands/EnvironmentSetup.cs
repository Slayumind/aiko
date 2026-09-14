using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// Puts a saved list of environments into effect outside Aiko: the status line and the reminder
/// hook in each Claude Code folder, the command folder in PATH, and the profile functions that
/// would hide the commands. The wizard's Finish and --apply-environments both come here, so the two
/// can never set things up differently.
static class EnvironmentSetup
{
    public static List<PatchProblem> ApplyAccess(EnvironmentSettings settings)
    {
        var problems = new List<PatchProblem>();
        if (BridgePath.Current() is not { } bridge)
        {
            problems.Add(PatchProblem.BridgeUnknown);
            return problems;
        }

        var anyBinding = settings.Environments.Any(e => e.ProjectFolders.Count > 0);
        foreach (var folder in settings.Environments.SelectMany(e => e.ConfigDirectories))
        {
            var outcome = ClaudeSettingsFile.AddBridge(folder, bridge);
            if (outcome.Problem != PatchProblem.None)
            {
                problems.Add(outcome.Problem);
            }

            // The reminder about bindings is only worth a process at session start once something is bound.
            if (anyBinding)
            {
                ClaudeSettingsFile.AddSessionHook(folder, bridge);
            }
        }

        return problems;
    }

    /// Returns the commands that now work, or null when the shim is not there to set them up.
    public static string? ApplyCommands(EnvironmentSettings settings, IEnumerable<(string Path, ProfileFunction Function)> turnOff)
    {
        if (!CommandFolder.Sync(settings))
        {
            return null;
        }

        CommandFolder.AddToPath();
        foreach (var profile in turnOff.GroupBy(f => f.Path))
        {
            PowerShellProfiles.TurnOff(profile.Key, profile.Select(f => f.Function).ToList());
        }

        return string.Join(", ", settings.Environments.Select(e => e.Command));
    }

    /// Everything, for the environments already saved: every found switcher is turned off, the way
    /// the wizard ticks them by default (D-167).
    public static bool ApplySaved()
    {
        var settings = SettingsStore.LoadEnvironments();
        if (!settings.HasEnvironments)
        {
            Log.Write("apply: no environments saved");
            return false;
        }

        var problems = ApplyAccess(settings);
        var switchers = PowerShellProfiles.FindSwitchers()
            .SelectMany(profile => profile.Functions.Select(function => (profile.Path, function)));
        var commands = ApplyCommands(settings, switchers);

        Log.Write($"apply: {settings.Environments.Count} environments, access problems {problems.Count}, commands {commands ?? "not set up"}");
        return problems.Count == 0 && commands is not null;
    }

    /// Takes back what ApplySaved put outside the Claude Code folders.
    public static void Undo()
    {
        PowerShellProfiles.TurnOnAll();
        CommandFolder.Remove();
        Log.Write("apply: commands, PATH entry and profile changes undone");
    }
}
