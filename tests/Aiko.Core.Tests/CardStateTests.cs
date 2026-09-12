namespace Aiko.Core.Tests;

public class CardStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    private static LimitSnapshot Snapshot(DateTimeOffset receivedAt, params LimitWindow[] windows) =>
        new("Personal", LimitSource.StatusLine, receivedAt, windows);

    [Theory]
    [InlineData(0, LimitTone.Normal)]
    [InlineData(74, LimitTone.Normal)]
    [InlineData(75, LimitTone.Caution)]
    [InlineData(89, LimitTone.Caution)]
    [InlineData(90, LimitTone.Critical)]
    [InlineData(100, LimitTone.Critical)]
    public void Colour_follows_the_thresholds(int percent, LimitTone expected)
    {
        Assert.Equal(expected, CardState.ToneFor(percent, DataFreshness.Live));
    }

    [Fact]
    public void Stale_numbers_lose_their_colour_but_keep_their_value()
    {
        var snapshot = Snapshot(Now.AddHours(-1), new LimitWindow(LimitKind.FiveHour, 94, Now.AddHours(2)));

        var card = CardState.From(snapshot, Now);

        Assert.Equal(DataFreshness.Stale, card.Freshness);
        Assert.Equal(94, card.Rows[0].Percent);
        Assert.Equal(LimitTone.Unknown, card.Rows[0].Tone);
    }

    [Fact]
    public void Without_data_the_card_has_no_rows_and_no_time()
    {
        var card = CardState.From(LimitSnapshot.NoData("Work"), Now);

        Assert.False(card.HasData);
        Assert.Equal(DataFreshness.None, card.Freshness);
        Assert.Null(card.UpdatedAt);
        Assert.Null(card.IconRow);
    }

    [Fact]
    public void The_icon_follows_the_session_window()
    {
        var snapshot = Snapshot(
            Now,
            new LimitWindow(LimitKind.SevenDay, 61, Now.AddDays(4)),
            new LimitWindow(LimitKind.FiveHour, 34, Now.AddHours(3)));

        var card = CardState.From(snapshot, Now);

        Assert.Equal(LimitKind.FiveHour, card.IconRow!.Value.Kind);
        Assert.Equal(34, card.IconRow.Value.Percent);
    }

    [Fact]
    public void A_reset_window_shows_zero_and_no_estimate()
    {
        var snapshot = Snapshot(Now, new LimitWindow(LimitKind.FiveHour, 88, Now.AddMinutes(-5)));

        var row = CardState.From(snapshot, Now).Rows.Single();

        Assert.True(row.IsReset);
        Assert.Equal(0, row.Percent);
        Assert.Equal(PaceVerdict.Unknown, row.Pace.Verdict);
    }

    [Fact]
    public void A_slow_pace_lasts_past_the_reset()
    {
        // One hour into a five hour window, ten percent spent: at this rate the window ends first.
        var snapshot = Snapshot(Now, new LimitWindow(LimitKind.FiveHour, 10, Now.AddHours(4)));

        var row = CardState.From(snapshot, Now).Rows.Single();

        Assert.Equal(PaceVerdict.LastsPastReset, row.Pace.Verdict);
        Assert.Equal(TimeSpan.FromHours(4), row.Pace.TimeLeft);
    }

    [Fact]
    public void A_fast_pace_says_how_long_is_left()
    {
        // Four hours into a five hour window, eighty percent spent: twenty percent left at the same
        // rate is one more hour, and the window still has an hour to go — it is a close call.
        var snapshot = Snapshot(Now, new LimitWindow(LimitKind.FiveHour, 90, Now.AddHours(1)));

        var row = CardState.From(snapshot, Now).Rows.Single();

        Assert.Equal(PaceVerdict.RunsOutBeforeReset, row.Pace.Verdict);
        Assert.True(row.Pace.TimeLeft < TimeSpan.FromHours(1));
        Assert.True(row.Pace.TimeLeft > TimeSpan.Zero);
    }

    [Fact]
    public void A_window_that_has_not_started_yet_has_no_estimate()
    {
        var snapshot = Snapshot(Now, new LimitWindow(LimitKind.FiveHour, 0, Now.AddHours(5)));

        var row = CardState.From(snapshot, Now).Rows.Single();

        Assert.Equal(PaceVerdict.Unknown, row.Pace.Verdict);
    }

    [Fact]
    public void The_model_limit_becomes_a_third_row_in_direct_mode()
    {
        var report = UsageReport.FromJson("""
            {
              "five_hour": { "utilization": 34.0, "resets_at": "2026-09-12T15:00:00+00:00" },
              "limits": [ { "kind": "weekly_scoped", "percent": 3.0, "resets_at": "2026-09-16T03:00:00+00:00",
                            "scope": { "model": { "display_name": "Fable" } } } ]
            }
            """);
        var snapshot = LimitSnapshot.FromDirectMode("Personal", Now, report);

        var card = CardState.From(snapshot, Now);

        Assert.Equal(2, card.Rows.Count);
        var model = card.Rows.Single(r => r.Kind == LimitKind.ModelWeek);
        Assert.Equal(3, model.Percent);
        Assert.Equal(PaceVerdict.Unknown, model.Pace.Verdict);
    }

    [Fact]
    public void The_card_keeps_the_time_the_numbers_arrived()
    {
        var snapshot = Snapshot(Now.AddMinutes(-2), new LimitWindow(LimitKind.FiveHour, 34, Now.AddHours(3)));

        var card = CardState.From(snapshot, Now);

        Assert.Equal(Now.AddMinutes(-2), card.UpdatedAt);
        Assert.Equal(DataFreshness.Live, card.Freshness);
    }
}
