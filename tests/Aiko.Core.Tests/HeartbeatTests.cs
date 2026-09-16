using Aiko.Core;

namespace Aiko.Core.Tests;

/// The daily identifier that goes with the update check.
///
/// The whole promise rests on one property: two days of it cannot be joined back into one person.
/// These tests hold that promise still, because a change here would be the kind that nobody
/// notices and everybody would mind.
public class HeartbeatTests
{
    private const string Install = "6f1c0d4a9b8e4f2a91d37c5e08b64a12";
    private static readonly DateOnly Monday = new(2026, 9, 7);
    private static readonly DateOnly Tuesday = new(2026, 9, 8);

    [Fact]
    public void The_same_day_gives_the_same_answer()
    {
        Assert.Equal(Heartbeat.DailyId(Install, Monday), Heartbeat.DailyId(Install, Monday));
    }

    [Fact]
    public void The_next_day_gives_another_one()
    {
        // Without this the identifier is a name, and the promise in PRIVACY.md is untrue.
        Assert.NotEqual(Heartbeat.DailyId(Install, Monday), Heartbeat.DailyId(Install, Tuesday));
    }

    [Fact]
    public void Two_computers_differ_on_the_same_day()
    {
        Assert.NotEqual(
            Heartbeat.DailyId(Install, Monday),
            Heartbeat.DailyId("0000aaaa1111bbbb2222cccc3333dddd", Monday));
    }

    [Fact]
    public void The_value_kept_on_the_computer_never_appears_in_what_is_sent()
    {
        var sent = Heartbeat.DailyId(Install, Monday);

        Assert.DoesNotContain(Install, sent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Install[..8], sent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void It_is_short_and_plain_enough_for_a_web_address()
    {
        var sent = Heartbeat.DailyId(Install, Monday);

        Assert.Equal(Heartbeat.IdLength, sent.Length);
        Assert.All(sent, c => Assert.True(char.IsAsciiHexDigitLower(c)));
    }

    /// The week key rides on ISO weeks, where a week belongs to the year that holds its Thursday.
    /// Around New Year that year is not the year on the calendar, and a naive key would count one
    /// copy twice in the first days of January.
    [Theory]
    [InlineData(2026, 1, 1, "2026-W01")]
    [InlineData(2026, 12, 31, "2026-W53")]
    [InlineData(2027, 1, 1, "2026-W53")]
    [InlineData(2026, 9, 16, "2026-W38")]
    public void The_week_key_follows_the_ISO_week(int year, int month, int day, string expected)
    {
        Assert.Equal(expected, Heartbeat.WeekKey(new DateOnly(year, month, day)));
    }

    [Fact]
    public void Monday_and_Sunday_of_one_week_give_the_same_key()
    {
        Assert.Equal(
            Heartbeat.WeekKey(new DateOnly(2026, 9, 14)),
            Heartbeat.WeekKey(new DateOnly(2026, 9, 20)));
    }

    [Fact]
    public void The_next_monday_starts_another_week()
    {
        Assert.NotEqual(
            Heartbeat.WeekKey(new DateOnly(2026, 9, 20)),
            Heartbeat.WeekKey(new DateOnly(2026, 9, 21)));
    }

    [Fact]
    public void The_month_key_is_the_calendar_month()
    {
        Assert.Equal("2026-09", Heartbeat.MonthKey(new DateOnly(2026, 9, 16)));
        Assert.Equal("2028-02", Heartbeat.MonthKey(new DateOnly(2028, 2, 29)));
        Assert.NotEqual(
            Heartbeat.MonthKey(new DateOnly(2026, 9, 30)),
            Heartbeat.MonthKey(new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void A_period_is_first_until_it_has_been_reported()
    {
        var day = new DateOnly(2026, 9, 16);

        Assert.True(Heartbeat.FirstThisWeek(null, day));
        Assert.True(Heartbeat.FirstThisWeek(string.Empty, day));
        Assert.True(Heartbeat.FirstThisMonth(null, day));

        Assert.False(Heartbeat.FirstThisWeek(Heartbeat.WeekKey(day), day));
        Assert.False(Heartbeat.FirstThisMonth(Heartbeat.MonthKey(day), day));
    }

    [Fact]
    public void Last_weeks_answer_does_not_cover_this_week()
    {
        // The copy that ran last Monday and again this Monday is two weekly runs, not one.
        var lastWeek = Heartbeat.WeekKey(new DateOnly(2026, 9, 14));

        Assert.True(Heartbeat.FirstThisWeek(lastWeek, new DateOnly(2026, 9, 21)));
    }

    [Fact]
    public void The_keys_are_not_identifiers_and_hold_no_install_value()
    {
        var day = new DateOnly(2026, 9, 16);

        Assert.DoesNotContain(Install, Heartbeat.WeekKey(day), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Install, Heartbeat.MonthKey(day), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_a_value_on_the_computer_nothing_is_sent(string installId)
    {
        // A fresh random value on every start would count one person many times over, so when the
        // file cannot be written Aiko sends no identifier at all.
        Assert.Equal(string.Empty, Heartbeat.DailyId(installId, Monday));
    }
}
