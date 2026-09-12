namespace Aiko.Core;

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
    public static string For(string bridgeExePath)
    {
        var path = bridgeExePath?.Trim() ?? string.Empty;
        if (path.Length == 0)
        {
            // An empty command is refused by the patch, which is what we want: better no line at
            // all than a status line that runs nothing.
            return string.Empty;
        }

        // A path that is already quoted stays as it is; quoting it twice would break the command.
        return path.StartsWith('"') && path.EndsWith('"') ? path : $"\"{path}\"";
    }
}
