using Aiko.Core;

namespace Aiko.Core.Tests;

public class IconRowsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static CardState Card(string environment, int? percent) =>
        CardState.From(
            new LimitSnapshot(
                environment,
                LimitSource.StatusLine,
                Now,
                percent is { } used ? [new LimitWindow(LimitKind.FiveHour, used, Now.AddHours(3))] : []),
            Now);

    [Fact]
    public void With_nothing_reported_the_ring_and_the_dot_are_empty()
    {
        Assert.Equal((null, null), IconRows.Choose([], "Work"));
        Assert.Equal((null, null), IconRows.Choose([Card("Aiko", null), Card("Work", null)], "Work"));
    }

    [Fact]
    public void One_environment_takes_the_ring_and_leaves_no_dot()
    {
        var (ring, dot) = IconRows.Choose([Card("Aiko", 42)], null);

        Assert.Equal(42, ring?.Percent);
        Assert.Null(dot);
    }

    [Fact]
    public void The_chosen_environment_is_the_ring_and_the_other_is_the_dot()
    {
        var (ring, dot) = IconRows.Choose([Card("Aiko", 42), Card("Work", 82)], "Work");

        Assert.Equal(82, ring?.Percent);
        Assert.Equal(42, dot?.Percent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Gone")]
    public void Without_a_choice_that_exists_the_first_environment_is_the_ring(string? chosen)
    {
        var (ring, dot) = IconRows.Choose([Card("Aiko", 42), Card("Work", 82)], chosen);

        Assert.Equal(42, ring?.Percent);
        Assert.Equal(82, dot?.Percent);
    }

    [Fact]
    public void A_chosen_environment_with_no_numbers_gives_the_ring_to_one_that_has_them()
    {
        var (ring, dot) = IconRows.Choose([Card("Aiko", 42), Card("Work", null)], "Work");

        Assert.Equal(42, ring?.Percent);
        Assert.Null(dot);
    }

    [Fact]
    public void With_three_environments_the_dot_is_the_first_other_one_with_numbers()
    {
        var (ring, dot) = IconRows.Choose([Card("Aiko", 42), Card("Side", null), Card("Work", 82)], "Aiko");

        Assert.Equal(42, ring?.Percent);
        Assert.Equal(82, dot?.Percent);
    }
}
