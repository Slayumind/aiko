using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

/// When Claude Code is started and what is written to it are tables, and they live in
/// spec/cases/conversation-runner; both cores read them. What is left here is the order of things
/// between the thread of a process and the thread of the window.
public class ConversationRunnerTests
{
    private const string Ready = """{"type":"system","subtype":"init","session_id":"s-1","model":"claude-opus-5-5","permissionMode":"default"}""";
    private const string Hello = """{"type":"assistant","message":{"content":[{"type":"text","text":"Hello."}]},"parent_tool_use_id":null}""";
    private const string Done = """{"type":"result","subtype":"success","is_error":false,"terminal_reason":"completed","result":"Hello."}""";

    /// A process the test speaks for, line by line.
    private sealed class HandProcess : IConversationProcess
    {
        public Action<string> Say { get; private set; } = _ => { };

        public Action<int> Exit { get; private set; } = _ => { };

        public bool Stopped { get; private set; }

        public bool Start(ProcessStart start, Action<string> onLine, Action<int> onExit)
        {
            Say = onLine;
            Exit = onExit;
            return true;
        }

        public bool Write(string line) => !Stopped;

        public void Stop() => Stopped = true;
    }

    private sealed class NoLog : ITerminalLog
    {
        public void Started(string environment, bool resumed)
        {
        }

        public void StartFailed(string environment)
        {
        }

        public void Exited(string environment, int exitCode, int linesRead, int linesWritten)
        {
        }

        public void Stopped(string environment, int linesRead, int linesWritten)
        {
        }
    }

    /// The runner with the work for the window thread kept in a list, to be done by hand.
    private static (ConversationRunner Runner, List<HandProcess> Processes, List<Action> Posted) Made()
    {
        var processes = new List<HandProcess>();
        var posted = new List<Action>();
        var runner = new ConversationRunner(
            new RunnerSetup("claude", "/work", "/accounts/.claude-work", "/home/someone", "Work", "id-1", false, PermissionMode.Manual, null, []),
            () =>
            {
                processes.Add(new HandProcess());
                return processes[^1];
            },
            posted.Add,
            new NoLog());

        return (runner, processes, posted);
    }

    [Fact]
    public void Lines_that_came_together_reach_the_window_as_one_change()
    {
        var (runner, processes, posted) = Made();
        runner.Send("hi", []);
        var changes = 0;
        runner.Changed = () => changes++;

        processes[0].Say(Ready);
        processes[0].Say(Hello);
        processes[0].Say(Done);

        Assert.Single(posted);
        Assert.Equal(TalkActivity.Working, runner.State.Activity);

        posted[0]();

        Assert.Equal(1, changes);
        Assert.Equal(TalkActivity.Done, runner.State.Activity);
    }

    [Fact]
    public void A_line_after_the_last_change_asks_for_the_window_thread_again()
    {
        var (runner, processes, posted) = Made();
        runner.Send("hi", []);

        processes[0].Say(Ready);
        posted[0]();
        processes[0].Say(Hello);

        Assert.Equal(2, posted.Count);
    }

    [Fact]
    public void What_a_stopped_process_still_says_is_dropped()
    {
        var (runner, processes, posted) = Made();
        runner.Send("hi", []);
        processes[0].Say(Ready);

        runner.Close();
        processes[0].Say(Hello);
        processes[0].Exit(0);
        posted.ForEach(work => work());

        Assert.True(processes[0].Stopped);
        Assert.False(runner.IsRunning);
        Assert.DoesNotContain(runner.State.Items, item => item is FeedItem.Answer);
    }

    [Fact]
    public void The_exit_of_an_older_process_does_not_end_the_new_one()
    {
        var (runner, processes, posted) = Made();
        runner.Send("one", []);
        runner.Close();
        runner.Send("two", []);

        processes[0].Exit(1);
        posted.ForEach(work => work());

        Assert.Equal(2, processes.Count);
        Assert.True(runner.IsRunning);
        Assert.Equal(TalkActivity.Working, runner.State.Activity);
    }
}
