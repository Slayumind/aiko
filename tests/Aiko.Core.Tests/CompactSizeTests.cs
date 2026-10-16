using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

public class CompactSizeTests
{
    [Theory]
    [InlineData(46425, 46)]
    [InlineData(6302, 6)]
    [InlineData(1500, 2)]
    [InlineData(1499, 1)]
    [InlineData(120, 1)]
    [InlineData(0, 1)]
    [InlineData(1_000_000, 1000)]
    public void Tokens_show_in_thousands_and_never_as_zero(int tokens, int thousands) =>
        Assert.Equal(thousands, CompactSize.Thousands(tokens));

    [Theory]
    [InlineData(46425, 6302, true)]
    [InlineData(46425, 0, false)]
    [InlineData(0, 6302, false)]
    [InlineData(0, 0, false)]
    public void A_size_needs_both_numbers(int before, int after, bool known) =>
        Assert.Equal(known, CompactSize.Of(new StreamEvent.Compacted(before, after)) is not null);
}
