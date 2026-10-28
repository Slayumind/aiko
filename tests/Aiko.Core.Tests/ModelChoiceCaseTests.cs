using System.Text.Json;
using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// The models a new session offers, read from spec/cases/model-choice, the same table the Swift
/// tests read.
public class ModelChoiceCaseTests
{
    private static readonly JsonElement Cases = SpecCases.Json("model-choice");

    private static readonly IReadOnlyList<ClaudeModel> Named =
        [.. Cases.GetProperty("named").EnumerateArray().Select(model => new ClaudeModel(
            model.GetProperty("value").GetString()!, model.GetProperty("displayName").GetString()!))];

    private static IReadOnlyList<ClaudeModel> OfferedIn(JsonElement row) =>
        NewSessionChoices.Offered(row.GetProperty("fromNamed").GetBoolean() ? Named : []);

    [Fact]
    public void Every_list_of_models_is_offered_the_same()
    {
        foreach (var row in Cases.GetProperty("offered").EnumerateArray())
        {
            var offered = OfferedIn(row);
            var name = row.GetProperty("name").GetString();
            Assert.True(Words(row, "values").SequenceEqual(offered.Select(model => model.Value)), $"{name}: values");
            Assert.True(Words(row, "names").SequenceEqual(offered.Select(model => model.DisplayName)), $"{name}: names");
        }
    }

    [Fact]
    public void Every_click_on_the_model_takes_the_same_next_one()
    {
        foreach (var row in Cases.GetProperty("next").EnumerateArray())
        {
            var model = row.GetProperty("model").GetString();
            var list = OfferedIn(row).Select(offered => offered.Value).ToList();
            Assert.True(
                row.GetProperty("next").GetString() == NewSessionChoices.NextModel(model, list),
                $"{model ?? "null"} from {string.Join(",", list)}");
        }
    }

    [Fact]
    public void Every_new_session_starts_with_the_same_model()
    {
        foreach (var row in Cases.GetProperty("default").EnumerateArray())
        {
            var sessions = TerminalSessions.Empty;
            var at = 0;
            foreach (var session in row.GetProperty("sessions").EnumerateArray())
            {
                at++;
                sessions = sessions.Add(
                    $"s{at}", session.GetProperty("environment").GetString()!, "/work", $"s{at}",
                    session.GetProperty("model").GetString(), PermissionMode.Manual, session.GetProperty("lastUsed").GetInt64());
            }

            var list = OfferedIn(row).Select(offered => offered.Value).ToList();
            Assert.True(
                row.GetProperty("model").GetString() == NewSessionChoices.DefaultModel(sessions, row.GetProperty("environment").GetString()!, list),
                row.GetProperty("name").GetString());
        }
    }

    private static IEnumerable<string> Words(JsonElement row, string key) =>
        row.GetProperty(key).EnumerateArray().Select(word => word.GetString()!);
}
