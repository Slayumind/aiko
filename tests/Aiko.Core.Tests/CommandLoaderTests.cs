using Aiko.Core.Terminal;

namespace Aiko.Core.Tests;

public class CommandLoaderTests
{
    private const string Answer =
        """{"type":"control_response","response":{"subtype":"success","request_id":"commands-1","response":{"commands":[{"name":"compact","description":"Free up context","argumentHint":"","builtin":true}]}}}""";

    private sealed class ScriptedProcess(bool starts) : IConversationProcess
    {
        public Action<string> Say { get; private set; } = _ => { };

        public Action<int> Exit { get; private set; } = _ => { };

        public List<string> Written { get; } = [];

        public ProcessStart? Started { get; private set; }

        public bool Stopped { get; private set; }

        public bool Start(ProcessStart start, Action<string> onLine, Action<int> onExit)
        {
            Started = start;
            Say = onLine;
            Exit = onExit;
            return starts;
        }

        public bool Write(string line)
        {
            Written.Add(line);
            return starts;
        }

        public void Stop() => Stopped = true;
    }

    private static readonly CommandSetup Setup = new("claude", "C:/work", "C:/Users/me/.claude", "C:/Users/me", []);

    private static (ScriptedProcess Process, List<IReadOnlyList<SlashCommand>> Answers) Loaded(bool starts = true)
    {
        var process = new ScriptedProcess(starts);
        var answers = new List<IReadOnlyList<SlashCommand>>();
        new CommandLoader(() => process, work => work()).Load(Setup, answers.Add);
        return (process, answers);
    }

    [Fact]
    public void AsksForCommandsWithoutASessionAndWithoutHooks()
    {
        var (process, _) = Loaded();

        Assert.Contains("--input-format", process.Started!.Arguments);
        Assert.DoesNotContain("--session-id", process.Started.Arguments);
        Assert.Contains("""{"disableAllHooks":true}""", process.Started.Arguments);
        Assert.Contains("\"initialize\"", Assert.Single(process.Written));
    }

    [Fact]
    public void GivesTheCommandsOnceAndLetsClaudeCodeGo()
    {
        var (process, answers) = Loaded();

        process.Say(Answer);
        process.Exit(0);

        var commands = Assert.Single(answers);
        Assert.Equal("compact", Assert.Single(commands).Name);
        Assert.True(process.Stopped);
    }

    [Fact]
    public void LeavingWithoutAnAnswerGivesAnEmptyList()
    {
        var (process, answers) = Loaded();

        process.Say("""{"type":"system","subtype":"init","session_id":"s"}""");
        process.Exit(1);

        Assert.Empty(Assert.Single(answers));
    }

    [Fact]
    public void AClaudeCodeThatDidNotStartGivesAnEmptyList()
    {
        var (_, answers) = Loaded(starts: false);

        Assert.Empty(Assert.Single(answers));
    }
}
