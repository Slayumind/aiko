using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// Which icons of the taskbar button may be destroyed: a replaced icon lives a few seconds after
/// its replacement, however long it stood on the button before.
public class ReplacedIconsTests
{
    private static readonly TimeSpan Keep = TimeSpan.FromSeconds(3);

    [Fact]
    public void An_icon_that_stood_long_lives_on_after_it_is_replaced()
    {
        var icons = new ReplacedIcons<string>(Keep);
        icons.Give("rings", now: 0);

        Assert.Empty(icons.Give("face 1", now: 13_000));
        Assert.Empty(icons.Give("face 2", now: 15_999));
        Assert.Equal(["rings"], icons.Give("face 3", now: 16_001));
    }

    [Fact]
    public void Frames_of_a_transition_go_in_the_order_they_were_replaced()
    {
        var icons = new ReplacedIcons<string>(Keep);
        icons.Give("a", now: 0);
        icons.Give("b", now: 30);
        icons.Give("c", now: 60);
        icons.Give("d", now: 90);

        Assert.Equal(["a", "b"], icons.Give("e", now: 3_061));
    }

    [Fact]
    public void The_icon_just_replaced_and_the_new_one_stay()
    {
        var icons = new ReplacedIcons<string>(Keep);
        icons.Give("a", now: 0);
        icons.Give("b", now: 1);

        Assert.Equal(["a"], icons.Give("c", now: 100_000));
    }

    [Fact]
    public void A_closed_window_gives_back_every_icon()
    {
        var icons = new ReplacedIcons<string>(Keep);
        icons.Give("a", now: 0);
        icons.Give("b", now: 10);

        Assert.Equal(["a", "b"], icons.TakeAll());
        Assert.Empty(icons.TakeAll());
    }
}
