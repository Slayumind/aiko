using System.IO;
using Aiko.Core;

namespace Aiko.App;

/// Which shell Claude Code will run our status line with.
///
/// It uses Git Bash and only falls back to PowerShell when Git Bash is missing, and the two need
/// different text for the same command. Guessing wrong is quiet: the status line simply produces
/// nothing.
///
/// The looking itself lives in the core, because the bridge asks the same question when it runs
/// the status line the user already had. Two answers that could differ would put a bash command
/// through PowerShell.
static class ShellDetect
{
    public static ClaudeShell Current() => ClaudeShellLookup.ShellFor(ThisComputer.Platform, GitBashPath());

    public static string? GitBashPath() =>
        WindowsGitBash.Find(
            File.Exists,
            WindowsGitBash.Places(ThisComputer.SystemFolders, Environment.GetEnvironmentVariable("PATH") ?? string.Empty));
}
