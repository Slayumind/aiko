namespace Aiko.Core;

/// What one change in the settings window does to the environments (D-178, D-179, D-180).
///
/// Every change is applied at once and saved, so each one has to leave a list that makes sense on
/// its own: the ring and the default follow a renamed environment, a folder is bound to one
/// environment at most, and the environment in .claude cannot be removed.
public static class EnvironmentEdits
{
    public const int MaxNameLength = 40;

    public static NameProblem CheckName(EnvironmentSettings settings, string current, string wanted)
    {
        var name = wanted.Trim();
        if (name.Length == 0)
        {
            return NameProblem.Empty;
        }

        if (name.Length > MaxNameLength)
        {
            return NameProblem.TooLong;
        }

        return settings.Environments.Any(e => e.Name != current && e.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ? NameProblem.Taken
            : NameProblem.None;
    }

    /// Once the commands are installed, terminals and scripts know them by name, so a rename keeps
    /// the command and only suggests the new one (D-180). Before that the command follows the name.
    public static Renamed Rename(EnvironmentSettings settings, string current, string wanted, bool commandsInstalled)
    {
        var name = wanted.Trim();
        if (Find(settings, current) is not { } environment
            || name == current
            || CheckName(settings, current, name) != NameProblem.None)
        {
            return new Renamed(settings, null);
        }

        var kept = environment.Command;
        var byName = LaunchCommand.FromEnvironmentName(name);
        var renamed = environment with
        {
            Name = name,
            CustomCommand = commandsInstalled ? CustomOrNull(name, kept) : environment.CustomCommand,
        };

        var updated = settings with
        {
            Environments = settings.Environments.Select(e => e == environment ? renamed : e).ToList(),
            RingEnvironment = settings.RingEnvironment == current ? name : settings.RingEnvironment,
            DefaultEnvironment = settings.DefaultEnvironment == current ? name : settings.DefaultEnvironment,
        };

        var suggestion = commandsInstalled && byName != kept && CheckCommand(updated, name, byName) == CommandProblem.None
            ? byName
            : null;

        return new Renamed(updated, suggestion);
    }

    /// The command an environment brings into the checklist as the person's own. Once commands are
    /// installed the one in use is kept, the same rule as a rename in settings (D-180), so going
    /// through the checklist again cannot quietly turn aiko-work into aiko-work-kodland.
    public static string? StartingCommand(AikoEnvironment? existing, bool commandsInstalled) =>
        existing is null ? null
        : existing.CustomCommand ?? (commandsInstalled ? existing.Command : null);

    public static CommandProblem CheckCommand(EnvironmentSettings settings, string environment, string command) =>
        LaunchCommand.Check(command, settings.Environments.Where(e => e.Name != environment).Select(e => e.Command));

    public static EnvironmentSettings SetCommand(EnvironmentSettings settings, string environment, string command)
    {
        var wanted = command.Trim();
        if (CheckCommand(settings, environment, wanted) != CommandProblem.None)
        {
            return settings;
        }

        return Change(settings, environment, e => e with { CustomCommand = CustomOrNull(e.Name, wanted) });
    }

    public static EnvironmentSettings SetDirectMode(EnvironmentSettings settings, string environment, bool on) =>
        Change(settings, environment, e => e with { DirectMode = on });

    public static EnvironmentSettings SetPersona(EnvironmentSettings settings, string environment, bool on) =>
        Change(settings, environment, e => e with { Persona = on });

    /// The skills need the persona, like the memory: without her the choice does not change.
    public static EnvironmentSettings SetSkills(EnvironmentSettings settings, string environment, bool on) =>
        Change(settings, environment, e => e.Persona ? e with { Skills = on } : e);

    /// Before the skills became a choice of each environment, one switch in persona.json turned
    /// them off everywhere. A person who had it off must not get the skills back by an update, so
    /// "off" is carried into every environment once and the old switch is set back to on, which
    /// makes a second call change nothing.
    public static SkillsCarried CarryOldSkillsSwitch(EnvironmentSettings settings, PersonaSettings persona) =>
        persona.SkillsOn
            ? new SkillsCarried(settings, persona, Changed: false)
            : new SkillsCarried(
                settings with { Environments = settings.Environments.Select(e => e with { Skills = false }).ToList() },
                persona with { SkillsOn = true },
                Changed: true);

    /// The memory needs the persona, so without her the choice does not change: the switch is
    /// locked in the window, and a call that got past it must not store a wish that is not shown.
    public static EnvironmentSettings SetMemory(EnvironmentSettings settings, string environment, bool on) =>
        Change(settings, environment, e => e.Persona ? e with { Memory = on } : e);

    public static EnvironmentSettings SetDefault(EnvironmentSettings settings, string environment) =>
        Find(settings, environment) is null ? settings : settings with { DefaultEnvironment = environment };

    /// Every bound folder with its environment, sorted by folder. The order does not depend on the
    /// environment, so a row stays where it is when its environment changes.
    public static IReadOnlyList<Binding> Bindings(EnvironmentSettings settings) =>
        settings.Environments
            .SelectMany(e => e.ProjectFolders.Select(folder => new Binding(folder, e.Name)))
            .OrderBy(b => b.Folder, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// Binds the folder to this environment and takes it away from any other one.
    public static EnvironmentSettings Bind(EnvironmentSettings settings, string folder, string environment)
    {
        if (Find(settings, environment) is null || string.IsNullOrWhiteSpace(folder))
        {
            return settings;
        }

        return settings with
        {
            Environments = settings.Environments
                .Select(e =>
                {
                    var others = e.ProjectFolders.Where(f => !RealClaude.SameFolder(f, folder)).ToList();
                    return e with { ProjectFolders = e.Name == environment ? [.. others, folder] : others };
                })
                .ToList(),
        };
    }

    public static EnvironmentSettings Unbind(EnvironmentSettings settings, string folder) =>
        settings with
        {
            Environments = settings.Environments
                .Select(e => e with { ProjectFolders = e.ProjectFolders.Where(f => !RealClaude.SameFolder(f, folder)).ToList() })
                .ToList(),
        };

    /// A new folder goes to the environment that is not the default: binding a folder to the
    /// environment it already runs in would change nothing.
    public static AikoEnvironment? ForNewBinding(EnvironmentSettings settings, string userProfile)
    {
        var fallback = settings.Default(userProfile);
        return settings.Environments.FirstOrDefault(e => e.Name != fallback?.Name) ?? fallback;
    }

    /// The .claude folder belongs to VS Code and Claude Desktop too, so Aiko never lets it go.
    public static bool CanRemove(EnvironmentSettings settings, string environment, string userProfile) =>
        Find(settings, environment) is { } found && found != settings.First(userProfile);

    public static EnvironmentSettings Remove(EnvironmentSettings settings, string environment, string userProfile)
    {
        if (!CanRemove(settings, environment, userProfile))
        {
            return settings;
        }

        return settings with
        {
            Environments = settings.Environments.Where(e => e.Name != environment).ToList(),
            RingEnvironment = settings.RingEnvironment == environment ? null : settings.RingEnvironment,
            DefaultEnvironment = settings.DefaultEnvironment == environment ? null : settings.DefaultEnvironment,
        };
    }

    private static AikoEnvironment? Find(EnvironmentSettings settings, string name) =>
        settings.Environments.FirstOrDefault(e => e.Name == name);

    private static EnvironmentSettings Change(EnvironmentSettings settings, string environment, Func<AikoEnvironment, AikoEnvironment> change) =>
        Find(settings, environment) is null
            ? settings
            : settings with { Environments = settings.Environments.Select(e => e.Name == environment ? change(e) : e).ToList() };

    /// A command equal to the one the name makes is stored as none, so the file stays as short as it was.
    private static string? CustomOrNull(string name, string command) =>
        command == LaunchCommand.FromEnvironmentName(name) ? null : command;
}

public sealed record Renamed(EnvironmentSettings Settings, string? SuggestedCommand);

/// Both files after the old skills switch was carried over. Changed says whether to save them.
public sealed record SkillsCarried(EnvironmentSettings Settings, PersonaSettings Persona, bool Changed);

public sealed record Binding(string Folder, string Environment);

public enum NameProblem
{
    None,
    Empty,
    TooLong,
    Taken,
}
