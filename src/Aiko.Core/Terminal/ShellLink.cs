namespace Aiko.Core.Terminal;

/// The address or the file under the mouse in a line of output, for Ctrl+click (D-358): an
/// http(s) address, or words that look like a file with an optional line ("src/a.cs:42").
/// Rows: spec/cases/shell-ask.
public static class ShellLink
{
    private static readonly char[] Stops = [' ', '\t', '"', '\'', '(', ')', '[', ']', '<', '>', ',', '{', '}'];

    public static string? At(string line, int column)
    {
        if (column < 0 || column >= line.Length || Array.IndexOf(Stops, line[column]) >= 0)
        {
            return null;
        }

        var start = column;
        while (start > 0 && Array.IndexOf(Stops, line[start - 1]) < 0)
        {
            start--;
        }

        var end = column + 1;
        while (end < line.Length && Array.IndexOf(Stops, line[end]) < 0)
        {
            end++;
        }

        var word = line[start..end].TrimEnd('.', ',', ';', '!', '?', ':');
        if (word.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || word.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return word.Length > 8 ? word : null;
        }

        return FileLink.Parse(word) is not null ? word : null;
    }
}
