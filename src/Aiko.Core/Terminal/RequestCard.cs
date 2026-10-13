using System.Text.Json;

namespace Aiko.Core.Terminal;

public enum CardKind
{
    /// May this tool run?
    Permission,

    /// A question with options.
    Question,

    /// A plan to approve.
    Plan,
}

/// What the second button of a permission card does.
public enum SecondChoice
{
    None,

    /// Allow, and stop asking about edits in this session.
    EditsInSession,

    /// Allow, and never ask about this command or this MCP tool in this project again (D-306, D-307).
    AlwaysAllow,
}

public sealed record QuestionOption(string Label, string Description);

public sealed record CardQuestion(string Text, string Header, bool MultiSelect, IReadOnlyList<QuestionOption> Options);

/// What the person chose on a card.
public abstract record CardChoice
{
    /// The first button: yes to this one call, or yes to the plan.
    public sealed record Allow : CardChoice;

    /// The second button of a permission card.
    public sealed record Second : CardChoice;

    /// No. The message goes to Claude; an empty one becomes a plain refusal.
    public sealed record Deny(string Message) : CardChoice;

    /// The answers to the questions, in their order: the labels picked, or the person's own words.
    public sealed record Answers(IReadOnlyList<IReadOnlyList<string>> Picked) : CardChoice;
}

/// A request of Claude Code as a card of the feed: a permission, a question or a plan (D-294).
public sealed record RequestCard(
    CardKind Kind,
    string RequestId,
    string ToolUseId,
    bool FromSubagent,
    StepLine Step,
    SecondChoice Second,
    string RuleText,
    IReadOnlyList<CardQuestion> Questions,
    string Plan,
    StreamEvent.Request Source)
{
    /// What Claude reads when the person says no without a word.
    public const string PlainRefusal = "The user said no.";

    public static RequestCard From(StreamEvent.Request request)
    {
        var step = StepLine.For(request.ToolName, request.Input);
        var kind = request.ToolName switch
        {
            "AskUserQuestion" => CardKind.Question,
            "ExitPlanMode" => CardKind.Plan,
            _ => CardKind.Permission,
        };

        return new RequestCard(
            kind,
            request.RequestId,
            request.ToolUseId,
            request.AgentId is { Length: > 0 },
            step,
            kind == CardKind.Permission ? SecondFor(step, request) : SecondChoice.None,
            request.RuleText,
            kind == CardKind.Question ? QuestionsIn(request.Input) : [],
            kind == CardKind.Plan ? Text(request.Input, "plan") : "",
            request);
    }

    /// The line that answers the request.
    public string LineFor(CardChoice choice)
    {
        switch (choice)
        {
            case CardChoice.Second when Second == SecondChoice.EditsInSession:
                return HostLine.AllowEdits(RequestId, Source.Input);
            case CardChoice.Second when Second == SecondChoice.AlwaysAllow && Source.Rule is { } rule:
                return HostLine.AllowAlways(RequestId, Source.Input, rule);
            case CardChoice.Deny deny:
                var message = deny.Message.Trim();
                return HostLine.Deny(RequestId, message.Length > 0 ? message : PlainRefusal);
            case CardChoice.Answers answers:
                return HostLine.Answer(RequestId, Source.Input, Paired(answers.Picked));
            default:
                return HostLine.Allow(RequestId, Source.Input);
        }
    }

    /// An edit offers "edits in this session"; anything else offers the rule, when there is one.
    /// A command is never answered with the edit mode: that would allow more than the button says.
    private static SecondChoice SecondFor(StepLine step, StreamEvent.Request request)
    {
        if (step.Kind == StepKind.Edit && request.SuggestsAcceptEdits)
        {
            return SecondChoice.EditsInSession;
        }
        return request.Rule is not null ? SecondChoice.AlwaysAllow : SecondChoice.None;
    }

    /// Each answer under the text of its question. Several picks are joined with a comma, and a
    /// question nobody answered is left out.
    private List<KeyValuePair<string, string>> Paired(IReadOnlyList<IReadOnlyList<string>> picked)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        for (var index = 0; index < Questions.Count && index < picked.Count; index++)
        {
            var answer = string.Join(", ", picked[index].Where(pick => pick.Length > 0));
            if (answer.Length > 0)
            {
                pairs.Add(KeyValuePair.Create(Questions[index].Text, answer));
            }
        }
        return pairs;
    }

    private static List<CardQuestion> QuestionsIn(JsonElement input)
    {
        var questions = new List<CardQuestion>();
        foreach (var question in Objects(input, "questions"))
        {
            var options = Objects(question, "options")
                .Select(option => new QuestionOption(Text(option, "label"), Text(option, "description")))
                .Where(option => option.Label.Length > 0)
                .ToList();

            if (Text(question, "question") is { Length: > 0 } text)
            {
                questions.Add(new CardQuestion(
                    text,
                    Text(question, "header"),
                    question.TryGetProperty("multiSelect", out var multi) && multi.ValueKind == JsonValueKind.True,
                    options));
            }
        }
        return questions;
    }

    private static string Text(JsonElement node, string key) =>
        node.ValueKind == JsonValueKind.Object
        && node.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static IEnumerable<JsonElement> Objects(JsonElement node, string key) =>
        node.ValueKind == JsonValueKind.Object
        && node.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object)
            : [];
}
