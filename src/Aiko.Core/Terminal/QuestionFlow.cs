namespace Aiko.Core.Terminal;

/// A key the card of questions takes while it has the keyboard (D-330).
public enum QuestionKey
{
    /// The arrows: up and left go back one row, down and right go on one row, in reading order.
    Previous,
    Next,
    Space,
    Enter,
    Escape,
}

/// What the card does after a key or a click.
public enum QuestionOutcome
{
    None,

    /// Every question has its answer: they go to Claude Code.
    Answer,

    /// The person said no to the questions.
    Deny,

    /// The row of the person's own words was chosen: the field for them takes the keyboard.
    WriteOwn,
}

/// One state of the card and what it asks the window to do.
public sealed record QuestionStep(QuestionFlow Flow, QuestionOutcome Outcome);

/// How a step of the strip at the top of the card looks.
public enum QuestionMark
{
    Ahead,
    Open,
    Answered,
}

/// A card of questions while the person answers it (D-330, AIKO-327): one question at a time, the
/// strip of steps above it, Back and Next below. Every question has one more row after its options,
/// the person's own words ("Other"), as in the console of Claude Code.
///
/// The record never changes; each key or click gives the next one.
public sealed record QuestionFlow
{
    private QuestionFlow(
        IReadOnlyList<CardQuestion> questions,
        int at,
        int focus,
        IReadOnlyList<IReadOnlyList<int>> picked,
        IReadOnlyList<string> own)
    {
        Questions = questions;
        At = at;
        Focus = focus;
        Picked = picked;
        Own = own;
    }

    public IReadOnlyList<CardQuestion> Questions { get; private init; }

    /// The open question.
    public int At { get; private init; }

    /// The row the keyboard stands on in the open question. Rows are the options, then Other.
    public int Focus { get; private init; }

    /// The rows picked in each question.
    public IReadOnlyList<IReadOnlyList<int>> Picked { get; private init; }

    /// The person's own words in each question.
    public IReadOnlyList<string> Own { get; private init; }

    public static QuestionFlow Start(IReadOnlyList<CardQuestion> questions) => new(
        questions,
        0,
        0,
        [.. questions.Select(_ => (IReadOnlyList<int>)[])],
        [.. questions.Select(_ => "")]);

    public CardQuestion Open => Questions[At];

    /// The row of the person's own words in the open question.
    public int OwnRow => Open.Options.Count;

    public bool IsLast => At == Questions.Count - 1;

    /// The preview of the focused option, or of the picked one when the keyboard is on Other.
    public string? Preview
    {
        get
        {
            var options = Open.Options;
            var row = Focus < options.Count ? Focus : Picked[At].FirstOrDefault(pick => pick < options.Count, -1);
            return row >= 0 && options[row].Preview.Length > 0 ? options[row].Preview : null;
        }
    }

    public bool IsPicked(int row) => Picked[At].Contains(row);

    /// A question is answered when a row is picked, and Other has words when it is picked.
    public bool IsAnswered(int question) =>
        Picked[question].Count > 0
        && (!Picked[question].Contains(Questions[question].Options.Count) || Own[question].Trim().Length > 0);

    public QuestionMark Mark(int question) =>
        question == At ? QuestionMark.Open : IsAnswered(question) ? QuestionMark.Answered : QuestionMark.Ahead;

    /// The labels picked in each question, with the person's own words in the place of Other.
    public IReadOnlyList<IReadOnlyList<string>> Answers() =>
        [.. Questions.Select((question, at) => (IReadOnlyList<string>)[.. Picked[at]
            .Order()
            .Select(row => row < question.Options.Count ? question.Options[row].Label : Own[at].Trim())
            .Where(answer => answer.Length > 0)])];

    public QuestionStep Press(QuestionKey key) => key switch
    {
        QuestionKey.Previous => new(this with { Focus = Math.Max(0, Focus - 1) }, QuestionOutcome.None),
        QuestionKey.Next => new(this with { Focus = Math.Min(OwnRow, Focus + 1) }, QuestionOutcome.None),
        QuestionKey.Space => Choose(Focus),
        QuestionKey.Enter => Enter(),
        QuestionKey.Escape => new(this, QuestionOutcome.Deny),
        _ => new(this, QuestionOutcome.None),
    };

    /// A digit from 1: the row with that number, as a click on it.
    public QuestionStep Number(int number) =>
        number >= 1 && number <= OwnRow + 1 ? Click(number - 1) : new(this, QuestionOutcome.None);

    /// A click on a row. One answer goes on to the next question at once, as in the console; with
    /// several answers a click only ticks the row.
    public QuestionStep Click(int row)
    {
        var chosen = Choose(row);
        if (chosen.Outcome != QuestionOutcome.None || Open.MultiSelect)
        {
            return chosen;
        }

        return chosen.Flow.Forward();
    }

    /// The person's own words in the open question. Words pick Other; no words leave it as it was.
    public QuestionFlow Write(string words)
    {
        var flow = this with { Own = Replace(Own, At, words) };
        if (words.Trim().Length == 0 || IsPicked(OwnRow))
        {
            return flow;
        }

        return flow with { Picked = Replace(Picked, At, Open.MultiSelect ? [.. Picked[At], OwnRow] : [OwnRow]) };
    }

    /// Enter in the field of the person's own words: the words stand, and the card goes on.
    public QuestionStep Confirm() => IsAnswered(At) ? Forward() : new(this, QuestionOutcome.None);

    /// Back: the question before, with the keyboard on its first picked row.
    public QuestionFlow Back() => At == 0 ? this : Go(At - 1);

    /// Next, or Answer on the last question. A question without an answer keeps the card on it.
    public QuestionStep Forward()
    {
        if (!IsAnswered(At))
        {
            return new(this, QuestionOutcome.None);
        }

        if (!IsLast)
        {
            return new(Go(At + 1), QuestionOutcome.None);
        }

        var open = Enumerable.Range(0, Questions.Count).FirstOrDefault(question => !IsAnswered(question), -1);
        return open < 0 ? new(this, QuestionOutcome.Answer) : new(Go(open), QuestionOutcome.None);
    }

    /// A click on a step of the strip: that question.
    public QuestionFlow Show(int question) => question >= 0 && question < Questions.Count ? Go(question) : this;

    private QuestionStep Enter()
    {
        // With one answer Enter takes the row the keyboard is on, as in the console.
        if (!Open.MultiSelect && !IsPicked(Focus))
        {
            return Click(Focus);
        }

        return Forward();
    }

    private QuestionStep Choose(int row)
    {
        if (row < 0 || row > OwnRow)
        {
            return new(this, QuestionOutcome.None);
        }

        var focused = this with { Focus = row };
        if (row == OwnRow && Own[At].Trim().Length == 0)
        {
            return new(focused, QuestionOutcome.WriteOwn);
        }

        IReadOnlyList<int> picks = Open.MultiSelect
            ? IsPicked(row) ? [.. Picked[At].Where(pick => pick != row)] : [.. Picked[At], row]
            : [row];
        return new(focused with { Picked = Replace(Picked, At, picks) }, QuestionOutcome.None);
    }

    private QuestionFlow Go(int question) => this with
    {
        At = question,
        Focus = Picked[question].Count > 0 ? Picked[question].Min() : 0,
    };

    private static IReadOnlyList<T> Replace<T>(IReadOnlyList<T> list, int at, T value) =>
        [.. list.Select((item, index) => index == at ? value : item)];
}
