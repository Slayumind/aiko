using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Aiko.Core.Terminal;
using LinkTarget = Aiko.Core.Terminal.LinkTarget;

namespace Aiko.App;

/// An answer of Claude drawn from its markdown (D-303, D-304). The core reads the markdown and cuts
/// it into pieces; this only draws them: prose as a document that can be selected, each block of
/// code in its own frame with a copy button, each table as a grid.
static class AnswerView
{
    public static UIElement For(string markdown)
    {
        var column = new StackPanel { Margin = new Thickness(0, 6, 0, 10) };
        foreach (var segment in AnswerSegments.Of(Markdown.Parse(markdown)))
        {
            column.Children.Add(Segment(segment));
        }

        return column;
    }

    /// One piece of an answer, drawn the way it stays.
    public static UIElement Segment(AnswerSegment segment) => segment switch
    {
        AnswerSegment.Prose prose => ProseBox(prose.Blocks),
        AnswerSegment.Code code => CodeBlock(code.Language, code.Text),
        AnswerSegment.Table table => Table(table.Value),
        _ => new Border(),
    };

    /// A finished answer with a quiet copy icon under it that copies the whole answer as its
    /// markdown, the way Claude wrote it (D-303). The icon shows while the mouse is over the
    /// answer (D-332).
    public static UIElement WithCopy(string markdown)
    {
        // The icon takes a thin line of its own under the answer. Laid over the margin below, it
        // went under the field of the last answer (check of 2026-10-25).
        var column = new StackPanel { Background = Brushes.Transparent };
        var answer = For(markdown);
        if (answer is FrameworkElement body)
        {
            body.Margin = new Thickness(body.Margin.Left, body.Margin.Top, body.Margin.Right, 0);
        }

        column.Children.Add(answer);

        var copy = CopyButton.For(() => markdown);
        copy.Margin = new Thickness(-8, -4, 0, 4);
        copy.HorizontalAlignment = HorizontalAlignment.Left;
        CopyButton.ShowOnHover(column, copy);
        column.Children.Add(copy);
        return column;
    }

    // ---- prose ----

    /// The prose of an answer in a box one can select and copy from; the ink of a streaming
    /// answer works on its document.
    public static RichTextBox ProseBox(IReadOnlyList<MarkdownBlock> blocks)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = Tokens.Get<FontFamily>("Sans"),
            FontSize = Tokens.Get<double>("TextName"),
            Foreground = Tokens.Brush("Ink"),
            LineHeight = 22,
        };
        foreach (var block in blocks)
        {
            document.Blocks.Add(Block(block));
        }

        var box = new RichTextBox
        {
            Document = document,
            IsReadOnly = true,
            IsDocumentEnabled = true,
            IsReadOnlyCaretVisible = false,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Margin = new Thickness(-2, 0, 0, 0),
            SelectionBrush = Tokens.Brush("Muted"),
            Focusable = true,
        };

        // Ctrl+C and the menu copy the markdown of the selection, not the text on screen (D-303).
        box.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (_, e) =>
        {
            CopySelection(box.Selection, blocks);
            e.Handled = true;
        }));
        return box;
    }

    private static void CopySelection(TextSelection selection, IReadOnlyList<MarkdownBlock> blocks)
    {
        var leaves = MarkdownCopy.Leaves(blocks);
        if (Place(selection.Start, leaves, LogicalDirection.Forward) is { } from
            && Place(selection.End, leaves, LogicalDirection.Backward) is { } to
            && MarkdownCopy.Selected(blocks, from, to) is { Length: > 0 } markdown)
        {
            Clipboard.SetText(markdown);
        }
    }

    /// The leaf and the character a point of the selection falls on. A point between two pieces
    /// of text (at the edge of a paragraph or a bold word) moves to the nearest text inside the
    /// selection.
    private static TextPlace? Place(TextPointer point, IReadOnlyDictionary<object, int> leaves, LogicalDirection inward)
    {
        if (point.Parent is Run { Tag: { } tag } run && leaves.TryGetValue(tag, out var leaf))
        {
            return new TextPlace(leaf, run.ContentStart.GetOffsetToPosition(point));
        }

        for (var next = point.GetNextContextPosition(inward); next is not null; next = next.GetNextContextPosition(inward))
        {
            if (next.Parent is Run { Tag: { } found } text && leaves.TryGetValue(found, out var at))
            {
                return new TextPlace(at, inward == LogicalDirection.Forward ? 0 : text.Text.Length);
            }
        }

        return null;
    }

    private static Block Block(MarkdownBlock block) => block switch
    {
        MarkdownBlock.Paragraph paragraph => Paragraph(paragraph.Inlines, new Thickness(0, 0, 0, 10)),
        MarkdownBlock.Heading heading => Heading(heading),
        MarkdownBlock.List list => List(list),
        MarkdownBlock.Quote quote => Quote(quote),
        MarkdownBlock.Rule => new BlockUIContainer(new Border
        {
            Height = 1,
            Background = Tokens.Brush("Hairline"),
            Margin = new Thickness(0, 6, 0, 14),
        }),
        // Code and tables are pieces of their own; inside a list or a quote they stay text.
        MarkdownBlock.Code code => CodeAsText(code),
        MarkdownBlock.Table => new Paragraph(),
        _ => new Paragraph(),
    };

    private static Paragraph Paragraph(IReadOnlyList<MarkdownInline> inlines, Thickness margin)
    {
        var paragraph = new Paragraph { Margin = margin };
        paragraph.Inlines.AddRange(Inlines(inlines));
        return paragraph;
    }

    private static Paragraph Heading(MarkdownBlock.Heading heading)
    {
        var paragraph = Paragraph(heading.Inlines, new Thickness(0, heading.Level == 1 ? 8 : 6, 0, 8));
        paragraph.FontSize = heading.Level switch
        {
            1 => 19,
            2 => 17,
            _ => Tokens.Get<double>("TextName"),
        };
        paragraph.FontWeight = heading.Level <= 2 ? FontWeights.Medium : FontWeights.SemiBold;
        return paragraph;
    }

    private const double ListIndent = 22;
    private const double MarkerGap = 8;

    /// The marker of a list item is drawn by Aiko, not by the list: a list draws its markers with
    /// a brush of its own that no item can change, so the ink of a streaming answer could not hold
    /// a marker back until the words of its item come (check of 2026-10-23). The marker stands in
    /// the hanging indent of the item's first line; numbers stand right-aligned, so "9." and "10."
    /// end in one column (AIKO-244).
    private static List List(MarkdownBlock.List list)
    {
        var start = Math.Max(1, list.Start);
        var indent = list.Ordered ? Math.Max(ListIndent, MarkerWidth(start + list.Items.Count - 1) + MarkerGap) : ListIndent;
        var drawn = new List
        {
            MarkerStyle = TextMarkerStyle.None,
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(indent, 0, 0, 0),
        };

        var number = start;
        foreach (var item in list.Items)
        {
            var listItem = new ListItem();
            foreach (var block in item)
            {
                var inner = Block(block);
                inner.Margin = new Thickness(0, 0, 0, 4);
                listItem.Blocks.Add(inner);
            }

            if (listItem.Blocks.FirstBlock is not Paragraph first)
            {
                first = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
                if (listItem.Blocks.FirstBlock is { } before)
                {
                    listItem.Blocks.InsertBefore(before, first);
                }
                else
                {
                    listItem.Blocks.Add(first);
                }
            }

            first.TextIndent = -indent;
            var marker = Marker(list.Ordered ? $"{number}." : "\u2022", indent);
            if (first.Inlines.FirstInline is { } firstInline)
            {
                first.Inlines.InsertBefore(firstInline, marker);
            }
            else
            {
                first.Inlines.Add(marker);
            }

            drawn.ListItems.Add(listItem);
            number++;
        }

        return drawn;
    }

    /// The tag of the marker of a list item, so the ink can find it.
    public static readonly object ListMarker = new();

    private static InlineUIContainer Marker(string text, double indent) => new(new TextBlock
    {
        Text = text,
        Width = indent,
        Padding = new Thickness(0, 0, MarkerGap, 0),
        TextAlignment = TextAlignment.Right,
        FontFamily = Tokens.Get<FontFamily>("Sans"),
        FontSize = Tokens.Get<double>("TextName"),
        Foreground = Tokens.Brush("Ink"),
    })
    {
        BaselineAlignment = BaselineAlignment.Baseline,
        Tag = ListMarker,
    };

    private static double MarkerWidth(int number) => new FormattedText(
        $"{number}.",
        System.Globalization.CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        new Typeface(Tokens.Get<FontFamily>("Sans"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
        Tokens.Get<double>("TextName"),
        Brushes.Black,
        pixelsPerDip: 1).WidthIncludingTrailingWhitespace;

    private static Section Quote(MarkdownBlock.Quote quote)
    {
        var section = new Section
        {
            BorderBrush = Tokens.Brush("InputLine"),
            BorderThickness = new Thickness(2, 0, 0, 0),
            Padding = new Thickness(12, 0, 0, 0),
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = Tokens.Brush("Muted"),
        };
        foreach (var block in quote.Blocks)
        {
            section.Blocks.Add(Block(block));
        }

        return section;
    }

    private static Paragraph CodeAsText(MarkdownBlock.Code code) => new(new Run(code.Text) { Tag = code })
    {
        FontFamily = Tokens.Get<FontFamily>("Mono"),
        FontSize = Tokens.Get<double>("TextSmall"),
        Margin = new Thickness(0, 0, 0, 10),
    };

    private static IEnumerable<Inline> Inlines(IReadOnlyList<MarkdownInline> inlines)
    {
        foreach (var inline in inlines)
        {
            yield return inline switch
            {
                MarkdownInline.Text text => new Run(text.Value) { Tag = text },
                MarkdownInline.Code code => new Run(code.Value)
                {
                    Tag = code,
                    FontFamily = Tokens.Get<FontFamily>("Mono"),
                    FontSize = Tokens.Get<double>("TextSmall"),
                    Background = Tokens.Brush("Raised"),
                },
                MarkdownInline.Strong strong => Wrapped(new Bold(), strong.Inlines),
                MarkdownInline.Emphasis emphasis => Wrapped(new Italic(), emphasis.Inlines),
                MarkdownInline.Strike strike => Wrapped(new Span { TextDecorations = TextDecorations.Strikethrough }, strike.Inlines),
                MarkdownInline.Link link => Link(link),
                _ => new Run(),
            };
        }
    }

    private static Span Wrapped(Span span, IReadOnlyList<MarkdownInline> inlines)
    {
        span.Inlines.AddRange(Inlines(inlines));
        return span;
    }

    /// A web link opens the browser; "file:line" opens the file in VS Code at that line. Anything
    /// else stays text.
    private static Inline Link(MarkdownInline.Link link)
    {
        var target = LinkTarget.Of(link.Target);
        if (target is LinkTarget.None)
        {
            return Wrapped(new Span(), link.Inlines);
        }

        var hyperlink = new Hyperlink
        {
            Foreground = Tokens.Brush("Ink"),
            TextDecorations = TextDecorations.Underline,
            Cursor = Cursors.Hand,
            ToolTip = link.Target,
        };
        hyperlink.Inlines.AddRange(Inlines(link.Inlines));
        hyperlink.Click += (_, _) => Open(target);
        return hyperlink;
    }

    private static void Open(LinkTarget target)
    {
        try
        {
            switch (target)
            {
                case LinkTarget.Web web:
                    Process.Start(new ProcessStartInfo(web.Url) { UseShellExecute = true });
                    break;
                case LinkTarget.File file:
                    var place = file.Link.Line is { } line ? $"{file.Link.Path}:{line}" : file.Link.Path;
                    Process.Start(new ProcessStartInfo("code", ["-g", place]) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
                    break;
            }
        }
        catch (Win32Exception)
        {
            // No browser or no VS Code: the link stays a link and nothing happens.
        }
    }

    // ---- code ----

    private static UIElement CodeBlock(string language, string text)
    {
        var body = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            IsReadOnlyCaretVisible = false,
            // Long lines wrap: the scroll bars of the window are vertical only.
            TextWrapping = TextWrapping.Wrap,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 0, 12, 10),
            FontFamily = Tokens.Get<FontFamily>("Mono"),
            FontSize = Tokens.Get<double>("TextSmall"),
            Foreground = Tokens.Brush("Ink"),
            SelectionBrush = Tokens.Brush("Muted"),
        };

        var copy = CopyButton.For(() => text);

        var head = new DockPanel { Margin = new Thickness(12, 2, 2, 0) };
        DockPanel.SetDock(copy, Dock.Right);
        head.Children.Add(copy);
        head.Children.Add(TerminalPanel.Mono(language.Length > 0 ? language : " ", "Muted"));

        var column = new DockPanel();
        DockPanel.SetDock(head, Dock.Top);
        column.Children.Add(head);
        column.Children.Add(body);

        return new Border
        {
            Background = Tokens.Brush("Surface"),
            BorderBrush = Tokens.Brush("Hairline"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 2, 0, 12),
            Child = column,
        };
    }

    // ---- tables ----

    private static UIElement Table(MarkdownBlock.Table table)
    {
        var grid = new Grid();
        for (var column = 0; column < table.Columns.Count; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        void AddRow(IReadOnlyList<IReadOnlyList<MarkdownInline>> cells, bool header)
        {
            var row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var column = 0; column < table.Columns.Count; column++)
            {
                var text = new TextBlock
                {
                    FontFamily = Tokens.Get<FontFamily>("Sans"),
                    FontSize = Tokens.Get<double>("TextNumber"),
                    FontWeight = header ? FontWeights.Medium : FontWeights.Normal,
                    Foreground = Tokens.Brush("Ink"),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 360,
                    TextAlignment = table.Columns[column] switch
                    {
                        ColumnAlign.Center => TextAlignment.Center,
                        ColumnAlign.Right => TextAlignment.Right,
                        _ => TextAlignment.Left,
                    },
                };
                if (column < cells.Count)
                {
                    text.Inlines.AddRange(Inlines(cells[column]));
                }

                var cell = new Border
                {
                    Padding = new Thickness(column == 0 ? 0 : 16, 6, 0, 6),
                    BorderBrush = Tokens.Brush("Hairline"),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = text,
                };
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                grid.Children.Add(cell);
            }
        }

        AddRow(table.Header, header: true);
        foreach (var row in table.Rows)
        {
            AddRow(row, header: false);
        }

        grid.Margin = new Thickness(0, 2, 0, 12);
        grid.HorizontalAlignment = HorizontalAlignment.Left;
        return grid;
    }
}
