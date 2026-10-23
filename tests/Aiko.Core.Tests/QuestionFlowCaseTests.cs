using System.Text.Json;
using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// The card of questions read from spec/cases, the same table the Swift tests read.
public class QuestionFlowCaseTests
{
    [Fact]
    public void Every_flow_ends_where_the_table_says()
    {
        var cases = SpecCases.Json("question-card");
        foreach (var row in cases.GetProperty("flows").EnumerateArray())
        {
            var name = row.GetProperty("name").GetString();
            var step = new QuestionStep(QuestionFlow.Start(Questions(cases, row.GetProperty("questions").GetString()!)), QuestionOutcome.None);
            foreach (var action in row.GetProperty("actions").EnumerateArray())
            {
                step = Act(step.Flow, action);
            }

            var flow = step.Flow;
            if (row.TryGetProperty("at", out var at))
            {
                Assert.True(at.GetInt32() == flow.At, $"{name}: at {flow.At}");
            }

            if (row.TryGetProperty("focus", out var focus))
            {
                Assert.True(focus.GetInt32() == flow.Focus, $"{name}: focus {flow.Focus}");
            }

            if (row.TryGetProperty("outcome", out var outcome))
            {
                Assert.True(Enum.Parse<QuestionOutcome>(outcome.GetString()!, ignoreCase: true) == step.Outcome, $"{name}: {step.Outcome}");
            }

            if (row.TryGetProperty("marks", out var marks))
            {
                var expected = marks.EnumerateArray().Select(mark => Enum.Parse<QuestionMark>(mark.GetString()!, ignoreCase: true));
                Assert.True(expected.SequenceEqual(Enumerable.Range(0, flow.Questions.Count).Select(flow.Mark)), $"{name}: marks");
            }

            if (row.TryGetProperty("answers", out var answers))
            {
                var expected = answers.EnumerateArray().Select(list => string.Join("|", list.EnumerateArray().Select(a => a.GetString())));
                var actual = flow.Answers().Select(list => string.Join("|", list));
                Assert.True(expected.SequenceEqual(actual), $"{name}: {string.Join(" / ", actual)}");
            }

            if (row.TryGetProperty("preview", out var preview))
            {
                Assert.True(preview.ValueKind == JsonValueKind.Null ? flow.Preview is null : preview.GetString() == flow.Preview, $"{name}: preview {flow.Preview}");
            }
        }
    }

    private static QuestionStep Act(QuestionFlow flow, JsonElement action)
    {
        if (action.TryGetProperty("key", out var key))
        {
            return flow.Press(Enum.Parse<QuestionKey>(key.GetString()!, ignoreCase: true));
        }

        if (action.TryGetProperty("number", out var number))
        {
            return flow.Number(number.GetInt32());
        }

        if (action.TryGetProperty("click", out var click))
        {
            return flow.Click(click.GetInt32());
        }

        if (action.TryGetProperty("write", out var write))
        {
            return new(flow.Write(write.GetString()!), QuestionOutcome.None);
        }

        if (action.TryGetProperty("confirm", out _))
        {
            return flow.Confirm();
        }

        if (action.TryGetProperty("back", out _))
        {
            return new(flow.Back(), QuestionOutcome.None);
        }

        if (action.TryGetProperty("forward", out _))
        {
            return flow.Forward();
        }

        return new(flow.Show(action.GetProperty("show").GetInt32()), QuestionOutcome.None);
    }

    private static List<CardQuestion> Questions(JsonElement cases, string set) =>
        [.. cases.GetProperty("questions").GetProperty(set).EnumerateArray().Select(question => new CardQuestion(
            question.GetProperty("text").GetString()!,
            question.GetProperty("header").GetString()!,
            question.GetProperty("multi").GetBoolean(),
            [.. question.GetProperty("options").EnumerateArray().Select(option => new QuestionOption(
                option.GetProperty("label").GetString()!,
                option.TryGetProperty("description", out var description) ? description.GetString()! : "",
                option.TryGetProperty("preview", out var preview) ? preview.GetString()! : ""))]))];
}
