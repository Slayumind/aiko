using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// The arrows in the field, read from spec/cases/field-history, the same table the Swift tests read.
public class FieldHistoryCaseTests
{
    [Fact]
    public void Every_walk_through_what_was_sent_shows_the_same()
    {
        foreach (var row in SpecCases.Json("field-history").GetProperty("walks").EnumerateArray())
        {
            var name = row.GetProperty("name").GetString();
            List<string> sent = [.. row.GetProperty("sent").EnumerateArray().Select(message => message.GetString()!)];
            var field = row.GetProperty("field").GetString()!;
            var walk = FieldWalk.None;
            var at = 0;
            foreach (var step in row.GetProperty("steps").EnumerateArray())
            {
                at++;
                var next = FieldHistory.Step(sent, walk, field, step.GetProperty("key").GetString() == "up" ? -1 : +1);
                walk = next.Walk;
                field = next.Field ?? field;
                Assert.True(step.GetProperty("taken").GetBoolean() == next.Field is not null, $"{name}, step {at}: taken");
                Assert.True(step.GetProperty("field").GetString() == field, $"{name}, step {at}: {field}");
            }
        }
    }
}
