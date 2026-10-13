namespace Aiko.Core.Terminal;

/// What the second line of a session in the list is about.
public enum GlanceLine
{
    /// Nothing to say.
    None,

    /// A permission is asked for: Kind, Label and Text name the step.
    Request,

    /// A question with choices waits for an answer. Text is its header.
    Question,

    /// A plan waits to be approved.
    Plan,

    /// The step that runs now.
    Step,

    /// The first line of the last answer.
    Answer,

    /// Claude Code ended in the middle of this step.
    StoppedAt,

    /// The environment has no sign-in.
    SignIn,
}

/// One conversation as the list of sessions sees it: what it does and one line about it (D-299).
///
/// The words of the line are strings of the interface; here are only its parts.
public sealed record TalkGlance(TalkActivity Activity, GlanceLine Line, StepKind Kind, string Label, string Text)
{
    /// The longest text of a line, in UTF-16 units. A row shows one line and cuts the rest itself.
    public const int MaxText = 120;

    public static TalkGlance Of(Conversation talk)
    {
        var activity = talk.Activity;
        return activity switch
        {
            TalkActivity.SignedOut => Plain(activity, GlanceLine.SignIn),
            TalkActivity.Failed => LastStep(talk, _ => true) is { } step
                ? About(activity, GlanceLine.StoppedAt, step.Line)
                : Plain(activity, GlanceLine.None),
            TalkActivity.Waiting => Asked(talk, activity),
            TalkActivity.Working => LastStep(talk, step => step.State == StepState.Running) is { } step
                ? About(activity, GlanceLine.Step, step.Line)
                : Plain(activity, GlanceLine.None),
            _ => Said(talk, activity),
        };
    }

    private static TalkGlance Asked(Conversation talk, TalkActivity activity)
    {
        var card = talk.Items.OfType<FeedItem.Card>().Last(item => item.State == RequestState.Open).Request;
        return card.Kind switch
        {
            CardKind.Question => new TalkGlance(activity, GlanceLine.Question, StepKind.Other, "", Cut(card.Questions.FirstOrDefault()?.Header ?? "")),
            CardKind.Plan => Plain(activity, GlanceLine.Plan),
            _ => About(activity, GlanceLine.Request, card.Step),
        };
    }

    private static TalkGlance Said(Conversation talk, TalkActivity activity)
    {
        var answer = talk.Items.OfType<FeedItem.Answer>().LastOrDefault();
        var line = answer is null ? "" : StepLine.Lines(answer.Text).Select(text => text.Trim()).FirstOrDefault(text => text.Length > 0) ?? "";
        return line.Length > 0
            ? new TalkGlance(activity, GlanceLine.Answer, StepKind.Other, "", Cut(line))
            : Plain(activity, GlanceLine.None);
    }

    private static FeedItem.Step? LastStep(Conversation talk, Func<FeedItem.Step, bool> wanted) =>
        talk.Items.OfType<FeedItem.Step>().LastOrDefault(step => !step.Line.Hidden && wanted(step));

    private static TalkGlance About(TalkActivity activity, GlanceLine line, StepLine step) =>
        new(activity, line, step.Kind, step.Label, Cut(step.Subject));

    private static TalkGlance Plain(TalkActivity activity, GlanceLine line) => new(activity, line, StepKind.Other, "", "");

    /// Never cuts a letter of two units in half.
    private static string Cut(string text)
    {
        if (text.Length <= MaxText)
        {
            return text;
        }

        var end = char.IsHighSurrogate(text[MaxText - 1]) ? MaxText - 1 : MaxText;
        return text[..end];
    }
}
