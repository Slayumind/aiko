using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// The end of a streaming answer made whole, read from spec/cases, the same table the Swift tests read.
public class MarkdownTailCaseTests
{
    [Fact]
    public void Every_tail_is_closed_as_the_table_says()
    {
        foreach (var row in SpecCases.Json("markdown-tail").GetProperty("tails").EnumerateArray())
        {
            var closed = MarkdownTail.Closed(row.GetProperty("text").GetString()!);
            Assert.True(row.GetProperty("closed").GetString() == closed, $"{row.GetProperty("name").GetString()}: {closed}");
        }
    }
}
