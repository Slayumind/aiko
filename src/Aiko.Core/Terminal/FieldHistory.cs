namespace Aiko.Core.Terminal;

/// Where the arrows stand in what was sent from the field (D-341): At is the message shown, or
/// -1 while the field holds what the person is typing; Draft is that text, kept while the arrows
/// walk.
public sealed record FieldWalk(int At, string Draft)
{
    public static readonly FieldWalk None = new(-1, "");

    public bool Walking => At >= 0;
}

/// The field and the walk after one press of an arrow. Field is null when the arrow is not the
/// walk's: it moves the caret as usual.
public sealed record FieldStep(FieldWalk Walk, string? Field);

/// Up and Down through the messages sent from the field, as in a console: up is older, down is
/// newer, and past the newest the field gets back what was being typed.
public static class FieldHistory
{
    public static FieldStep Step(IReadOnlyList<string> sent, FieldWalk walk, string field, int step)
    {
        if (sent.Count == 0 || (!walk.Walking && step > 0))
        {
            return new FieldStep(walk, null);
        }

        var start = walk.Walking ? walk : new FieldWalk(sent.Count, field);
        var next = start.At + step;
        if (next < 0)
        {
            // Up on the oldest stays on it, and the arrow is still the walk's.
            return new FieldStep(start, sent[0]);
        }

        return next >= sent.Count
            ? new FieldStep(FieldWalk.None, start.Draft)
            : new FieldStep(start with { At = next }, sent[next]);
    }

    /// What the field holds as its own text: the draft while the arrows show an old message.
    public static string Draft(FieldWalk walk, string field) => walk.Walking ? walk.Draft : field;
}
