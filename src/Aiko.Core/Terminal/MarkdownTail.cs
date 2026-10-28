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
        var (inFence, tailStart) = Scan(markdown);
        if (markdown.Length == 0 || inFence)
        {
            return markdown;
        }

        // Inline marks never cross a blank line or a code block, so only the words after the
        // last of them can wait for one.
        var head = markdown[..tailStart];
        var tail = markdown[tailStart..];

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
            // Only spaces before it on its line: a star there may be a list marker.
            var lineBegin = at == 0 ? 0 : text.LastIndexOf('\n', at - 1) + 1;
            var lineStart = text.AsSpan(lineBegin, at - lineBegin).IsWhiteSpace();

            // A star with spaces on both sides, as in "2 * 3", is not emphasis.
            var spaced = (at == 0 || char.IsWhiteSpace(text[at - 1])) && (at + 1 >= text.Length || char.IsWhiteSpace(text[at + 1]));
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
                : text[at] == '*' && !(lineStart && at + 1 < text.Length && text[at + 1] == ' ') && !spaced ? "*"
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

    /// Whether the text ends inside a code block (``` or ~~~), and where the words after the last
    /// blank line or the last closed code block start. A blank line inside a block is not one.
    private static (bool InFence, int TailStart) Scan(string markdown)
    {
        string? fence = null;
        var tailStart = 0;
        var start = 0;
        while (true)
        {
            var end = markdown.IndexOf('\n', start);
            var line = end < 0 ? markdown[start..] : markdown[start..end];
            var next = end < 0 ? markdown.Length : end + 1;
            var trimmed = line.TrimStart();
            if (fence is null && (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal)))
            {
                fence = trimmed[..3];
            }
            else if (fence is not null && trimmed.StartsWith(fence, StringComparison.Ordinal))
            {
                fence = null;
                tailStart = next;
            }
            else if (fence is null && trimmed.Length == 0 && end >= 0)
            {
                tailStart = next;
            }

            if (end < 0)
            {
                return (fence is not null, tailStart);
            }

            start = next;
        }
    }
}
