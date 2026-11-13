namespace Aiko.Core.Tests;

public class LogTextTests
{
    [Fact]
    public void A_path_in_the_log_keeps_only_its_last_part()
    {
        foreach (var row in SpecCases.Json("log-paths").GetProperty("rows").EnumerateArray())
        {
            Assert.Equal(row.GetProperty("clean").GetString(), LogText.WithoutPaths(row.GetProperty("line").GetString()!));
        }
    }
}
