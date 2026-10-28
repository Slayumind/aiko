using System.Text;

namespace Aiko.Core.Terminal;

/// Finds the title a program sets for its terminal, OSC 0 or OSC 2, in the output of the terminal
/// (D-360). The control of Windows Terminal does not tell its host the title, so Aiko reads it on
/// the way. Output comes in pieces, and a title may be cut between two of them.
public sealed class TitleReader
{
    private const char Escape = '\u001b';
    private const char Bell = '\u0007';

    /// A title longer than this is not a title. Nothing past it is kept in memory.
    public const int MaxLength = 1024;

    private readonly StringBuilder _command = new();
    private State _state = State.Text;

    private enum State
    {
        Text,

        /// An escape came; a "]" starts a command of the operating system.
        AfterEscape,

        /// Inside the command, until a bell or an escape and a backslash.
        Command,

        /// An escape came inside the command.
        CommandEscape,
    }

    /// The last title that ended in this piece of output, or null when none did.
    public string? Read(string output)
    {
        string? title = null;
        foreach (var c in output)
        {
            switch (_state)
            {
                case State.Text:
                    if (c == Escape)
                    {
                        _state = State.AfterEscape;
                    }
                    break;

                case State.AfterEscape:
                    _state = c == ']' ? State.Command : c == Escape ? State.AfterEscape : State.Text;
                    _command.Clear();
                    break;

                case State.Command:
                    if (c == Bell)
                    {
                        title = Finished() ?? title;
                    }
                    else if (c == Escape)
                    {
                        _state = State.CommandEscape;
                    }
                    else if (_command.Length <= MaxLength + 2)
                    {
                        // Past the limit nothing more is kept, and the command is no title.
                        _command.Append(c);
                    }
                    break;

                case State.CommandEscape:
                    if (c == '\\')
                    {
                        title = Finished() ?? title;
                    }
                    else
                    {
                        // A new escape sequence cut the command short.
                        _command.Clear();
                        _state = c == ']' ? State.Command : State.Text;
                    }
                    break;
            }
        }

        return title;
    }

    private string? Finished()
    {
        _state = State.Text;
        var command = _command.ToString();
        _command.Clear();
        return command.Length <= MaxLength + 2 && (command.StartsWith("0;", StringComparison.Ordinal) || command.StartsWith("2;", StringComparison.Ordinal))
            ? command[2..]
            : null;
    }
}

/// Whether output of a terminal draws something a person can see. A program starts with sequences
/// that only clear the screen, move the cursor and set the title; until a letter comes the terminal
/// shows a loader instead (D-368).
public static class TerminalScreen
{
    public static bool HasText(string output)
    {
        for (var at = 0; at < output.Length; at++)
        {
            var c = output[at];
            if (c == '\u001b' && at + 1 < output.Length)
            {
                at = SequenceEnd(output, at);
                continue;
            }

            if (!char.IsWhiteSpace(c) && !char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }

    /// The last place of the escape sequence that starts at the escape: a control sequence ends with
    /// its final character, a command of the system with a bell or ESC \, a choice of a character
    /// set (ESC ( B) after two characters, others after one.
    private static int SequenceEnd(string output, int escape)
    {
        var kind = output[escape + 1];
        var at = escape + 2;
        if (kind is '(' or ')' or '*' or '+')
        {
            return Math.Min(escape + 2, output.Length - 1);
        }

        if (kind == '[')
        {
            while (at < output.Length && output[at] is < '@' or > '~')
            {
                at++;
            }

            return at;
        }

        if (kind == ']')
        {
            while (at < output.Length && output[at] != '\u0007' && !(output[at] == '\u001b' && at + 1 < output.Length && output[at + 1] == '\\'))
            {
                at++;
            }

            return at < output.Length && output[at] == '\u001b' ? at + 1 : at;
        }

        return escape + 1;
    }
}

/// The title of Claude Code (spike 2026-10): "✳ name" waits for input, a turning half circle before
/// the name means a turn is going on. The name is "Claude Code" until the first turn, then the
/// subject of the conversation.
public sealed record ClaudeTitle(bool Working, string Name)
{
    /// The name Claude Code shows before it knows the subject.
    public const string DefaultName = "Claude Code";

    private const string IdleMark = "✳";

    /// Null for a title that is not one of Claude Code.
    public static ClaudeTitle? Parse(string? title)
    {
        var text = (title ?? "").Trim();
        if (text.Length == 0)
        {
            return null;
        }

        var mark = char.IsSurrogate(text[0]) && text.Length > 1 ? text[..2] : text[..1];
        bool working;
        if (mark == IdleMark)
        {
            working = false;
        }
        else if (IsSpinner(text[0]))
        {
            working = true;
        }
        else
        {
            return null;
        }

        var name = text[mark.Length..].TrimStart('️').Trim();
        return new ClaudeTitle(working, name);
    }

    /// The name a session takes from the title: none before Claude Code knows the subject.
    public string? SessionName => Name.Length == 0 || Name == DefaultName ? null : Name;

    /// The half circles of the spike, the quarter ones next to them, and the braille dots that
    /// other versions of Claude Code turn.
    private static bool IsSpinner(char c) => c is >= '◐' and <= '◓' or >= '⠁' and <= '⣿';
}
