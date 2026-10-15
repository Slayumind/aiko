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
            column.Children.Add(segment switch
            {
                AnswerSegment.Prose prose => Prose(prose.Blocks),
                AnswerSegment.Code code => CodeBlock(code.Language, code.Text),
                AnswerSegment.Table table => Table(table.Value),
                _ => new Border(),
            });
        }

        return column;
    }

    /// A finished answer with a quiet button under it that copies the whole answer as its
    /// markdown, the way Claude wrote it (D-303).
    public static UIElement WithCopy(string markdown)
    {
        var column = new StackPanel();
        column.Children.Add(For(markdown));

        var copy = TerminalPanel.Flat(Strings.Copy, () => { }, ghost: true);
        copy.Height = 24;
        copy.Margin = new Thickness(-12, -8, 0, 4);
        copy.HorizontalAlignment = HorizontalAlignment.Left;
        copy.Click += (_, _) =>
        {
            Clipboard.SetText(markdown);
            copy.Content = TerminalPanel.Text(Strings.Copied, "TextNumber", "Muted", FontWeights.Normal);
        };
        column.Children.Add(copy);
        return column;
    }

    // ---- prose ----

    private static UIElement Prose(IReadOnlyList<MarkdownBlock> blocks)
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

    private static List List(MarkdownBlock.List list)
    {
        var drawn = new List
        {
            MarkerStyle = list.Ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            StartIndex = Math.Max(1, list.Start),
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(22, 0, 0, 0),
        };
        foreach (var item in list.Items)
        {
            var listItem = new ListItem();
            foreach (var block in item)
            {
                var inner = Block(block);
                inner.Margin = new Thickness(0, 0, 0, 4);
                listItem.Blocks.Add(inner);
            }

            drawn.ListItems.Add(listItem);
        }

        return drawn;
    }

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
            Padding = new Thickness(12, 10, 12, 12),
            FontFamily = Tokens.Get<FontFamily>("Mono"),
            FontSize = Tokens.Get<double>("TextSmall"),
            Foreground = Tokens.Brush("Ink"),
            SelectionBrush = Tokens.Brush("Muted"),
        };

        var copy = TerminalPanel.Flat(Strings.Copy, () => { }, ghost: true);
        copy.Height = 26;
        copy.Click += (_, _) =>
        {
            Clipboard.SetText(text);
            copy.Content = TerminalPanel.Text(Strings.Copied, "TextSmall", "Muted", FontWeights.Normal);
        };

        var head = new DockPanel { Margin = new Thickness(12, 4, 4, 0) };
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
