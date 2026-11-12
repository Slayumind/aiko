using System.IO;

namespace Aiko.App;

/// The shell of a command line session and how it starts: Git Bash when it is there, as Claude Code
/// itself prefers it (RESEARCH), otherwise PowerShell 7, otherwise Windows PowerShell. Each one starts
/// with Aiko's marks of shell integration (Assets/Shell, D-412), after the person's own profile.
sealed record ShellLaunch(string CommandLine, string Name)
{
    private static string Scripts => Path.Combine(AppContext.BaseDirectory, "Assets", "Shell");

    public static ShellLaunch Find()
    {
        if (GitBash() is { } bash)
        {
            return Bash(bash);
        }

        return FindOnPath("pwsh.exe") is { } pwsh ? PowerShell(pwsh, "pwsh") : PowerShell("powershell.exe", "powershell");
    }

    public static string? GitBash()
    {
        if (Environment.GetEnvironmentVariable("CLAUDE_CODE_GIT_BASH_PATH") is { Length: > 0 } set && File.Exists(set))
        {
            return set;
        }

        return new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "bin", "bash.exe"),
        }.FirstOrDefault(File.Exists);
    }

    /// The rc file replaces the login files, so it reads them itself before the marks. Bash takes a
    /// Windows path with forward slashes.
    public static ShellLaunch Bash(string bash) =>
        new($"\"{bash}\" --rcfile \"{Path.Combine(Scripts, "aiko.bash").Replace('\\', '/')}\" -i", "bash");

    /// The profile loads first, as always; the script then wraps its prompt.
    public static ShellLaunch PowerShell(string program, string name) =>
        new($"\"{program}\" -NoLogo -NoExit -Command \". '{Path.Combine(Scripts, "aiko.ps1").Replace("'", "''")}'\"", name);

    public static string? FindOnPath(string file) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(folder => Path.Combine(folder.Trim(), file))
            .FirstOrDefault(File.Exists);
}
