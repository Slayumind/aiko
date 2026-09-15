using System.Diagnostics;
using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// Runs the real claude.exe the way a person would from a fresh terminal (D-203).
///
/// Never the claude in PATH: that is Aiko's shim, and in a bound folder it overrides the config
/// folder we ask for. Never with the variables of a Claude Code session either: inside a session
/// Claude Code ignores -y and refuses to run a command source.
sealed class ClaudeCli(string claudeExe) : IClaudeCli
{
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public CliResult Run(string configDirectory, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        var start = new ProcessStartInfo(claudeExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            WorkingDirectory = Path.GetTempPath(),
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var name in start.Environment.Keys.ToList())
        {
            if (name is "CLAUDECODE" or "CLAUDE_CONFIG_DIR"
                || name.StartsWith("CLAUDE_CODE_", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("AIKO_", StringComparison.OrdinalIgnoreCase))
            {
                start.Environment.Remove(name);
            }
        }

        // Environment 1 runs without the variable, like a plain claude and the VS Code panel (D-156).
        if (!ClaudeConfigFolder.IsDefault(configDirectory, Home))
        {
            start.Environment["CLAUDE_CONFIG_DIR"] = configDirectory;
        }

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return new CliResult(-1, TimedOut: false);
            }

            process.StandardInput.Close();

            // Read both streams to the end, or a chatty command fills the pipe and never exits.
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeout))
            {
                process.Kill(entireProcessTree: true);
                return new CliResult(-1, TimedOut: true);
            }

            Task.WaitAll([output, errors], TimeSpan.FromSeconds(5));
            return new CliResult(process.ExitCode, TimedOut: false);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return new CliResult(-1, TimedOut: false);
        }
    }
}
