namespace Aiko.Core;

public enum ConfigVariable
{
    /// Leave CLAUDE_CONFIG_DIR as the terminal has it: Aiko knows nothing better.
    Keep,

    /// Set it to the folder of the environment.
    Set,

    /// Remove it, so Claude Code uses .claude the way it does with no variable at all. Setting it
    /// to .claude by hand is not the same: Claude Code then keeps .claude.json in another place.
    Clear,
}

/// What the shim does with one start of Claude Code.
public sealed record ShimPlan(AikoEnvironment? Environment, ConfigVariable Variable, string? ConfigDirectory, bool Explicit)
{
    public static readonly ShimPlan PassThrough = new(null, ConfigVariable.Keep, null, false);
}

/// Decides which environment a start of Claude Code belongs to.
///
/// The shim is one program under several names. Started as "claude", it follows the project
/// folder bindings. Started as a command such as "aiko-work", it runs that environment whatever
/// the folder says: an explicit command wins over a binding (D-159).
public static class ShimLaunch
{
    public const string ClaudeName = "claude";

    public static ShimPlan Decide(
        string invokedAs,
        string workingDirectory,
        EnvironmentSettings settings,
        string userProfile)
    {
        var name = Path.GetFileNameWithoutExtension(invokedAs);

        if (name.Equals(ClaudeName, StringComparison.OrdinalIgnoreCase))
        {
            var bound = ProjectBinding.EnvironmentFor(workingDirectory, settings, userProfile);
            return PlanFor(bound, userProfile, isExplicit: false);
        }

        var chosen = settings.Environments.FirstOrDefault(e => e.Command.Equals(name, StringComparison.OrdinalIgnoreCase));

        // A command left over from a renamed or removed environment. Starting Claude Code as it
        // is beats refusing to start at all.
        return chosen is null ? ShimPlan.PassThrough : PlanFor(chosen, userProfile, isExplicit: true);
    }

    private static ShimPlan PlanFor(AikoEnvironment? environment, string userProfile, bool isExplicit)
    {
        if (environment is null || environment.ConfigDirectories.Count == 0)
        {
            return ShimPlan.PassThrough;
        }

        var folder = environment.ConfigDirectories[0];
        return ClaudeConfigFolder.IsDefault(folder, userProfile)
            ? new ShimPlan(environment, ConfigVariable.Clear, null, isExplicit)
            : new ShimPlan(environment, ConfigVariable.Set, folder, isExplicit);
    }
}
