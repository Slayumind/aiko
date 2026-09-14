namespace Aiko.Core.Tests;

/// When Aiko is allowed to guess how long a limit will last, and when it must keep quiet.
///
/// The guess stretches a single measurement across the whole window. Early in a window that is
/// nonsense, and it was being said as plainly as any other number. Somebody stops working because
/// of a number like that, so the card now says nothing until there is something to go on.
public class PaceEstimateTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = TimeSpan.FromHours(5);

    /// One five hour window, so far along and so much of it spent.
    private static PaceEstimate Pace(int percent, TimeSpan windowGone)
    {
        var resetsAt = Now + (Window - windowGone);
        var snapshot = new LimitSnapshot(
            "Personal",
            LimitSource.StatusLine,
            Now,
            [new LimitWindow(LimitKind.FiveHour, percent, resetsAt)]);

        return CardState.From(snapshot, Now).Rows[0].Pace;
    }

    [Fact]
    public void Two_minutes_into_a_window_there_is_nothing_to_go_on()
    {
        // One percent in two minutes used to read as "runs out in about three hours".
        Assert.Equal(PaceVerdict.Unknown, Pace(1, TimeSpan.FromMinutes(2)).Verdict);
    }

    [Fact]
    public void A_tiny_share_spent_says_nothing_even_late_in_the_window()
    {
        Assert.Equal(PaceVerdict.Unknown, Pace(2, TimeSpan.FromHours(4)).Verdict);
    }

    [Fact]
    public void A_big_share_spent_says_nothing_while_the_window_is_young()
    {
        // Somebody who opened a huge file in the first minutes is not going at that rate all day.
        Assert.Equal(PaceVerdict.Unknown, Pace(40, TimeSpan.FromMinutes(10)).Verdict);
    }

    [Fact]
    public void Once_there_is_something_to_go_on_the_guess_comes_back()
    {
        var pace = Pace(50, TimeSpan.FromHours(1));

        Assert.Equal(PaceVerdict.RunsOutBeforeReset, pace.Verdict);
        // Half the limit in one hour: the rest goes in about another hour.
        Assert.InRange(pace.TimeLeft, TimeSpan.FromMinutes(55), TimeSpan.FromMinutes(65));
    }

    [Fact]
    public void A_slow_hour_lasts_past_the_reset()
    {
        var pace = Pace(10, TimeSpan.FromHours(2));

        Assert.Equal(PaceVerdict.LastsPastReset, pace.Verdict);
        Assert.InRange(pace.TimeLeft, TimeSpan.FromMinutes(175), TimeSpan.FromMinutes(185));
    }

    [Fact]
    public void Just_over_both_thresholds_is_enough()
    {
        // Five percent and half an hour of a five hour window: the first point worth a guess.
        Assert.NotEqual(PaceVerdict.Unknown, Pace(6, TimeSpan.FromMinutes(35)).Verdict);
    }

    [Fact]
    public void A_window_that_just_reset_says_nothing()
    {
        var snapshot = new LimitSnapshot(
            "Personal",
            LimitSource.StatusLine,
            Now,
            [new LimitWindow(LimitKind.FiveHour, 80, Now.AddMinutes(-1))]);

        var row = CardState.From(snapshot, Now).Rows[0];

        Assert.True(row.IsReset);
        Assert.Equal(PaceVerdict.Unknown, row.Pace.Verdict);
    }

    [Fact]
    public void The_weekly_model_limit_never_guesses()
    {
        // Nothing reports how long that window is, so there is no window to reason about.
        var snapshot = new LimitSnapshot(
            "Personal",
            LimitSource.DirectMode,
            Now,
            [new LimitWindow(LimitKind.SevenDay, 60, Now.AddDays(3))])
        {
            Model = new ModelLimit("Fable", 70, Now.AddDays(3)),
        };

        var model = CardState.From(snapshot, Now).Rows.Single(r => r.Kind == LimitKind.ModelWeek);

        Assert.Equal(PaceVerdict.Unknown, model.Pace.Verdict);
    }
}
