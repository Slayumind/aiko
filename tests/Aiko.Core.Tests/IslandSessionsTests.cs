using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// What the island says about the sessions while the terminal window is closed (D-364).
public class IslandSessionsTests
{
    [Fact]
    public void Working_and_waiting_sessions_are_counted_apart()
    {
        var island = IslandSessions.Of(
        [
            ("a", TerminalActivity.Working),
            ("b", TerminalActivity.Waiting),
            ("c", TerminalActivity.Working),
            ("d", TerminalActivity.Done),
            ("e", TerminalActivity.Idle),
            ("f", TerminalActivity.Exited),
        ]);

        Assert.Equal(new IslandSessions(Working: 2, Waiting: 1, FirstWaiting: "b"), island);
    }

    [Fact]
    public void The_first_waiting_session_is_the_one_a_click_opens()
    {
        var island = IslandSessions.Of([("x", TerminalActivity.Waiting), ("y", TerminalActivity.Waiting)]);

        Assert.Equal("x", island.FirstWaiting);
        Assert.Equal(2, island.Waiting);
    }

    [Fact]
    public void Nothing_working_or_waiting_leaves_the_island_as_it_was()
    {
        var island = IslandSessions.Of([("a", TerminalActivity.Done), ("b", TerminalActivity.Idle)]);

        Assert.True(island.IsEmpty);
        Assert.True(IslandSessions.None.IsEmpty);
    }
}
