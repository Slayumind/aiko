namespace Aiko.Core.Terminal;

/// A piece of a line of an answer.
public abstract record MarkdownInline
{
    public sealed record Text(string Value) : MarkdownInline;

    public sealed record Code(string Value) : MarkdownInline;

    public sealed record Strong(IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

    public sealed record Emphasis(IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

    public sealed record Strike(IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;

    /// A picture is a link too: Aiko does not load pictures from the internet (D-294).
    public sealed record Link(string Target, IReadOnlyList<MarkdownInline> Inlines) : MarkdownInline;
}

public enum ColumnAlign
{
    None,
    Left,
    Center,
    Right,
}

/// A block of an answer.
public abstract record MarkdownBlock
{
    public sealed record Paragraph(IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

    public sealed record Heading(int Level, IReadOnlyList<MarkdownInline> Inlines) : MarkdownBlock;

    public sealed record Code(string Language, string Text) : MarkdownBlock;

    /// Start is the number of the first item of an ordered list.
    public sealed record List(bool Ordered, int Start, IReadOnlyList<IReadOnlyList<MarkdownBlock>> Items) : MarkdownBlock;

    public sealed record Quote(IReadOnlyList<MarkdownBlock> Blocks) : MarkdownBlock;

    public sealed record Table(
        IReadOnlyList<ColumnAlign> Columns,
        IReadOnlyList<IReadOnlyList<MarkdownInline>> Header,
        IReadOnlyList<IReadOnlyList<IReadOnlyList<MarkdownInline>>> Rows) : MarkdownBlock;

    public sealed record Rule : MarkdownBlock;
}

/// Reads the markdown of an answer into blocks (D-303).
///
/// This is a part of markdown, the part Claude writes: paragraphs, headings with #, lists, fenced
/// code, tables, quotes, rules, links, bold, italic and strike. Everything else stays text as it
/// was written. Three things differ from the full rules on purpose. A line break inside a
/// paragraph is kept, the way a terminal shows it. Code is only fenced, never made by an indent.
/// A fence that was not closed runs to the end, so an answer that is still being written already
/// shows its code as code.
///
/// The Swift core reads the same text unit by unit, so every character the rules look at is
/// ASCII. Anything above ASCII counts as a letter.
public static class Markdown
{
    public static IReadOnlyList<MarkdownBlock> Parse(string? text)
    {
        var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return Blocks(lines);
    }

    // ---- blocks ----

    private static List<MarkdownBlock> Blocks(IReadOnlyList<string> lines)
    {
        var blocks = new List<MarkdownBlock>();
        var at = 0;

        while (at < lines.Count)
        {
            var line = lines[at];
            var content = line.TrimStart(' ', '\t');

            if (content.Length == 0)
            {
                at++;
            }
            else if (FenceIn(content) is { } fence)
            {
                at = ReadCode(lines, at, fence, blocks);
            }
            else if (HeadingIn(content) is { } heading)
            {
                blocks.Add(heading);
                at++;
            }
            else if (IsRule(content))
            {
                blocks.Add(new MarkdownBlock.Rule());
                at++;
            }
            else if (content[0] == '>')
            {
                at = ReadQuote(lines, at, blocks);
            }
            else if (MarkerIn(content) is { } marker)
            {
                at = ReadList(lines, at, marker, blocks);
            }
            else if (TableStartsAt(lines, at))
            {
                at = ReadTable(lines, at, blocks);
            }
            else
            {
                at = ReadParagraph(lines, at, blocks);
            }
        }

        return blocks;
    }

    private readonly record struct Fence(char Mark, int Length, string Language);

    /// Three or more backticks or tildes. The first word after them names the language.
    private static Fence? FenceIn(string content)
    {
        var mark = content[0];
        if (mark != '`' && mark != '~')
        {
            return null;
        }

        var length = RunLength(content, 0);
        if (length < 3)
        {
            return null;
        }

        var info = content[length..].Trim(' ', '\t');
        if (mark == '`' && info.Contains('`'))
        {
            return null;
        }

        var end = info.IndexOfAny([' ', '\t']);
        return new Fence(mark, length, end < 0 ? info : info[..end]);
    }

    private static int ReadCode(IReadOnlyList<string> lines, int at, Fence fence, List<MarkdownBlock> blocks)
    {
        var indent = Indent(lines[at]);
        var body = new List<string>();
        at++;

        while (at < lines.Count)
        {
            var content = lines[at].TrimStart(' ', '\t');
            if (content.Length >= fence.Length
                && content[0] == fence.Mark
                && RunLength(content, 0) >= fence.Length
                && content[RunLength(content, 0)..].Trim(' ', '\t').Length == 0)
            {
                at++;
                break;
            }

            body.Add(WithoutIndent(lines[at], indent));
            at++;
        }

        blocks.Add(new MarkdownBlock.Code(fence.Language, string.Join('\n', body)));
        return at;
    }

    /// One to six # and a space. A # without the space is a word, not a heading.
    private static MarkdownBlock.Heading? HeadingIn(string content)
    {
        var level = content[0] == '#' ? RunLength(content, 0) : 0;
        if (level is < 1 or > 6 || (content.Length > level && content[level] != ' ' && content[level] != '\t'))
        {
            return null;
        }

        var title = content[level..].Trim(' ', '\t');
        var closing = title.Length;
        while (closing > 0 && title[closing - 1] == '#')
        {
            closing--;
        }
        if (closing < title.Length && (closing == 0 || title[closing - 1] == ' '))
        {
            title = title[..closing].TrimEnd(' ', '\t');
        }

        return new MarkdownBlock.Heading(level, Inlines(title));
    }

    /// Three or more of - * or _ and nothing else but spaces.
    private static bool IsRule(string content)
    {
        var mark = content[0];
        if (mark != '-' && mark != '*' && mark != '_')
        {
            return false;
        }

        var count = 0;
        foreach (var character in content)
        {
            if (character == mark)
            {
                count++;
            }
            else if (character != ' ' && character != '\t')
            {
                return false;
            }
        }
        return count >= 3;
    }

    private static int ReadQuote(IReadOnlyList<string> lines, int at, List<MarkdownBlock> blocks)
    {
        var inner = new List<string>();
        while (at < lines.Count)
        {
            var content = lines[at].TrimStart(' ', '\t');
            if (content.Length == 0 || content[0] != '>')
            {
                break;
            }

            inner.Add(content.Length > 1 && content[1] == ' ' ? content[2..] : content[1..]);
            at++;
        }

        blocks.Add(new MarkdownBlock.Quote(Blocks(inner)));
        return at;
    }

    private readonly record struct Marker(bool Ordered, int Start, int Width);

    /// A bullet (- * +) or a number with a dot or a bracket, then a space.
    private static Marker? MarkerIn(string content)
    {
        if (content[0] is '-' or '*' or '+')
        {
            return content.Length > 1 && content[1] == ' ' ? new Marker(false, 1, 2) : null;
        }

        var digits = 0;
        while (digits < content.Length && char.IsAsciiDigit(content[digits]))
        {
            digits++;
        }

        if (digits is < 1 or > 9
            || digits + 1 >= content.Length
            || (content[digits] != '.' && content[digits] != ')')
            || content[digits + 1] != ' ')
        {
            return null;
        }

        return new Marker(true, int.Parse(content[..digits]), digits + 2);
    }

    private static int ReadList(IReadOnlyList<string> lines, int at, Marker first, List<MarkdownBlock> blocks)
    {
        var listIndent = Indent(lines[at]);
        var items = new List<IReadOnlyList<MarkdownBlock>>();
        List<string>? item = null;
        var contentIndent = 0;

        while (at < lines.Count)
        {
            var line = lines[at];
            var content = line.TrimStart(' ', '\t');
            var indent = Indent(line);

            if (content.Length == 0)
            {
                // An empty line stays in the item only when the item goes on after it.
                var next = NextWithText(lines, at);
                if (next < 0 || !(Indent(lines[next]) >= contentIndent || IsSibling(lines[next], listIndent, contentIndent, first)))
                {
                    break;
                }
                item!.Add("");
                at++;
            }
            else if (IsSibling(line, listIndent, contentIndent, first) || item is null)
            {
                if (item is not null)
                {
                    items.Add(Blocks(item));
                }
                var marker = MarkerIn(content)!.Value;
                contentIndent = indent + marker.Width;
                item = [content[marker.Width..]];
                at++;
            }
            else if (indent >= contentIndent)
            {
                item.Add(WithoutIndent(line, contentIndent));
                at++;
            }
            else
            {
                break;
            }
        }

        if (item is not null)
        {
            items.Add(Blocks(item));
        }

        blocks.Add(new MarkdownBlock.List(first.Ordered, first.Start, items));
        return at;
    }

    /// The next item of the same list: a marker of the same kind that is not deep enough to
    /// belong to the item above it.
    private static bool IsSibling(string line, int listIndent, int contentIndent, Marker first)
    {
        var indent = Indent(line);
        var content = line.TrimStart(' ', '\t');
        return indent >= listIndent
            && indent < contentIndent
            && content.Length > 0
            && !IsRule(content)
            && MarkerIn(content) is { } marker
            && marker.Ordered == first.Ordered;
    }

    private static int NextWithText(IReadOnlyList<string> lines, int at)
    {
        for (var next = at + 1; next < lines.Count; next++)
        {
            if (lines[next].Trim(' ', '\t').Length > 0)
            {
                return next;
            }
        }
        return -1;
    }

    /// A row with a bar in it, and under it a row of dashes with the same number of cells.
    private static bool TableStartsAt(IReadOnlyList<string> lines, int at)
    {
        if (at + 1 >= lines.Count || !lines[at].Contains('|'))
        {
            return false;
        }

        var columns = ColumnsIn(lines[at + 1]);
        return columns is not null && columns.Count == Cells(lines[at]).Count;
    }

    private static List<ColumnAlign>? ColumnsIn(string line)
    {
        if (!line.Contains('-'))
        {
            return null;
        }

        var columns = new List<ColumnAlign>();
        foreach (var cell in Cells(line))
        {
            var left = cell.StartsWith(':');
            var right = cell.Length > 1 && cell.EndsWith(':');
            var dashes = cell[(left ? 1 : 0)..(cell.Length - (right ? 1 : 0))];
            if (dashes.Length == 0 || dashes.Any(character => character != '-'))
            {
                return null;
            }

            columns.Add(left && right ? ColumnAlign.Center
                : left ? ColumnAlign.Left
                : right ? ColumnAlign.Right
                : ColumnAlign.None);
        }
        return columns;
    }

    /// The cells of a row. A bar at either end is the frame, and \| is a bar inside a cell.
    private static List<string> Cells(string line)
    {
        var row = line.Trim(' ', '\t');
        if (row.StartsWith('|'))
        {
            row = row[1..];
        }
        if (row.EndsWith('|') && !row.EndsWith("\\|"))
        {
            row = row[..^1];
        }

        var cells = new List<string>();
        var cell = new System.Text.StringBuilder();
        for (var at = 0; at < row.Length; at++)
        {
            if (row[at] == '\\' && at + 1 < row.Length && row[at + 1] == '|')
            {
                cell.Append('|');
                at++;
            }
            else if (row[at] == '|')
            {
                cells.Add(cell.ToString().Trim(' ', '\t'));
                cell.Clear();
            }
            else
            {
                cell.Append(row[at]);
            }
        }
        cells.Add(cell.ToString().Trim(' ', '\t'));
        return cells;
    }

    private static int ReadTable(IReadOnlyList<string> lines, int at, List<MarkdownBlock> blocks)
    {
        var columns = ColumnsIn(lines[at + 1])!;
        var header = Cells(lines[at]).Select(Inlines).ToList();
        var rows = new List<IReadOnlyList<IReadOnlyList<MarkdownInline>>>();
        at += 2;

        while (at < lines.Count && lines[at].Contains('|'))
        {
            var cells = Cells(lines[at]);
            var row = new List<IReadOnlyList<MarkdownInline>>();
            for (var column = 0; column < columns.Count; column++)
            {
                row.Add(Inlines(column < cells.Count ? cells[column] : ""));
            }
            rows.Add(row);
            at++;
        }

        blocks.Add(new MarkdownBlock.Table(columns, header, rows));
        return at;
    }

    private static int ReadParagraph(IReadOnlyList<string> lines, int at, List<MarkdownBlock> blocks)
    {
        var text = new List<string>();
        while (at < lines.Count)
        {
            var content = lines[at].Trim(' ', '\t');
            if (content.Length == 0 || (text.Count > 0 && StartsAnotherBlock(lines, at, content)))
            {
                break;
            }
            text.Add(content);
            at++;
        }

        blocks.Add(new MarkdownBlock.Paragraph(Inlines(string.Join('\n', text))));
        return at;
    }

    /// What ends a paragraph without an empty line. A numbered list does it only when it starts
    /// at one: "2024. was a good year" in the middle of a paragraph is a sentence.
    private static bool StartsAnotherBlock(IReadOnlyList<string> lines, int at, string content) =>
        FenceIn(content) is not null
        || HeadingIn(content) is not null
        || IsRule(content)
        || content[0] == '>'
        || MarkerIn(content) is { Ordered: false } or { Start: 1 }
        || TableStartsAt(lines, at);

    /// Spaces in front of a line. A tab counts as four.
    private static int Indent(string line)
    {
        var indent = 0;
        foreach (var character in line)
        {
            if (character == ' ')
            {
                indent++;
            }
            else if (character == '\t')
            {
                indent += 4;
            }
            else
            {
                break;
            }
        }
        return indent;
    }

    /// The line without its first spaces, up to the given number of them.
    private static string WithoutIndent(string line, int indent)
    {
        var at = 0;
        var removed = 0;
        while (at < line.Length && removed < indent && (line[at] == ' ' || line[at] == '\t'))
        {
            removed += line[at] == '\t' ? 4 : 1;
            at++;
        }
        return line[at..];
    }

    private static int RunLength(string text, int at)
    {
        var end = at;
        while (end < text.Length && text[end] == text[at])
        {
            end++;
        }
        return end - at;
    }

    // ---- inlines ----

    private static List<MarkdownInline> Inlines(string text)
    {
        var inlines = new List<MarkdownInline>();
        var plain = new System.Text.StringBuilder();
        var at = 0;

        void Add(MarkdownInline inline, int next)
        {
            if (plain.Length > 0)
            {
                inlines.Add(new MarkdownInline.Text(plain.ToString()));
                plain.Clear();
            }
            inlines.Add(inline);
            at = next;
        }

        while (at < text.Length)
        {
            var character = text[at];

            if (character == '\\' && at + 1 < text.Length && IsPunctuation(text[at + 1]))
            {
                plain.Append(text[at + 1]);
                at += 2;
            }
            else if (character == '`' && CodeSpanAt(text, at) is { } span)
            {
                Add(new MarkdownInline.Code(span.Text), span.Next);
            }
            else if ((character == '[' || (character == '!' && at + 1 < text.Length && text[at + 1] == '['))
                && LinkAt(text, at) is { } link)
            {
                Add(link.Inline, link.Next);
            }
            else if (character == '<' && AngleLinkAt(text, at) is { } angle)
            {
                Add(angle.Inline, angle.Next);
            }
            else if (character == 'h' && BareLinkAt(text, at) is { } bare)
            {
                Add(bare.Inline, bare.Next);
            }
            else if ((character == '*' || character == '_' || character == '~') && EmphasisAt(text, at) is { } emphasis)
            {
                Add(emphasis.Inline, emphasis.Next);
            }
            else if (character is '`' or '*' or '_' or '~')
            {
                var run = RunLength(text, at);
                plain.Append(text, at, run);
                at += run;
            }
            else
            {
                plain.Append(character);
                at++;
            }
        }

        if (plain.Length > 0)
        {
            inlines.Add(new MarkdownInline.Text(plain.ToString()));
        }
        return inlines;
    }

    private readonly record struct Found(MarkdownInline Inline, int Next);

    private readonly record struct Span(string Text, int Next);

    /// Code between two runs of backticks of the same length.
    private static Span? CodeSpanAt(string text, int at)
    {
        var run = RunLength(text, at);
        var from = at + run;

        for (var search = from; search < text.Length; search++)
        {
            if (text[search] != '`')
            {
                continue;
            }

            var closing = RunLength(text, search);
            if (closing == run)
            {
                var code = text[from..search].Replace('\n', ' ');
                if (code.Length >= 2 && code[0] == ' ' && code[^1] == ' ' && code.Trim(' ').Length > 0)
                {
                    code = code[1..^1];
                }
                return new Span(code, search + closing);
            }
            search += closing - 1;
        }
        return null;
    }

    /// [words](target) and ![words](target). A title after the target is read and dropped.
    private static Found? LinkAt(string text, int at)
    {
        var open = text[at] == '!' ? at + 1 : at;
        var close = ClosingBracket(text, open);
        if (close < 0 || close + 1 >= text.Length || text[close + 1] != '(')
        {
            return null;
        }

        var cursor = close + 2;
        while (cursor < text.Length && IsSpace(text[cursor]))
        {
            cursor++;
        }

        string target;
        if (cursor < text.Length && text[cursor] == '<')
        {
            var end = text.IndexOf('>', cursor);
            if (end < 0)
            {
                return null;
            }
            target = text[(cursor + 1)..end];
            cursor = end + 1;
        }
        else
        {
            var start = cursor;
            var depth = 0;
            while (cursor < text.Length && !IsSpace(text[cursor]))
            {
                if (text[cursor] == '(')
                {
                    depth++;
                }
                else if (text[cursor] == ')')
                {
                    if (depth == 0)
                    {
                        break;
                    }
                    depth--;
                }
                cursor++;
            }
            target = text[start..cursor];
        }

        while (cursor < text.Length && IsSpace(text[cursor]))
        {
            cursor++;
        }

        if (cursor < text.Length && (text[cursor] == '"' || text[cursor] == '\''))
        {
            var end = text.IndexOf(text[cursor], cursor + 1);
            if (end < 0)
            {
                return null;
            }
            cursor = end + 1;
            while (cursor < text.Length && IsSpace(text[cursor]))
            {
                cursor++;
            }
        }

        if (cursor >= text.Length || text[cursor] != ')' || target.Length == 0)
        {
            return null;
        }

        var words = Inlines(text[(open + 1)..close]);
        if (words.Count == 0)
        {
            words.Add(new MarkdownInline.Text(target));
        }
        return new Found(new MarkdownInline.Link(target, words), cursor + 1);
    }

    /// The bracket that closes the one at this place. Brackets inside it are counted, code and
    /// escaped characters are stepped over.
    private static int ClosingBracket(string text, int open)
    {
        var depth = 0;
        for (var at = open; at < text.Length; at++)
        {
            if (text[at] == '\\')
            {
                at++;
            }
            else if (text[at] == '`' && CodeSpanAt(text, at) is { } span)
            {
                at = span.Next - 1;
            }
            else if (text[at] == '[')
            {
                depth++;
            }
            else if (text[at] == ']' && --depth == 0)
            {
                return at;
            }
        }
        return -1;
    }

    /// <https://example.com>
    private static Found? AngleLinkAt(string text, int at)
    {
        var end = text.IndexOf('>', at);
        if (end < 0)
        {
            return null;
        }

        var target = text[(at + 1)..end];
        return IsWebAddress(target) && !target.Any(IsSpace)
            ? new Found(new MarkdownInline.Link(target, [new MarkdownInline.Text(target)]), end + 1)
            : null;
    }

    /// An address written as it is. It ends at a space; a full stop or a bracket after it is not
    /// part of it.
    private static Found? BareLinkAt(string text, int at)
    {
        if (at > 0 && IsWordCharacter(text[at - 1]))
        {
            return null;
        }

        var end = at;
        while (end < text.Length && !IsSpace(text[end]) && text[end] != '<' && text[end] != '`')
        {
            end++;
        }

        while (end > at && IsTail(text, at, end))
        {
            end--;
        }

        var target = text[at..end];
        return IsWebAddress(target)
            ? new Found(new MarkdownInline.Link(target, [new MarkdownInline.Text(target)]), end)
            : null;
    }

    private static bool IsTail(string text, int from, int end)
    {
        var last = text[end - 1];
        if (last == ')')
        {
            var address = text[from..end];
            return address.Count(character => character == ')') > address.Count(character => character == '(');
        }
        return last is '.' or ',' or ';' or ':' or '!' or '?' or '\'' or '"' or '*' or '_' or '~' or ']';
    }

    private static bool IsWebAddress(string target) =>
        (target.StartsWith("https://", StringComparison.Ordinal) && target.Length > 8)
        || (target.StartsWith("http://", StringComparison.Ordinal) && target.Length > 7);

    /// **bold**, *italic*, ***both*** with * or _, and ~~strike~~.
    private static Found? EmphasisAt(string text, int at)
    {
        var mark = text[at];
        var run = RunLength(text, at);
        var after = at + run;

        if (after >= text.Length || IsSpace(text[after]) || run > 3
            || (mark == '~' && run != 2)
            || (mark == '_' && at > 0 && IsWordCharacter(text[at - 1])))
        {
            return null;
        }

        for (var size = run; size >= 1; size--)
        {
            if (mark == '~' && size != 2)
            {
                continue;
            }

            var from = at + size;
            if (CloserOf(text, from, mark, size) is not { } closer)
            {
                continue;
            }

            var inner = Inlines(text[from..closer.ContentEnd]);
            MarkdownInline inline = mark == '~' ? new MarkdownInline.Strike(inner)
                : size == 1 ? new MarkdownInline.Emphasis(inner)
                : size == 2 ? new MarkdownInline.Strong(inner)
                : new MarkdownInline.Emphasis([new MarkdownInline.Strong(inner)]);
            return new Found(inline, closer.Next);
        }
        return null;
    }

    private readonly record struct Closer(int ContentEnd, int Next);

    /// The run that closes an emphasis of this size. A run of exactly this size wins; if there is
    /// none, the end of the first longer run is taken, so "**bold *both***" closes both.
    private static Closer? CloserOf(string text, int from, char mark, int size)
    {
        Closer? longer = null;

        for (var at = from; at < text.Length; at++)
        {
            if (text[at] == '\\')
            {
                at++;
            }
            else if (text[at] == '`' && CodeSpanAt(text, at) is { } span)
            {
                at = span.Next - 1;
            }
            else if (text[at] == mark)
            {
                var run = RunLength(text, at);
                var next = at + run;
                var closes = at > from
                    && !IsSpace(text[at - 1])
                    && (mark != '_' || next >= text.Length || !IsWordCharacter(text[next]));

                if (closes && run == size)
                {
                    return new Closer(at, next);
                }
                if (closes && run > size && longer is null)
                {
                    longer = new Closer(next - size, next);
                }
                at = next - 1;
            }
        }
        return longer;
    }

    private static bool IsSpace(char character) => character is ' ' or '\t' or '\n';

    /// A letter or a digit. Everything above ASCII counts as a letter.
    private static bool IsWordCharacter(char character) => char.IsAsciiLetterOrDigit(character) || character >= 0x80;

    private static bool IsPunctuation(char character) =>
        character is >= '!' and <= '/' or >= ':' and <= '@' or >= '[' and <= '`' or >= '{' and <= '~';
}
