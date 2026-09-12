namespace Aiko.Core.Tests;

public class UsageReportTests
{
    // Shortened copy of a real answer captured during the spike, code names included.
    private const string RealAnswer = """
        {
          "five_hour": { "utilization": 34.0, "resets_at": "2026-09-11T15:00:00.123456+00:00",
                         "limit_dollars": null, "locked_reason": null },
          "seven_day": { "utilization": 61.0, "resets_at": "2026-09-15T03:00:00.000000+00:00" },
          "seven_day_opus": null,
          "tangelo": null,
          "nimbus_quill": { "utilization": 2.0, "resets_at": null },
          "limits": [
            { "kind": "session", "percent": 34.0, "resets_at": "2026-09-11T15:00:00.123456+00:00", "scope": null },
            { "kind": "weekly_all", "percent": 61.0, "resets_at": "2026-09-15T03:00:00.000000+00:00", "scope": null },
            { "kind": "weekly_scoped", "percent": 3.0, "resets_at": "2026-09-15T03:00:00+00:00",
              "scope": { "model": { "id": null, "display_name": "Fable" }, "surface": null } }
          ],
          "member_dashboard_available": false
        }
        """;

    [Fact]
    public void Reads_both_windows()
    {
        var report = UsageReport.FromJson(RealAnswer);

        Assert.True(report.HasData);
        Assert.Equal(34, report.Windows.Single(w => w.Kind == LimitKind.FiveHour).Percent);
        Assert.Equal(61, report.Windows.Single(w => w.Kind == LimitKind.SevenDay).Percent);
    }

    [Fact]
    public void Reads_the_model_limit_with_its_name()
    {
        var report = UsageReport.FromJson(RealAnswer);

        Assert.NotNull(report.Model);
        Assert.Equal("Fable", report.Model!.Value.ModelName);
        Assert.Equal(3, report.Model.Value.Percent);
    }

    [Fact]
    public void Reads_a_reset_time_with_and_without_a_fraction()
    {
        var report = UsageReport.FromJson(RealAnswer);

        Assert.Equal(
            new DateTimeOffset(2026, 9, 11, 15, 0, 0, TimeSpan.Zero).AddTicks(1234560),
            report.Windows.Single(w => w.Kind == LimitKind.FiveHour).ResetsAt);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 15, 3, 0, 0, TimeSpan.Zero),
            report.Model!.Value.ResetsAt);
    }

    [Fact]
    public void Unknown_keys_and_nulls_do_not_break_the_parser()
    {
        var report = UsageReport.FromJson("""
            {
              "five_hour": { "utilization": 12.0, "resets_at": "2026-09-11T15:00:00+00:00" },
              "juniper_tide": null,
              "omelette_promotional": { "what": "is this" }
            }
            """);

        Assert.Single(report.Windows);
        Assert.Null(report.Model);
    }

    [Fact]
    public void A_window_without_a_reset_time_is_skipped()
    {
        var report = UsageReport.FromJson("""
            { "five_hour": { "utilization": 12.0, "resets_at": null } }
            """);

        Assert.False(report.HasData);
    }

    [Fact]
    public void Limits_without_a_scoped_weekly_entry_have_no_model_limit()
    {
        var report = UsageReport.FromJson("""
            {
              "five_hour": { "utilization": 5.0, "resets_at": "2026-09-11T15:00:00+00:00" },
              "limits": [ { "kind": "session", "percent": 5.0, "resets_at": "2026-09-11T15:00:00+00:00" } ]
            }
            """);

        Assert.Null(report.Model);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void Broken_input_reads_as_no_data_and_never_throws(string json)
    {
        Assert.False(UsageReport.FromJson(json).HasData);
    }
}
