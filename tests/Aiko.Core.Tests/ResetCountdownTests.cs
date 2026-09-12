using Aiko.Core;

namespace Aiko.Core.Tests;

public class ResetCountdownTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reports_days_and_hours_when_more_than_a_day_is_left()
    {
        var countdown = ResetCountdown.Between(Now, Now.AddDays(4).AddHours(11).AddMinutes(30));

        Assert.False(countdown.IsReset);
        Assert.Equal(CountdownUnit.DaysAndHours, countdown.Unit);
        Assert.Equal(4, countdown.Days);
        Assert.Equal(11, countdown.Hours);
    }

    [Fact]
    public void Reports_hours_and_minutes_within_a_day()
    {
        var countdown = ResetCountdown.Between(Now, Now.AddHours(3).AddMinutes(34).AddSeconds(50));

        Assert.Equal(CountdownUnit.HoursAndMinutes, countdown.Unit);
        Assert.Equal(0, countdown.Days);
        Assert.Equal(3, countdown.Hours);
        Assert.Equal(34, countdown.Minutes);
    }

    [Fact]
    public void Rounds_down_and_never_promises_more_time()
    {
        var countdown = ResetCountdown.Between(Now, Now.AddMinutes(12).AddSeconds(59));

        Assert.Equal(CountdownUnit.Minutes, countdown.Unit);
        Assert.Equal(12, countdown.Minutes);
    }

    [Fact]
    public void Reports_less_than_a_minute_when_almost_no_time_is_left()
    {
        var countdown = ResetCountdown.Between(Now, Now.AddSeconds(59));

        Assert.False(countdown.IsReset);
        Assert.Equal(CountdownUnit.LessThanMinute, countdown.Unit);
    }

    [Fact]
    public void Exactly_one_day_is_still_days_and_hours()
    {
        var countdown = ResetCountdown.Between(Now, Now.AddDays(1));

        Assert.Equal(CountdownUnit.DaysAndHours, countdown.Unit);
        Assert.Equal(1, countdown.Days);
        Assert.Equal(0, countdown.Hours);
    }

    [Fact]
    public void A_window_whose_time_has_passed_is_reset()
    {
        var countdown = ResetCountdown.Between(Now, Now.AddMinutes(-1));

        Assert.True(countdown.IsReset);
    }

    [Fact]
    public void The_moment_of_reset_counts_as_reset()
    {
        var countdown = ResetCountdown.Between(Now, Now);

        Assert.True(countdown.IsReset);
    }
}
