using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// Brings every Claude Code folder in line with the persona switches (D-201, D-234).
///
/// What a folder should have is decided in Aiko.Core.PluginPlan; this class reads the files,
/// writes the marketplace and runs claude.exe. All of it happens on one background queue, one
/// folder after another: two claude processes writing the same settings.json would lose a change.
static class PluginSync
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static readonly object Gate = new();

    private static Task Pending = Task.CompletedTask;

    /// Checks every folder and changes only what differs. Reading three small files per folder is
    /// all it costs when nothing is to be done, so it is safe to call at startup.
    public static void Request(string why) => Enqueue(() => Reconcile(why, updatePersona: false));

    /// The persona text changed, for example the temperament: installed persona plugins take the
    /// new text for the next session.
    public static void PersonaChanged() => Enqueue(() => Reconcile("persona changed", updatePersona: true));

    /// For an environment that is going: its keys already left settings.json, and this takes out
    /// what Claude Code keeps in its own plugin files.
    public static void Remove(IReadOnlyCollection<string> folders, string why) =>
        Enqueue(() => RemoveFrom(folders, why, DateTimeOffset.MaxValue));

    /// For the uninstaller, which cannot wait long: stops at the deadline, whatever is left.
    public static void RemoveNow(IReadOnlyCollection<string> folders, DateTimeOffset deadline) =>
        RemoveFrom(folders, "uninstall", deadline);

    private static void Enqueue(Action work)
    {
        lock (Gate)
        {
            Pending = Pending.ContinueWith(_ =>
            {
                try
                {
                    work();
                }
                catch (Exception e)
                {
                    Log.Write($"plugins: sync failed with {e.GetType().Name}");
                }
            }, TaskScheduler.Default);
        }
    }

    private static void Reconcile(string why, bool updatePersona)
    {
        var environments = SettingsStore.LoadEnvironments();
        var persona = SettingsStore.LoadPersona();

        var folders = environments.Environments
            .SelectMany(e => e.ConfigDirectories.Select(folder => (Folder: folder, Desired: PluginPlan.Desired(e, persona, SkillShelf.Skills))))
            .Select(p => (p.Folder, p.Desired, State: ReadState(p.Folder)))
            .ToList();

        // With the persona off everywhere and nothing of ours installed, Aiko writes nothing at all.
        if (folders.All(f => f.Desired.Count == 0 && f.State.Installed.Count == 0 && f.State.Enabled.Count == 0))
        {
            return;
        }

        // A new command in the marketplace, for example after moving from a build folder to the
        // installed app, is not run again until it is accepted: an update with -y accepts it.
        if (!TryWriteMarketplace(persona, out var marketplaceChanged, out var skillsChanged))
        {
            return;
        }

        updatePersona |= marketplaceChanged;

        var plans = folders
            .Select(p => (p.Folder, p.Desired, Steps: StepsFor(p.Desired, p.State, updatePersona, skillsChanged)))
            .Where(p => p.Steps.Count > 0)
            .ToList();

        if (plans.Count == 0)
        {
            return;
        }

        if (ClaudeLauncher.FindClaude() is not { } claude)
        {
            Log.Write("plugins: claude.exe not found, nothing changed");
            return;
        }

        var cli = new ClaudeCli(claude);
        foreach (var (folder, _, steps) in plans)
        {
            LogResults(why, folder, PluginReconciler.Apply(cli, folder, steps));
        }
    }

    private static void RemoveFrom(IReadOnlyCollection<string> folders, string why, DateTimeOffset deadline)
    {
        var withPlugins = folders
            .Where(Directory.Exists)
            .Select(folder => (Folder: folder, Steps: PluginPlan.Removal(ReadState(folder))))
            .Where(p => p.Steps.Count > 0)
            .ToList();

        if (withPlugins.Count == 0)
        {
            return;
        }

        if (ClaudeLauncher.FindClaude() is not { } claude)
        {
            Log.Write($"plugins: {why}: claude.exe not found, the plugins stay turned off");
            return;
        }

        var cli = new ClaudeCli(claude);
        foreach (var (folder, steps) in withPlugins)
        {
            var results = PluginReconciler.Apply(cli, folder, steps, TimeProvider.System, deadline);

            // The command writes an empty enabledPlugins back into the file Aiko just cleaned.
            ClaudeSettingsFile.TidyAfterPluginRemoval(folder);
            LogResults(why, folder, results);
        }
    }

    private static void LogResults(string why, string folder, IReadOnlyList<(PluginStep Step, CliResult Result)> results)
    {
        var failed = results.Where(r => !r.Result.Succeeded).ToList();
        Log.Write($"plugins: {why}: {Path.GetFileName(folder)}: {results.Count} steps, {failed.Count} failed"
            + (failed.Count > 0 ? " (" + string.Join(", ", failed.Select(r => $"{r.Step.Kind} exit {r.Result.ExitCode}{(r.Result.TimedOut ? " timeout" : "")}")) + ")" : ""));
    }

    private static IReadOnlyList<PluginStep> StepsFor(IReadOnlySet<string> desired, PluginState state, bool updatePersona, bool skillsChanged)
    {
        var steps = PluginPlan.Steps(desired, state, AikoMarketplace.Folder(LocalAppData)).ToList();
        var persona = AikoMarketplace.PluginId(PersonaPlugin.Name);
        if (updatePersona && desired.Contains(persona) && state.Installed.Contains(persona))
        {
            steps.Add(new PluginStep(PluginStepKind.Update, persona));
        }

        // The skills plugin got other files, from a new Aiko or a switched skill: installed copies
        // take the new version.
        var skills = AikoMarketplace.PluginId(SkillCatalog.PluginName);
        if (skillsChanged && desired.Contains(skills) && state.Installed.Contains(skills))
        {
            steps.Add(new PluginStep(PluginStepKind.Update, skills));
        }

        return steps;
    }

    private static PluginState ReadState(string folder) =>
        PluginState.Read(
            ReadIfThere(Path.Combine(folder, "settings.json")),
            ReadIfThere(Path.Combine(folder, "plugins", "installed_plugins.json")),
            ReadIfThere(Path.Combine(folder, "plugins", "known_marketplaces.json")));

    /// Written only when the text differs, so the marketplace folder is not touched for nothing.
    private static bool TryWriteMarketplace(PersonaSettings persona, out bool changed, out bool skillsChanged)
    {
        changed = false;
        skillsChanged = false;
        if (BridgePath.Current() is not { } bridge
            || AikoMarketplace.PersonaCommand(bridge, LocalAppData) is not { } command)
        {
            Log.Write("plugins: no command Claude Code would accept for the bridge path, nothing changed");
            return false;
        }

        try
        {
            skillsChanged = SkillShelf.CopyTo(AikoMarketplace.Folder(LocalAppData), persona);

            var path = AikoMarketplace.FilePath(LocalAppData);
            var json = AikoMarketplace.Json(command, SkillShelf.Shipped);
            var before = ReadIfThere(path);
            if (before != json)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path + ".tmp", json);
                File.Move(path + ".tmp", path, overwrite: true);
                changed = before is not null;
            }

            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"plugins: marketplace could not be written ({e.GetType().Name})");
            return false;
        }
    }

    private static string? ReadIfThere(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
