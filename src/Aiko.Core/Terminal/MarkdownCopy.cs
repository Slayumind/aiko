namespace Aiko.Core.Terminal;

/// A place in the prose of an answer: a piece of text the window drew and a character in it.
public readonly record struct TextPlace(int Leaf, int Offset) : IComparable<TextPlace>
{
    public int CompareTo(TextPlace other) =>
        Leaf != other.Leaf ? Leaf.CompareTo(other.Leaf) : Offset.CompareTo(other.Offset);
}

/// Writes what the person selected in the prose of an answer back as markdown (D-303).
///
/// The window draws each piece of text as one leaf: plain text, inline code, and a block of code
/// inside a list or a quote. Leaves are counted in the order they are drawn. A selection inside one
/// paragraph, heading or block of code copies only the selected text, with the marks around it.
/// A selection over several of them copies whole lines with their marks.
public static class MarkdownCopy
{
    /// The number of each leaf, found by the very object the window drew it from.
    public static IReadOnlyDictionary<object, int> Leaves(IReadOnlyList<MarkdownBlock> blocks)
    {
        var leaves = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        foreach (var (leaf, _) in Walk(blocks))
        {
            leaves[leaf] = leaves.Count;
        }

        return leaves;
    }

    public static string Selected(IReadOnlyList<MarkdownBlock> blocks, TextPlace from, TextPlace to)
    {
        if (to.CompareTo(from) < 0)
        {
            (from, to) = (to, from);
        }

        var leaves = Walk(blocks).ToList();
        if (from.Leaf >= leaves.Count || from == to)
        {
            return "";
        }

        var writer = new Writer(from, to);
        var line = leaves[from.Leaf].Line;
        if (!ReferenceEquals(line, leaves[Math.Min(to.Leaf, leaves.Count - 1)].Line))
        {
            return writer.Blocks(blocks);
        }

        // One line: only the text, without the # of a heading or the marker of a list.
        writer.Next = leaves.FindIndex(leaf => ReferenceEquals(leaf.Line, line));
        return line switch
        {
            MarkdownBlock.Paragraph paragraph => writer.Inlines(paragraph.Inlines),
            MarkdownBlock.Heading heading => writer.Inlines(heading.Inlines),
            MarkdownBlock.Code code => writer.Leaf(code.Text),
            MarkdownBlock.Table table => InTable(writer, table, from, to),
            _ => "",
        };
    }

    /// Inside one cell only its selected words go, as in a paragraph; over several cells the
    /// table goes with the selected words in its cells.
    private static string InTable(Writer writer, MarkdownBlock.Table table, TextPlace from, TextPlace to)
    {
        var start = writer.Next;
        foreach (var cell in Cells(table))
        {
            var count = Walk(cell).Count();
            if (from.Leaf >= start && to.Leaf < start + count)
            {
                writer.Next = start;
                return writer.Inlines(cell);
            }

            start += count;
        }

        return writer.Table(table);
    }

    /// Each leaf with the paragraph, heading or block of code it is drawn in.
    private static IEnumerable<(object Leaf, MarkdownBlock Line)> Walk(IEnumerable<MarkdownBlock> blocks)
    {
        foreach (var block in blocks)
        {
            var leaves = block switch
            {
                MarkdownBlock.Paragraph paragraph => Walk(paragraph.Inlines).Select(leaf => (leaf, block)),
                MarkdownBlock.Heading heading => Walk(heading.Inlines).Select(leaf => (leaf, block)),
                MarkdownBlock.Code => [((object)block, block)],
                MarkdownBlock.List list => list.Items.SelectMany(Walk),
                MarkdownBlock.Quote quote => Walk(quote.Blocks),
                MarkdownBlock.Table table => Cells(table).SelectMany(Walk).Select(leaf => (leaf, block)),
                _ => [],
            };
            foreach (var leaf in leaves)
            {
                yield return leaf;
            }
        }
    }

    /// The cells of a table in the order they are drawn: the header, then row after row.
    private static IEnumerable<IReadOnlyList<MarkdownInline>> Cells(MarkdownBlock.Table table) =>
        table.Header.Concat(table.Rows.SelectMany(row => row));

    private static IEnumerable<object> Walk(IEnumerable<MarkdownInline> inlines) =>
        inlines.SelectMany(inline => inline switch
        {
            MarkdownInline.Text or MarkdownInline.Code => [inline],
            MarkdownInline.Strong strong => Walk(strong.Inlines),
            MarkdownInline.Emphasis emphasis => Walk(emphasis.Inlines),
            MarkdownInline.Strike strike => Walk(strike.Inlines),
            MarkdownInline.Link link => Walk(link.Inlines),
            _ => [],
        });

    /// Writes the blocks in the order of their leaves. A leaf outside the selection writes
    /// nothing, and a mark around nothing is left out too.
    private sealed class Writer(TextPlace from, TextPlace to)
    {
        /// The number of the next leaf.
        public int Next { get; set; }

        public string Leaf(string text)
        {
            var at = Next++;
            if (at < from.Leaf || at > to.Leaf)
            {
                return "";
            }

            var start = at == from.Leaf ? Math.Clamp(from.Offset, 0, text.Length) : 0;
            var end = at == to.Leaf ? Math.Clamp(to.Offset, 0, text.Length) : text.Length;
            return end > start ? text[start..end] : "";
        }

        public string Inlines(IReadOnlyList<MarkdownInline> inlines) =>
            string.Concat(inlines.Select(Inline));

        public string Blocks(IReadOnlyList<MarkdownBlock> blocks) =>
            string.Join("\n\n", blocks.Select(Block).ToList().Where(text => text.Length > 0));

        private string Inline(MarkdownInline inline) => inline switch
        {
            MarkdownInline.Text text => Leaf(text.Value),
            MarkdownInline.Code code => CodeSpan(Leaf(code.Value)),
            MarkdownInline.Strong strong => Around("**", Inlines(strong.Inlines)),
            MarkdownInline.Emphasis emphasis => Around("*", Inlines(emphasis.Inlines)),
            MarkdownInline.Strike strike => Around("~~", Inlines(strike.Inlines)),
            MarkdownInline.Link link => Link(link),
            _ => "",
        };

        private static string CodeSpan(string code) => code.Length == 0
            ? ""
            : code.Contains('`') ? $"`` {code} ``" : $"`{code}`";

        private static string Around(string mark, string inner) =>
            inner.Length == 0 ? "" : mark + inner + mark;

        /// An address written as it is stays as it is; a link with words keeps its target.
        private string Link(MarkdownInline.Link link)
        {
            var words = Inlines(link.Inlines);
            var bare = link.Inlines is [MarkdownInline.Text text] && text.Value == link.Target;
            return words.Length == 0 || bare ? words : $"[{words}]({link.Target})";
        }

        private string Block(MarkdownBlock block) => block switch
        {
            MarkdownBlock.Paragraph paragraph => Inlines(paragraph.Inlines),
            MarkdownBlock.Heading heading => Prefixed(new string('#', heading.Level) + " ", Inlines(heading.Inlines)),
            MarkdownBlock.Code code => Fence(code.Language, Leaf(code.Text)),
            MarkdownBlock.List list => List(list),
            MarkdownBlock.Quote quote => Quote(Blocks(quote.Blocks)),
            MarkdownBlock.Rule => Rule(),
            MarkdownBlock.Table table => Table(table),
            _ => "",
        };

        /// A table as markdown, with only the selected words in its cells. A table with nothing
        /// selected in it writes nothing.
        public string Table(MarkdownBlock.Table table)
        {
            var header = table.Header.Select(Inlines).ToList();
            var rows = table.Rows.Select(row => row.Select(Inlines).ToList()).ToList();
            if (header.Concat(rows.SelectMany(row => row)).All(cell => cell.Length == 0))
            {
                return "";
            }

            static string Line(IEnumerable<string> cells) => "| " + string.Join(" | ", cells) + " |";
            var rule = "|" + string.Join("|", table.Columns.Select(align => align switch
            {
                ColumnAlign.Left => ":---",
                ColumnAlign.Center => ":---:",
                ColumnAlign.Right => "---:",
                _ => "---",
            })) + "|";
            return string.Join("\n", [Line(header), rule, .. rows.Select(Line)]);
        }

        private static string Prefixed(string prefix, string text) => text.Length == 0 ? "" : prefix + text;

        private static string Fence(string language, string code)
        {
            if (code.Length == 0)
            {
                return "";
            }

            var fence = "```";
            while (code.Contains(fence))
            {
                fence += "`";
            }

            return $"{fence}{language}\n{code}\n{fence}";
        }

        private string List(MarkdownBlock.List list)
        {
            var items = new List<string>();
            for (var at = 0; at < list.Items.Count; at++)
            {
                var item = Blocks(list.Items[at]);
                if (item.Length > 0)
                {
                    var marker = list.Ordered ? $"{list.Start + at}. " : "- ";
                    items.Add(marker + Indented(item, new string(' ', marker.Length)));
                }
            }

            return string.Join("\n", items);
        }

        private static string Indented(string text, string pad) =>
            string.Join("\n", text.Split('\n').Select((line, at) => at == 0 || line.Length == 0 ? line : pad + line));

        private static string Quote(string text) => text.Length == 0
            ? ""
            : string.Join("\n", text.Split('\n').Select(line => line.Length == 0 ? ">" : "> " + line));

        /// A rule is copied when the selection runs over it: a leaf before it and one after it.
        private string Rule() => from.Leaf < Next && Next <= to.Leaf ? "---" : "";
    }
}
