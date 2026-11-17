using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// When the open session counts as seen, so a turn that ends in it is no news (D-389).
public class TerminalSeenTests
{
    [Fact]
    public void A_window_in_front_with_the_keyboard_sees_its_open_session()
    {
        Assert.True(TerminalPulse.Sees(inView: true, inFocus: true));
    }

    [Fact]
    public void A_window_left_open_under_another_one_does_not_see_it()
    {
        // The owner went to the browser: the window is on screen, but nobody looks at it.
        Assert.False(TerminalPulse.Sees(inView: true, inFocus: false));
    }

    [Fact]
    public void A_minimized_or_closed_window_does_not_see_it()
    {
        Assert.False(TerminalPulse.Sees(inView: false, inFocus: false));
    }
}
