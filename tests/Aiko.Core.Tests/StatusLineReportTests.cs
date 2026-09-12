namespace Aiko.Core.Tests;

public class StatusLineReportTests
{
    // Shortened copy of a real status line payload captured during the spike.
    private const string RealPayload = """
        {
          "session_id": "00000000-0000-0000-0000-000000000000",
          "cwd": "C:\\work",
          "model": { "id": "claude-opus-5", "display_name": "Opus 5" },
          "rate_limits": {
            "five_hour": { "used_percentage": 28.000000000000004, "resets_at": 1789170600 },
            "seven_day": { "used_percentage": 15, "resets_at": 1789506000 }
          }
        }
        """;

    [Fact]
    public void Reads_both_windows_from_a_real_payload()
    {
        var report = StatusLineReport.FromJson(RealPayload);

        Assert.True(report.HasData);
        Assert.Equal(2, report.Windows.Count);
        Assert.Equal(28, report.Find(LimitKind.FiveHour)!.Value.Percent);
        Assert.Equal(15, report.Find(LimitKind.SevenDay)!.Value.Percent);
    }

    [Fact]
    public void Reads_the_reset_time_as_unix_seconds()
    {
        var report = StatusLineReport.FromJson(RealPayload);

        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(1789170600),
            report.Find(LimitKind.FiveHour)!.Value.ResetsAt);
    }

    [Fact]
    public void Rounds_a_percentage_to_the_nearest_whole_number()
    {
        var report = StatusLineReport.FromJson("""
            { "rate_limits": { "five_hour": { "used_percentage": 28.6, "resets_at": 1789170600 } } }
            """);

        Assert.Equal(29, report.Find(LimitKind.FiveHour)!.Value.Percent);
    }

    [Fact]
    public void Clamps_a_percentage_above_a_hundred()
    {
        var report = StatusLineReport.FromJson("""
            { "rate_limits": { "five_hour": { "used_percentage": 120, "resets_at": 1789170600 } } }
            """);

        Assert.Equal(100, report.Find(LimitKind.FiveHour)!.Value.Percent);
    }

    [Fact]
    public void A_window_without_a_reset_time_is_skipped()
    {
        var report = StatusLineReport.FromJson("""
            { "rate_limits": { "five_hour": { "used_percentage": 34 } } }
            """);

        Assert.False(report.HasData);
        Assert.Null(report.Find(LimitKind.FiveHour));
    }

    [Fact]
    public void A_payload_without_rate_limits_has_no_data()
    {
        var report = StatusLineReport.FromJson("""
            { "model": { "display_name": "Opus 5" } }
            """);

        Assert.False(report.HasData);
    }

    [Fact]
    public void Unknown_fields_do_not_break_the_parser()
    {
        var report = StatusLineReport.FromJson("""
            {
              "rate_limits": {
                "five_hour": { "used_percentage": 34, "resets_at": 1789170600, "locked_reason": null },
                "seven_day_omelette": { "used_percentage": 5 }
              }
            }
            """);

        Assert.Single(report.Windows);
        Assert.Equal(34, report.Find(LimitKind.FiveHour)!.Value.Percent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"rate_limits\": \"nonsense\" }")]
    public void Broken_input_reads_as_no_data_and_never_throws(string json)
    {
        var report = StatusLineReport.FromJson(json);

        Assert.False(report.HasData);
    }
}
