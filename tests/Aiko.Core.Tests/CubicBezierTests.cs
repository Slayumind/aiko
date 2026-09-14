using Aiko.Core;

namespace Aiko.Core.Tests;

public class CubicBezierTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void Every_curve_starts_at_0_and_ends_at_1(double time)
    {
        Assert.Equal(time, CubicBezier.Standard.Ease(time), 6);
        Assert.Equal(time, CubicBezier.Spring.Ease(time), 6);
    }

    [Fact]
    public void Time_outside_the_animation_is_clamped()
    {
        Assert.Equal(0, CubicBezier.Standard.Ease(-0.5));
        Assert.Equal(1, CubicBezier.Standard.Ease(1.5));
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void A_straight_curve_is_linear(double time)
    {
        Assert.Equal(time, new CubicBezier(1.0 / 3, 1.0 / 3, 2.0 / 3, 2.0 / 3).Ease(time), 4);
    }

    [Fact]
    public void Standard_is_most_of_the_way_there_early()
    {
        // 0.827631 is the same curve solved by bisection outside this code, to six places.
        Assert.Equal(0.827631, CubicBezier.Standard.Ease(0.3), 5);
    }

    [Fact]
    public void Spring_goes_past_the_end_before_it_settles()
    {
        var peak = Enumerable.Range(1, 99).Select(i => CubicBezier.Spring.Ease(i / 100.0)).Max();

        Assert.True(peak > 1.02, $"peak was {peak}");
        Assert.True(peak < 1.15, $"peak was {peak}");
    }

    [Fact]
    public void Standard_never_goes_backwards()
    {
        var values = Enumerable.Range(0, 101).Select(i => CubicBezier.Standard.Ease(i / 100.0)).ToList();

        Assert.All(values.Zip(values.Skip(1)), pair => Assert.True(pair.Second >= pair.First - 1e-9));
    }
}
