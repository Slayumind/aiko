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
