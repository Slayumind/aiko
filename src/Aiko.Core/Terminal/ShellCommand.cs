namespace Aiko.Core.Terminal;

/// What "!" at the start of the field means: a command for the shell, run by Aiko in the folder of
/// the session (D-343). Claude Code does not understand "!" through its stream, so Aiko runs the
/// command itself and sends Claude what it printed.
public static class ShellCommand
{
    /// The most of an output Claude gets, as Claude Code cuts the output of a command too.
    public const int MostChars = 30000;

    private const string InputTag = "bash-input";
    private const string OutputTag = "bash-stdout";
    private const string ErrorTag = "bash-stderr";

    /// The command typed after "!", or null when the text is not one.
    public static string? Of(string text)
    {
        var trimmed = text.TrimStart();
        if (!trimmed.StartsWith('!'))
        {
            return null;
        }

        var command = trimmed[1..].Trim();
        return command.Length > 0 ? command : null;
    }

    /// The message Claude gets. It has the tags Claude Code itself writes for a command run with
    /// "!", so Claude reads it the same way, and the file of the conversation reads back into the
    /// same feed. A command that failed says its exit code in the place of the errors.
    public static string Message(string command, string output, int exitCode)
    {
        var shown = output.Length > MostChars ? output[..MostChars] + "\n…" : output;
        var failed = exitCode == 0 ? "" : $"Exit code {exitCode}";
        return $"<{InputTag}>{command}</{InputTag}>\n<{OutputTag}>{shown.TrimEnd()}</{OutputTag}><{ErrorTag}>{failed}</{ErrorTag}>";
    }

    /// A message of a person in the file of a conversation that is a command of "!": its command,
    /// and its output when the same message has it. Claude Code writes the command and its output
    /// as two messages; Aiko writes them as one.
    public static (string Command, string? Output)? Read(string text)
    {
        if (Tagged(text, InputTag) is not { } command)
        {
            return null;
        }

        return (command, Output(text));
    }

    /// The output alone: the second message Claude Code writes for a command of "!".
    public static string? Output(string text)
    {
        var output = Tagged(text, OutputTag);
        var errors = Tagged(text, ErrorTag);
        if (output is null && errors is null)
        {
            return null;
        }

        return string.Join("\n", new[] { output, errors }.Where(part => part is { Length: > 0 }));
    }

    private static string? Tagged(string text, string tag)
    {
        var open = $"<{tag}>";
        var start = text.IndexOf(open, StringComparison.Ordinal);
        var end = text.IndexOf($"</{tag}>", StringComparison.Ordinal);
        return start >= 0 && end >= start + open.Length
            ? text[(start + open.Length)..end].Trim('\r', '\n')
            : null;
    }
}
