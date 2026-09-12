namespace Aiko.Core;

/// The shell Claude Code runs the status line with. It uses Git Bash, and falls back to
/// PowerShell only when Git Bash is not installed.
public enum ClaudeShell
{
    GitBash,
    PowerShell,
}

/// The one line Aiko writes into the Claude Code settings file.
///
/// The bridge takes no arguments: it reads CLAUDE_CONFIG_DIR itself and names its file after that
/// folder. So the command is only the path to the bridge, in quotes because the install path
/// holds spaces.
///
/// The exact text matters twice over: adding our line compares it against what is already there
/// to see whether the line is ours, and removing Aiko has to recognise it again.
public static class BridgeCommand
{
    public static string For(string bridgeExePath, ClaudeShell shell = ClaudeShell.GitBash)
    {
        var path = bridgeExePath?.Trim() ?? string.Empty;
        if (path.Length == 0)
        {
            // An empty command is refused by the patch, which is what we want: better no line at
            // all than a status line that runs nothing.
            return string.Empty;
        }

        // A path that is already quoted stays as it is; quoting it twice would break the command.
        var quoted = path.StartsWith('"') && path.EndsWith('"') ? path : $"\"{path}\"";

        // In PowerShell a quoted path on its own is just a string and nothing runs. The call
        // operator makes it a command. In bash the same "&" would break the line, so the two
        // shells get different text (spike 2026-09-11 left this open).
        return shell == ClaudeShell.PowerShell ? $"& {quoted}" : quoted;
    }
}
