namespace Aiko.Core.Tests;

/// The query of the daily version check carries what PRIVACY.md lists, and nothing with counting off.
public class CountQueryTests
{
    [Fact]
    public void The_query_carries_only_what_privacy_lists()
    {
        foreach (var row in SpecCases.Json("count-query").GetProperty("rows").EnumerateArray())
        {
            var day = row.GetProperty("day").ValueKind == System.Text.Json.JsonValueKind.Null ? null : row.GetProperty("day").GetString();
            var query = CountQuery.For(
                row.GetProperty("version").GetString()!,
                row.GetProperty("system").GetString()!,
                day,
                row.GetProperty("persona").GetBoolean(),
                row.GetProperty("week").GetBoolean(),
                row.GetProperty("month").GetBoolean());

            Assert.Equal(row.GetProperty("query").GetString(), query);
        }
    }
}
