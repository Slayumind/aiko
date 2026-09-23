namespace Aiko.Core.Tests;

public class LimitSnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static LimitSnapshot SnapshotWith(params LimitWindow[] windows) =>
        new("Personal", LimitSource.StatusLine, Now, windows);

    [Fact]
    public void Fresh_numbers_are_live()
    {
        var snapshot = SnapshotWith(new LimitWindow(LimitKind.FiveHour, 34, Now.AddHours(3)));

        Assert.Equal(DataFreshness.Live, snapshot.FreshnessAt(Now.AddMinutes(10)));
    }

    [Fact]
    public void Numbers_older_than_the_limit_are_stale()
    {
        var snapshot = SnapshotWith(new LimitWindow(LimitKind.FiveHour, 34, Now.AddHours(3)));

        Assert.Equal(DataFreshness.Stale, snapshot.FreshnessAt(Now.AddMinutes(16)));
    }

    [Fact]
    public void An_empty_snapshot_has_no_data_at_all()
    {
        var snapshot = LimitSnapshot.NoData("Work");

        Assert.False(snapshot.HasData);
        Assert.Equal(DataFreshness.None, snapshot.FreshnessAt(Now));
        Assert.Null(snapshot.StatusAt(Now, LimitKind.FiveHour));
    }

    [Fact]
    public void A_window_reports_its_percentage_and_countdown()
    {
        var snapshot = SnapshotWith(new LimitWindow(LimitKind.FiveHour, 34, Now.AddHours(3).AddMinutes(34)));

        var status = snapshot.StatusAt(Now, LimitKind.FiveHour)!.Value;

        Assert.Equal(34, status.Percent);
        Assert.False(status.IsReset);
        Assert.Equal(3, status.Countdown.Hours);
        Assert.Equal(34, status.Countdown.Minutes);
    }

    [Fact]
    public void A_window_whose_reset_time_has_passed_reads_as_zero()
    {
        var snapshot = SnapshotWith(new LimitWindow(LimitKind.FiveHour, 88, Now.AddMinutes(-1)));

        var status = snapshot.StatusAt(Now, LimitKind.FiveHour)!.Value;

        Assert.True(status.IsReset);
        Assert.Equal(0, status.Percent);
    }

    [Fact]
    public void Stale_data_still_shows_its_numbers()
    {
        // Old numbers stay true while nobody works: this is why we show them instead of dashes.
        var snapshot = SnapshotWith(new LimitWindow(LimitKind.SevenDay, 61, Now.AddDays(4)));

        var status = snapshot.StatusAt(Now.AddHours(5), LimitKind.SevenDay)!.Value;

        Assert.Equal(DataFreshness.Stale, snapshot.FreshnessAt(Now.AddHours(5)));
        Assert.Equal(61, status.Percent);
    }

    [Fact]
    public void A_report_without_data_becomes_an_empty_snapshot()
    {
        var snapshot = LimitSnapshot.FromStatusLine("Personal", Now, StatusLineReport.Empty);

        Assert.False(snapshot.HasData);
    }

    [Fact]
    public void A_report_with_data_keeps_the_source_and_the_time()
    {
        var report = StatusLineReport.FromJson("""
            { "rate_limits": { "five_hour": { "used_percentage": 34, "resets_at": 1789170600 } } }
            """);

        var snapshot = LimitSnapshot.FromStatusLine("Personal", Now, report);

        Assert.True(snapshot.HasData);
        Assert.Equal(LimitSource.StatusLine, snapshot.Source);
        Assert.Equal(Now, snapshot.ReceivedAt);
    }

    private static LimitSnapshot FromApi(DateTimeOffset receivedAt) =>
        new("Personal", LimitSource.DirectMode, receivedAt, [new LimitWindow(LimitKind.SevenDay, 40, Now.AddDays(3))])
        {
            Model = new ModelLimit("Fable", 62, Now.AddDays(3)),
        };

    private static LimitSnapshot FromStatusLine(DateTimeOffset receivedAt) =>
        new("Personal", LimitSource.StatusLine, receivedAt, [new LimitWindow(LimitKind.SevenDay, 41, Now.AddDays(3))]);

    [Fact]
    public void A_fresher_status_line_keeps_the_model_limit_from_direct_mode()
    {
        // The status line writes on every reply and never carries the model limit. Before this,
        // each reply hid the model row until the next direct answer.
        var merged = LimitSnapshot.Newer(FromStatusLine(Now.AddMinutes(1)), FromApi(Now));

        Assert.Equal(LimitSource.StatusLine, merged.Source);
        Assert.Equal(41, merged.StatusAt(Now, LimitKind.SevenDay)!.Value.Percent);
        Assert.Equal(new ModelLimit("Fable", 62, Now.AddDays(3)), merged.Model);
    }

    [Fact]
    public void A_fresher_direct_answer_wins_with_its_own_model_limit()
    {
        var merged = LimitSnapshot.Newer(FromStatusLine(Now), FromApi(Now.AddMinutes(1)));

        Assert.Equal(LimitSource.DirectMode, merged.Source);
        Assert.Equal(62, merged.Model!.Value.Percent);
    }

    [Fact]
    public void The_order_of_the_two_sources_does_not_matter()
    {
        var statusLine = FromStatusLine(Now.AddMinutes(1));
        var direct = FromApi(Now);

        Assert.Equal(LimitSnapshot.Newer(statusLine, direct), LimitSnapshot.Newer(direct, statusLine));
    }
}
