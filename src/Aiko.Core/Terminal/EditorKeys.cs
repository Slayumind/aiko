using System.Text;

namespace Aiko.Core.Terminal;

/// A key of the editor under the terminal that may matter to the program (D-354). Every other key
/// is the editor's own, and so is a letter while the editor holds text.
public enum EditorKey
{
    Up,
    Down,
    Left,
    Right,
    Enter,
    Escape,
    Tab,
    Backspace,

    /// A character typed: a digit, "/" or any other. The character itself comes with it.
    Character,

    /// Ctrl+C.
    Interrupt,
}

/// What a key does.
public enum EditorAction
{
    /// The editor handles it as a text box would.
    Keep,

    /// The sequence goes to the program; the editor stays as it is.
    Pass,

    /// The text and the pictures go to the program as one message.
    Send,
    NewLine,

    /// The command before or after in the history of a command line.
    HistoryBack,
    HistoryForward,

    /// The path before the caret is completed.
    Complete,
}

/// What a key did, and whether the editor now passes every key to the program.
public sealed record EditorPress(EditorAction Action, string Sequence, bool Passing);

/// Where the editor stands when a key comes.
public sealed record EditorState(SessionKind Kind, bool Empty, bool FirstLine, bool LastLine, bool Passing);

/// The keys of the editor (D-354, D-369). An empty editor lets the keys that answer a program
/// through: arrows, Enter, Esc, Shift+Tab, digits, "/" and Ctrl+C. "/" opens a menu of Claude Code
/// in the terminal, so after it the editor passes every key, letters too, until Enter, Esc or Ctrl+C
/// ends the menu. Esc always goes to the program: it stops Claude Code even with text in the
/// editor. With text, Enter sends and Shift+Enter starts a line. In a command line the arrows at the
/// first and the last line walk the history, and Tab completes a path.
public static class EditorKeys
{
    public const string Escape = "\u001b";

    public static EditorPress For(EditorState state, EditorKey key, bool shift, char character = '\0')
    {
        if (key == EditorKey.Escape)
        {
            return new(EditorAction.Pass, Escape, false);
        }

        if (key == EditorKey.Interrupt)
        {
            return state.Empty || state.Passing ? new(EditorAction.Pass, "\u0003", false) : Keep(state);
        }

        if (state.Passing && state.Empty)
        {
            return key switch
            {
                EditorKey.Enter => new(EditorAction.Pass, "\r", false),
                _ => new(EditorAction.Pass, Sequence(key, shift, character), true),
            };
        }

        if (state.Empty)
        {
            return key switch
            {
                EditorKey.Up or EditorKey.Down when state.Kind == SessionKind.Shell =>
                    new(key == EditorKey.Up ? EditorAction.HistoryBack : EditorAction.HistoryForward, "", false),
                EditorKey.Enter when shift => new(EditorAction.NewLine, "", false),
                EditorKey.Up or EditorKey.Down or EditorKey.Left or EditorKey.Right or EditorKey.Enter or EditorKey.Backspace =>
                    new(EditorAction.Pass, Sequence(key, shift, character), false),
                EditorKey.Tab when shift => new(EditorAction.Pass, "\u001b[Z", false),
                EditorKey.Tab => new(EditorAction.Pass, "\t", false),
                EditorKey.Character when char.IsAsciiDigit(character) => new(EditorAction.Pass, character.ToString(), false),
                EditorKey.Character when character == '/' => new(EditorAction.Pass, "/", true),
                _ => Keep(state),
            };
        }

        return key switch
        {
            EditorKey.Enter => new(shift ? EditorAction.NewLine : EditorAction.Send, "", false),
            EditorKey.Up when state.Kind == SessionKind.Shell && state.FirstLine => new(EditorAction.HistoryBack, "", false),
            EditorKey.Down when state.Kind == SessionKind.Shell && state.LastLine => new(EditorAction.HistoryForward, "", false),
            EditorKey.Tab when !shift && state.Kind == SessionKind.Shell => new(EditorAction.Complete, "", false),
            _ => Keep(state),
        };
    }

    private static EditorPress Keep(EditorState state) => new(EditorAction.Keep, "", state.Passing && state.Empty);

    /// What a terminal sends for the key.
    private static string Sequence(EditorKey key, bool shift, char character) => key switch
    {
        EditorKey.Up => "\u001b[A",
        EditorKey.Down => "\u001b[B",
        EditorKey.Right => "\u001b[C",
        EditorKey.Left => "\u001b[D",
        EditorKey.Enter => "\r",
        EditorKey.Backspace => "\u007f",
        EditorKey.Tab => shift ? "\u001b[Z" : "\t",
        EditorKey.Character => character == '\0' ? "" : character.ToString(),
        _ => "",
    };
}

/// What goes to the program when the editor sends (D-360): each picture as a paste of its path on
/// its own, so Claude Code makes it an attachment; then the text as one paste, its lines kept; then
/// Enter on its own. Between the writes the window waits a moment (60 ms in the spike).
public static class EditorSend
{
    public const string PasteStart = "\u001b[200~";
    public const string PasteEnd = "\u001b[201~";

    public static IReadOnlyList<string> Writes(string text, IReadOnlyList<string> pictures)
    {
        var writes = new List<string>();
        foreach (var picture in pictures)
        {
            writes.Add(PasteStart + picture + PasteEnd);
        }

        // A terminal pastes a line break as a carriage return, as Windows Terminal does.
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
        if (lines.Length > 0)
        {
            writes.Add(PasteStart + lines.Replace('\n', '\r') + PasteEnd);
        }

        if (writes.Count > 0)
        {
            writes.Add("\r");
        }

        return writes;
    }
}

/// The commands a command line got from the editor in this session, oldest first (D-369). The
/// arrows walk back and forth; what was typed before the walk comes back at its end.
public sealed record CommandHistory(IReadOnlyList<string> Commands, int At, string Draft)
{
    public static readonly CommandHistory Empty = new([], 0, "");

    /// A command sent. The same command twice in a row is kept once; the walk starts again.
    public CommandHistory Sent(string command)
    {
        var line = command.Trim();
        var commands = line.Length == 0 || (Commands.Count > 0 && Commands[^1] == line) ? Commands : [.. Commands, line];
        return new CommandHistory(commands, commands.Count, "");
    }

    /// The command before, or null at the oldest. text is what the editor holds now.
    public (CommandHistory History, string? Text) Back(string text)
    {
        if (At == 0)
        {
            return (this, null);
        }

        var draft = At == Commands.Count ? text : Draft;
        return (this with { At = At - 1, Draft = draft }, Commands[At - 1]);
    }

    /// The command after, or what was typed before the walk; null when no walk goes on.
    public (CommandHistory History, string? Text) Forward()
    {
        if (At >= Commands.Count)
        {
            return (this, null);
        }

        var at = At + 1;
        return (this with { At = at }, at == Commands.Count ? Draft : Commands[at]);
    }
}

/// One entry of a folder: its name and whether it is a folder.
public sealed record FolderEntry(string Name, bool Folder);

/// What Tab did: the new text and caret, and the names to choose from when more than one fits.
public sealed record Completion(string Text, int Caret, IReadOnlyList<string> Choices);

/// Tab in a command line (D-369): the word before the caret is a path, read against the folder of
/// the session. One match is completed, with a separator after a folder and a space after a file;
/// several are completed as far as they agree, and offered. Case does not matter, as on Windows.
public static class PathCompletion
{
    /// list gives the entries of a folder, or none when it cannot be read. separator is the one the
    /// shell writes: "/" for Git Bash, "\" for PowerShell. Null when nothing fits.
    public static Completion? Complete(string text, int caret, string folder, char separator, Func<string, IReadOnlyList<FolderEntry>> list)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        var start = caret;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1]) && text[start - 1] != '"')
        {
            start--;
        }

        var word = text[start..caret];
        var cut = Math.Max(word.LastIndexOf('/'), word.LastIndexOf('\\'));
        var typedFolder = cut >= 0 ? word[..(cut + 1)] : "";
        var typedName = word[(cut + 1)..];

        var where = Tidy(typedFolder.Length == 0 ? folder
            : IsRooted(typedFolder) ? typedFolder
            : folder.TrimEnd('/', '\\') + "/" + typedFolder, folder.Contains('\\') || typedFolder.Contains('\\') ? '\\' : '/');
        var matches = list(where)
            .Where(entry => entry.Name.StartsWith(typedName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (matches.Count == 0)
        {
            return null;
        }

        var one = matches.Count == 1;
        var name = one ? matches[0].Name : CommonStart([.. matches.Select(entry => entry.Name)], typedName.Length);
        var path = typedFolder + name + (one && matches[0].Folder ? separator.ToString() : "");

        // A path with a space is quoted whole; a whole file name is followed by a space.
        var written = path.Contains(' ') ? "\"" + path + "\"" : path;
        if (one && !matches[0].Folder)
        {
            written += " ";
        }

        IReadOnlyList<string> choices = one ? [] : [.. matches.Select(entry => entry.Folder ? entry.Name + separator : entry.Name)];
        return new Completion(text[..start] + written + text[caret..], start + written.Length, choices);
    }

    private static bool IsRooted(string path) =>
        path.StartsWith('/') || path.StartsWith('\\') || path.StartsWith('~') || (path.Length >= 2 && path[1] == ':');

    /// One separator all through, and none at the end, except after the root of a drive or "/".
    private static string Tidy(string path, char separator)
    {
        var same = path.Replace(separator == '\\' ? '/' : '\\', separator);
        var trimmed = same.TrimEnd(separator);
        return trimmed.Length == 0 ? same[..1] : trimmed.EndsWith(':') ? trimmed + separator : trimmed;
    }

    /// The longest start the names share, keeping the case of the first one; never shorter than
    /// what was typed.
    private static string CommonStart(List<string> names, int typed)
    {
        var length = names.Min(name => name.Length);
        var common = 0;
        while (common < length && names.All(name => char.ToLowerInvariant(name[common]) == char.ToLowerInvariant(names[0][common])))
        {
            common++;
        }

        return names[0][..Math.Max(common, Math.Min(typed, names[0].Length))];
    }
}
