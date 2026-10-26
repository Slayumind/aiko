namespace Aiko.Core.Terminal;

/// One row of the feed as the window draws it: one item, or a run of steps of one kind.
public abstract record FeedEntry(int Id)
{
    public sealed record One(FeedItem Item) : FeedEntry(Item.Id);

    /// A thought of Claude. It stands open while Claude still works after it and nothing else
    /// came yet; after that it folds into one line that opens with a click.
    public sealed record Thought(FeedItem.Thought Item, bool Open) : FeedEntry(Item.Id);

    /// Steps of one kind in a row are one line: "read ×3 · TalkInput.cs, TalkPanel.cs, +1" (D-342).
    /// The line opens to the steps themselves.
    public sealed record Run(IReadOnlyList<FeedItem.Step> Steps) : FeedEntry(Steps[0].Id)
    {
        /// How many names the line shows before "+N".
        public const int ShownNames = 2;

        public StepKind Kind => Steps[0].Line.Kind;

        /// A failed step marks the whole line; then a step at work; else the run is done.
        public StepState State =>
            Steps.Any(step => step.State == StepState.Failed) ? StepState.Failed
            : Steps.Any(step => step.State == StepState.Running) ? StepState.Running
            : StepState.Done;

        /// The first names of the run, each once, and how many other names there are.
        public (IReadOnlyList<string> Names, int More) Subjects()
        {
            var names = Steps.Select(step => step.Line.Subject).Where(name => name.Length > 0).Distinct().ToList();
            return (names.Take(ShownNames).ToList(), Math.Max(0, names.Count - ShownNames));
        }

        public bool Equals(Run? other) => other is not null && Steps.SequenceEqual(other.Steps);

        public override int GetHashCode() => Steps.Count == 0 ? 0 : HashCode.Combine(Steps[0].Id, Steps.Count);
    }
}

public static class FeedEntries
{
    /// The feed as rows. Reading and searching come in long series, and each step of a series
    /// says little alone, so two or more of one kind in a row become one line. An edit or a
    /// command stays its own line: each one matters.
    /// busy: Claude is at work on this conversation now.
    public static IReadOnlyList<FeedEntry> Of(IReadOnlyList<FeedItem> items, bool busy)
    {
        // Messages that wait for Claude stand under the turn and do not count as what came next.
        var last = items.LastOrDefault(item => item is not FeedItem.OwnerMessage { Queued: true });
        var rows = new List<FeedEntry>();
        var run = new List<FeedItem.Step>();

        void Flush()
        {
            if (run.Count == 1)
            {
                rows.Add(new FeedEntry.One(run[0]));
            }
            else if (run.Count > 1)
            {
                rows.Add(new FeedEntry.Run(run.ToList()));
            }
            run.Clear();
        }

        foreach (var item in items)
        {
            if (item is FeedItem.Step step && Joins(step))
            {
                if (run.Count > 0 && run[0].Line.Kind != step.Line.Kind)
                {
                    Flush();
                }
                run.Add(step);
                continue;
            }

            Flush();
            rows.Add(item is FeedItem.Thought thought
                ? new FeedEntry.Thought(thought, busy && ReferenceEquals(item, last))
                : new FeedEntry.One(item));
        }

        Flush();
        return rows;
    }

    private static bool Joins(FeedItem.Step step) =>
        step.Line.Kind is StepKind.Read or StepKind.Search
        && !step.Line.Hidden && step.Answers.Count == 0 && step.Images.Count == 0;
}

/// A long output in the feed shows its first lines and a button for the rest (D-342, D-343).
public static class LongOutput
{
    /// The lines shown of a long output.
    public const int Shown = 10;

    /// An output this long is shown whole: hiding one line behind a button saves nothing.
    public const int MostWhole = Shown + 1;

    /// The lines to show and how many are hidden behind the button.
    public static (string Text, int Hidden) Cut(string text)
    {
        var lines = StepLine.Lines(text.TrimEnd());
        return lines.Count > MostWhole
            ? (string.Join('\n', lines.Take(Shown)), lines.Count - Shown)
            : (text.TrimEnd(), 0);
    }
}
