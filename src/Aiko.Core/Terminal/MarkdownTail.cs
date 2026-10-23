namespace Aiko.Core.Terminal;

/// The end of an answer that is still being written, made whole for drawing (D-332).
///
/// While Claude writes "**bold" or "`code", the closing marks have not come yet, and markdown
/// shows the stars and the backtick as text until they do; then they vanish at once. Like the
/// "remend" step of opencode, the end of the last paragraph gets the marks it still waits for.
/// An opening mark with nothing after it yet is dropped instead, so no empty mark is drawn. A
/// code block still open is left alone: it is drawn whole as it is.
public static class MarkdownTail
{
    public static string Closed(string markdown)
    {
        if (markdown.Length == 0 || InOpenFence(markdown))
        {
            return markdown;
        }

        // Inline marks never cross a blank line, so only the last paragraph can wait for one.
        var cut = markdown.LastIndexOf("\n\n", StringComparison.Ordinal);
        var head = cut < 0 ? "" : markdown[..(cut + 2)];
        var tail = cut < 0 ? markdown : markdown[(cut + 2)..];

        var open = OpenMarks(tail);
        if (open.Count == 0)
        {
            return markdown;
        }

        // A mark at the very end opens nothing yet: drop it, and close what is left.
        var trimmed = tail.TrimEnd();
        while (open.Count > 0 && trimmed.EndsWith(open[^1].Mark, StringComparison.Ordinal) && trimmed.Length - open[^1].Mark.Length == open[^1].At)
        {
            trimmed = trimmed[..open[^1].At].TrimEnd();
            open.RemoveAt(open.Count - 1);
        }

        var closers = string.Concat(Enumerable.Reverse(open).Select(mark => mark.Mark));
        return head + trimmed + closers;
    }

    private sealed record OpenMark(string Mark, int At);

    /// The marks opened and not closed, the last opened last. Inside inline code nothing counts.
    private static List<OpenMark> OpenMarks(string text)
    {
        var open = new List<OpenMark>();
        var at = 0;
        while (at < text.Length)
        {
            var lineStart = at == 0 || text[at - 1] == '\n';
            if (text[at] == '`')
            {
                var end = text.IndexOf('`', at + 1);
                if (end < 0)
                {
                    open.Add(new OpenMark("`", at));
                    return open;
                }

                at = end + 1;
                continue;
            }

            var mark = text.AsSpan(at).StartsWith("**") ? "**"
                : text.AsSpan(at).StartsWith("~~") ? "~~"
                : text[at] == '*' && !(lineStart && at + 1 < text.Length && text[at + 1] == ' ') ? "*"
                : null;
            if (mark is null)
            {
                at++;
                continue;
            }

            var last = open.FindLastIndex(item => item.Mark == mark);
            if (last >= 0)
            {
                open.RemoveAt(last);
            }
            else
            {
                open.Add(new OpenMark(mark, at));
            }

            at += mark.Length;
        }

        return open;
    }

    private static bool InOpenFence(string markdown) =>
        markdown.Split('\n').Count(line => line.TrimStart().StartsWith("```", StringComparison.Ordinal)) % 2 == 1;
}
