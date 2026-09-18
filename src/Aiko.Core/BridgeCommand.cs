namespace Aiko.Core;

/// The one line Aiko writes into the Claude Code settings file.
///
/// The bridge takes no arguments: it reads CLAUDE_CONFIG_DIR itself and names its file after that
/// folder. So the command is only the path to the bridge, in quotes because the install path
/// holds spaces.
///
/// Our line has to be recognised again later, and not only when it is character for character the
/// same. The install path changes on a reinstall, and the shell changes the moment Git appears on
/// a machine that had none. A line matched by exact text stops being ours after either, gets put
/// aside as if the user had written it, and the bridge is then asked to run a path that no longer
/// exists on every model answer. So the line is recognised by the program it points at.
public static class BridgeCommand
{
    /// The bridge program without the suffix the system adds to program files.
    public const string ProgramName = "Aiko.Bridge";

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

    /// The session start hook: the same program with one argument.
    public static string HookFor(string bridgeExePath, ClaudeShell shell = ClaudeShell.GitBash)
    {
        var command = For(bridgeExePath, shell);
        return command.Length == 0 ? command : command + " " + SessionReminder.Argument;
    }

    /// Whether this status line command runs our bridge, whatever path and shell it was written
    /// for. Only the program name is compared: everything else about the line is allowed to change.
    public static bool IsAiko(PlatformConventions platform, string? command)
    {
        var path = ExecutablePathIn(command);
        if (path is null)
        {
            return false;
        }

        return string.Equals(
            Path.GetFileName(path), platform.ExecutableName(ProgramName), StringComparison.OrdinalIgnoreCase);
    }

    /// The program a status line command runs, with the PowerShell call operator and the quotes
    /// taken off. Null when the command is empty or names no program.
    private static string? ExecutablePathIn(string? command)
    {
        var text = command?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.StartsWith("& ", StringComparison.Ordinal))
        {
            text = text[2..].TrimStart();
        }

        if (text.StartsWith('"'))
        {
            var closing = text.IndexOf('"', 1);
            return closing > 1 ? text[1..closing] : null;
        }

        // An unquoted command ends at the first space. Our own line is always quoted, so this is
        // only here to read a line somebody wrote by hand.
        var space = text.IndexOf(' ');
        var head = space < 0 ? text : text[..space];
        return head.Length == 0 ? null : head;
    }
}
