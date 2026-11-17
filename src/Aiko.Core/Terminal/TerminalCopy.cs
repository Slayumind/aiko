namespace Aiko.Core.Terminal;

/// Text copied out of a terminal (D-371). Claude Code lays its text out by the width of the
/// terminal itself: a paragraph comes as rows, the rows after the first indented by two spaces, and
/// the terminal cannot tell them from lines that really end. Copied as it is, a paragraph falls
/// apart in the middle of a sentence. So a row is joined to the next when the first word of the next
/// would not have fit on it: the row was wrapped, not ended. The indent the rows share goes.
public static class TerminalCopy
{
    public static string Unwrap(string text, int columns)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(line => line.TrimEnd()).ToList();
        if (lines.Count < 2 || columns <= 0)
        {
            return string.Join('\n', lines);
        }

        // The indent of the rows after the first: what Claude Code puts before every following row.
        var indent = lines.Skip(1).Where(line => line.Length > 0).Select(Indent).DefaultIfEmpty(0).Min();

        var joined = new List<string> { lines[0] };

        // A selection starts after the mark of a message ("⏺ ", "⎿  "), and Claude Code puts the
        // following rows right under the start of the text: the first row was as wide as its text
        // and that indent.
        var lastWidth = (Indent(lines[0]) == 0 ? indent : 0) + lines[0].Length;
        for (var at = 1; at < lines.Count; at++)
        {
            var row = lines[at];
            var line = row.Length >= indent ? row[indent..] : row.TrimStart();
            var previous = joined[^1];

            if (Wrapped(previous, lastWidth, line, row, indent, columns))
            {
                joined[^1] = previous + " " + line;
            }
            else
            {
                joined.Add(line);
            }

            lastWidth = row.Length;
        }

        return string.Join('\n', joined);
    }

    /// Whether the row before was wrapped onto this one. width is how long the row before was on
    /// the screen, its indent counted.
    private static bool Wrapped(string previous, int width, string line, string row, int indent, int columns)
    {
        if (previous.Trim().Length == 0 || line.Length == 0 || Indent(row) > indent)
        {
            return false;
        }

        if (IsListItem(line) || HasFrame(previous) || HasFrame(line))
        {
            return false;
        }

        var firstWord = line.Split(' ', 2)[0];
        return width + 1 + firstWord.Length > columns;
    }

    private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

    /// "- ", "* ", "• " or "12. " starts a point of a list: a line of its own.
    private static bool IsListItem(string line)
    {
        if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("• ", StringComparison.Ordinal))
        {
            return true;
        }

        var digits = 0;
        while (digits < line.Length && char.IsAsciiDigit(line[digits]))
        {
            digits++;
        }

        return digits > 0 && digits + 1 < line.Length && line[digits] == '.' && line[digits + 1] == ' ';
    }

    /// The lines of a table or a box: never joined.
    private static bool HasFrame(string line) => line.Any(c => c is >= '─' and <= '╿');
}
