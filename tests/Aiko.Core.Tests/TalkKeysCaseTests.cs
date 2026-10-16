using System.Text.Json;
using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// Enter and Esc of the field read from spec/cases, the same table the Swift tests read.
public class TalkKeysCaseTests
{
    [Fact]
    public void Every_key_does_what_the_table_says()
    {
        foreach (var row in SpecCases.Json("talk-keys").GetProperty("keys").EnumerateArray())
        {
            var state = new TalkKeyState(
                row.GetProperty("card").GetBoolean(),
                row.GetProperty("empty").GetBoolean(),
                row.GetProperty("busy").GetBoolean(),
                row.GetProperty("dictating").GetBoolean(),
                row.GetProperty("voice").GetBoolean());

            var action = TalkKeys.For(Parsed<TalkKey>(row.GetProperty("key")), state);

            Assert.True(
                Parsed<TalkKeyAction>(row.GetProperty("action")) == action,
                $"{row.GetProperty("name").GetString()}: {action}");
        }
    }

    [Fact]
    public void The_keys_answer_the_card_of_the_table()
    {
        foreach (var row in SpecCases.Json("talk-keys").GetProperty("cards").EnumerateArray())
        {
            List<FeedItem> feed = [.. row.GetProperty("feed").EnumerateArray().Select((item, at) => Card(item, at))];
            var answers = row.GetProperty("answers");

            var card = TalkKeys.Card(feed);

            Assert.True(
                ReferenceEquals(answers.ValueKind == JsonValueKind.Null ? null : feed[answers.GetInt32()], card),
                row.GetProperty("name").GetString());
        }
    }

    private static FeedItem.Card Card(JsonElement item, int at)
    {
        var line = JsonSerializer.Serialize(new
        {
            type = "control_request",
            request_id = $"req-{at}",
            request = new { subtype = "can_use_tool", tool_name = item.GetProperty("tool").GetString(), input = new { }, tool_use_id = $"toolu_{at}" },
        });
        var request = RequestCard.From((StreamEvent.Request)StreamEvent.FromLine(line));
        return new FeedItem.Card(at, request, Parsed<RequestState>(item.GetProperty("state")));
    }

    /// "cancel-voice" is CancelVoice.
    private static T Parsed<T>(JsonElement name)
        where T : struct, Enum =>
        Enum.Parse<T>(name.GetString()!.Replace("-", ""), ignoreCase: true);
}
