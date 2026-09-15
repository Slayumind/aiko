namespace Aiko.Core;

public sealed record CliResult(int ExitCode, bool TimedOut)
{
    public bool Succeeded => !TimedOut && ExitCode == 0;
}

/// Runs the real claude.exe for one account folder. The seam that keeps the core testable: the
/// app starts a process, tests answer from a script.
public interface IClaudeCli
{
    CliResult Run(string configDirectory, IReadOnlyList<string> arguments, TimeSpan timeout);
}

public static class PluginReconciler
{
    /// A command source runs the bridge while installing; a minute is far more than it needs.
    public static readonly TimeSpan StepTimeout = TimeSpan.FromMinutes(1);

    /// Runs the steps in order. Without the marketplace no install can work, so a failed
    /// marketplace step ends the run; one plugin that fails does not stop the others.
    public static IReadOnlyList<(PluginStep Step, CliResult Result)> Apply(IClaudeCli cli, string configDirectory, IReadOnlyList<PluginStep> steps)
    {
        var results = new List<(PluginStep, CliResult)>();
        foreach (var step in steps)
        {
            var result = cli.Run(configDirectory, step.Arguments, StepTimeout);
            results.Add((step, result));

            if (!result.Succeeded && step.Kind is PluginStepKind.AddMarketplace or PluginStepKind.RemoveMarketplace)
            {
                break;
            }
        }

        return results;
    }
}
