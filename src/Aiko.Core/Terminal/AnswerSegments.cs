namespace Aiko.Core.Terminal;

/// A part of an answer that is drawn as one piece and selected as one piece.
public abstract record AnswerSegment
{
    /// Paragraphs, headings, lists, quotes and rules that follow each other.
    public sealed record Prose(IReadOnlyList<MarkdownBlock> Blocks) : AnswerSegment;

    /// A block of code, which has its own copy button.
    public sealed record Code(string Language, string Text) : AnswerSegment;

    public sealed record Table(MarkdownBlock.Table Value) : AnswerSegment;
}

/// Cuts an answer into the pieces the window draws (D-304).
///
/// Text is selected inside one piece. Code and tables are pieces of their own, because each is
/// drawn by its own control; everything between them is one piece of prose.
public static class AnswerSegments
{
    public static IReadOnlyList<AnswerSegment> Of(IReadOnlyList<MarkdownBlock> blocks)
    {
        var segments = new List<AnswerSegment>();
        var prose = new List<MarkdownBlock>();

        void EndProse()
        {
            if (prose.Count > 0)
            {
                segments.Add(new AnswerSegment.Prose([.. prose]));
                prose.Clear();
            }
        }

        foreach (var block in blocks)
        {
            switch (block)
            {
                case MarkdownBlock.Code code:
                    EndProse();
                    segments.Add(new AnswerSegment.Code(code.Language, code.Text));
                    break;
                case MarkdownBlock.Table table:
                    EndProse();
                    segments.Add(new AnswerSegment.Table(table));
                    break;
                default:
                    prose.Add(block);
                    break;
            }
        }

        EndProse();
        return segments;
    }
}
