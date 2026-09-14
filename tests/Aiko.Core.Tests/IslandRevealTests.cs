using Aiko.Core;

namespace Aiko.Core.Tests;

public class IslandRevealTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static CardState Card(string environment, int percent, bool stale = false) =>
        CardState.From(
            new LimitSnapshot(environment, LimitSource.StatusLine, stale ? Now.AddHours(-2) : Now, [new LimitWindow(LimitKind.FiveHour, percent, Now.AddHours(3))]),
            Now);

    [Theory]
    [InlineData(70, 76, true)]
    [InlineData(80, 91, true)]
    [InlineData(40, 95, true)]
    [InlineData(76, 80, false)]
    [InlineData(91, 20, false)]
    [InlineData(40, 60, false)]
    public void The_island_opens_when_a_session_crosses_a_threshold_upwards(int before, int after, bool opens)
    {
        Assert.Equal(opens, IslandReveal.ToneRose([Card("Work", before)], [Card("Work", after)]));
    }

    [Fact]
    public void The_first_numbers_and_numbers_after_a_silence_do_not_open_it()
    {
        Assert.False(IslandReveal.ToneRose([], [Card("Work", 92)]));
        Assert.False(IslandReveal.ToneRose([Card("Work", 50, stale: true)], [Card("Work", 92)]));
    }

    [Fact]
    public void Either_environment_crossing_is_enough()
    {
        var before = new[] { Card("Aiko", 30), Card("Work", 70) };
        var after = new[] { Card("Aiko", 31), Card("Work", 78) };

        Assert.True(IslandReveal.ToneRose(before, after));
    }
}
