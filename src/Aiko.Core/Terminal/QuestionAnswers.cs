using System.Text.Json;

namespace Aiko.Core.Terminal;

/// One answered question as the feed shows it after the card: the header of the question, or its
/// text when it has none, and what the person chose.
public sealed record QuestionAnswer(string Header, string Answer);

/// The answers to a card of questions, read from what Claude Code keeps beside the result of the
/// tool: tool_use_result in the stream, toolUseResult in the file of the conversation. Both hold
/// the questions and the answers by the text of each question.
public static class QuestionAnswers
{
    /// The tool behind a card of questions; its step is hidden until it has answers.
    public const string Tool = "AskUserQuestion";

    /// In the order of the questions; an answer to a question not in the list comes after them.
    public static IReadOnlyList<QuestionAnswer> From(JsonElement? result)
    {
        if (result is not { ValueKind: JsonValueKind.Object } root
            || !root.TryGetProperty("answers", out var answers)
            || answers.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var given = answers.EnumerateObject()
            .Where(answer => answer.Value.ValueKind == JsonValueKind.String)
            .Select(answer => (Question: answer.Name, Answer: answer.Value.GetString()!))
            .ToList();
        var list = new List<QuestionAnswer>();
        if (root.TryGetProperty("questions", out var questions) && questions.ValueKind == JsonValueKind.Array)
        {
            foreach (var question in questions.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object))
            {
                var text = Text(question, "question");
                var at = given.FindIndex(answer => answer.Question == text);
                if (at < 0)
                {
                    continue;
                }

                var header = Text(question, "header");
                list.Add(new QuestionAnswer(header.Length > 0 ? header : text, given[at].Answer));
                given.RemoveAt(at);
            }
        }

        list.AddRange(given.Select(answer => new QuestionAnswer(answer.Question, answer.Answer)));
        return list;
    }

    private static string Text(JsonElement node, string key) =>
        node.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
}
