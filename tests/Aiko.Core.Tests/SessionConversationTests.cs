using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// /clear starts a new conversation in the same session (D-453).
public class SessionConversationTests
{
    private const string First = "5f0c2a8e-1b7d-4c1e-9f3a-2d6b8e4a7c10";
    private const string Second = "7a1d3c52-9e04-4b6f-8c21-0f3e5d7a9b64";

    private static TerminalSessions One() =>
        new([new TerminalSession(First, "Aiko", "C:\\work", "work") { Started = true }], First);

    [Fact]
    public void The_session_remembers_the_conversation_across_the_file()
    {
        var moved = One().MoveToConversation(First, Second);
        var read = TerminalSessions.FromJson(moved.ToJson());

        Assert.Equal(Second, read.Find(First)!.ResumeId);
        Assert.Equal(First, read.OwnerOfConversation(Second)!.Id);
    }

    [Fact]
    public void The_tab_counts_what_wants_attention()
    {
        LiveTerminal? Done(string id) => new(TerminalActivity.Done, "");
        LiveTerminal? Stopped(string id) => new(TerminalActivity.Exited, "");

        Assert.Equal(1, SessionRows.Done(One(), "Aiko", Done));
        Assert.Equal(0, SessionRows.Stopped(One(), "Aiko", Done));
        Assert.Equal(1, SessionRows.Stopped(One(), "Aiko", Stopped));
    }

    [Fact]
    public void An_empty_id_or_the_same_one_changes_nothing()
    {
        var sessions = One();

        Assert.Same(sessions, sessions.MoveToConversation(First, ""));
        Assert.Same(sessions, sessions.MoveToConversation(First, First));
    }

    [Fact]
    public void A_broken_id_in_the_file_is_not_taken()
    {
        var text = One().MoveToConversation(First, Second).ToJson().Replace(Second, "--dangerous");

        Assert.Equal(First, TerminalSessions.FromJson(text).Find(First)!.ResumeId);
    }
}
