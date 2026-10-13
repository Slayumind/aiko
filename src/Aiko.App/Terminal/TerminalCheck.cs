using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// The doors of the terminal that need no window: they check the pipes and the conversation and
/// say in the log what they found. Each returns the exit code of the run.
[SupportedOSPlatform("windows10.0.17763")]
static class TerminalCheck
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    /// --try-pipe: starts Aiko as a child of itself through the real process code and checks what
    /// a conversation relies on: letters of any language come back whole, a line of five megabytes
    /// comes back whole, the variables are set and removed, and stopping ends the child together
    /// with the child it started.
    public static int Pipe()
    {
        var lines = new BlockingCollection<string>();
        using var exited = new ManualResetEventSlim();
        Environment.SetEnvironmentVariable(PipeChild.RemovedVariable, "still here");

        var process = new ClaudeProcess();
        var started = process.Start(
            new ProcessStart(
                Environment.ProcessPath!,
                [PipeChild.Argument],
                [new VariableChange(PipeChild.SetVariable, "set"), new VariableChange(PipeChild.RemovedVariable, null)],
                Path.GetTempPath()),
            lines.Add,
            _ => exited.Set());

        if (!started)
        {
            TerminalLog.PipeChecked(false, false, false, false, false, false);
            return 1;
        }

        string? Asked(string line) =>
            process.Write(line) && lines.TryTake(out var answer, Patience) ? answer : null;

        const string Letters = "Привет, こんにちは, 🙂 naïve";
        var letters = Asked(Letters) == Letters;

        var longText = string.Concat(Enumerable.Repeat("я🙂a", 5 * 1024 * 1024 / 7));
        var longLine = Asked(longText) == longText;

        var variables = Asked("variables") == "set|";

        var family = (Asked("spawn") ?? "").Split(' ').Select(id => int.TryParse(id, out var known) ? known : 0).ToList();
        var bothStarted = family.Count == 2 && family.All(IsAlive);

        process.Stop();
        var exitSeen = exited.Wait(Patience);
        var treeGone = bothStarted && WaitUntilGone(family);

        TerminalLog.PipeChecked(true, letters, longLine, variables, exitSeen, treeGone);
        return letters && longLine && variables && exitSeen && treeGone ? 0 : 1;
    }

    /// --try-conversation &lt;script&gt;: plays a conversation against a scripted Claude Code, with the
    /// real log behind it. The script names a mark, a word that is said in the conversation; the
    /// check fails when the mark is found in the log of Aiko.
    public static int Conversation(string scriptPath)
    {
        var json = ReadOrEmpty(scriptPath);
        var mark = MarkIn(json);
        if (TalkScript.Parse(json) is not { } talk || mark.Length == 0)
        {
            TerminalLog.ConversationChecked(false, false, false, 0, 0, 0);
            return 1;
        }

        var claude = new ScriptedClaude(talk.Replies);
        var runner = new ConversationRunner(Setup("claude", Path.GetTempPath(), null), claude.NewProcess, work => work(), new TerminalLog());
        talk.Play(runner);
        runner.Close();

        var markSeen = claude.Written.Any(line => line.Contains(mark, StringComparison.Ordinal))
            && talk.Replies.Any(reply => reply.Lines.Any(line => line.Contains(mark, StringComparison.Ordinal)));
        var logClean = !ReadOrEmpty(Log.FilePath).Contains(mark, StringComparison.Ordinal);

        TerminalLog.ConversationChecked(true, markSeen, logClean, runner.State.Items.Count, claude.Starts.Count, claude.Written.Count);
        return markSeen && logClean ? 0 : 1;
    }

    /// --try-conversation real &lt;folder&gt; [environment]: one short turn with the real Claude Code,
    /// on the smallest model. It spends a little of the limits and leaves one conversation in the
    /// history of that folder, so it is run only when the person says so.
    public static int Real(string folder, string? environmentName)
    {
        if (ClaudeLauncher.FindClaude() is not { } claude || !Directory.Exists(folder))
        {
            TerminalLog.RealChecked(false, false, false, false, 0);
            return 1;
        }

        var work = new BlockingCollection<Action>();
        var runner = new ConversationRunner(Setup(claude, folder, environmentName), () => new ClaudeProcess(), work.Add, new TerminalLog());

        // Nothing is allowed: the check asks for one word and has no reason to touch a file.
        runner.Changed = () =>
        {
            foreach (var card in runner.State.Items.OfType<FeedItem.Card>().Where(card => card.State == RequestState.Open))
            {
                runner.Choose(card.Request.RequestId, new CardChoice.Deny(""));
            }
        };

        runner.Send("Reply with the single word: ok", []);

        var deadline = Stopwatch.StartNew();
        while (runner.State.IsBusy && deadline.Elapsed < TimeSpan.FromMinutes(2))
        {
            if (work.TryTake(out var next, TimeSpan.FromMilliseconds(200)))
            {
                next();
            }
        }

        var state = runner.State;
        runner.Close();

        var answered = state.Items.Any(item => item is FeedItem.Answer { Text.Length: > 0 });
        TerminalLog.RealChecked(true, answered, state.Activity == TalkActivity.SignedOut, state.Activity == TalkActivity.Failed, state.Items.Count);
        return answered ? 0 : 1;
    }

    /// --try-history &lt;folder&gt; &lt;session id&gt; [environment]: reads the file Claude Code keeps of a
    /// conversation the way the window will, and counts what came out. The text stays in memory.
    public static int History(string folder, string sessionId, string? environmentName)
    {
        var setup = Setup("claude", folder, environmentName);
        var found = TerminalStore.TranscriptFile(setup.ConfigDirectory, folder, sessionId) is not null;
        var items = TerminalStore.History(setup.ConfigDirectory, folder, sessionId, PermissionMode.Manual).Items;

        TerminalLog.HistoryChecked(
            found,
            items.Count,
            items.Count(item => item is FeedItem.OwnerMessage),
            items.Count(item => item is FeedItem.Answer),
            items.Count(item => item is FeedItem.Step),
            items.Count(item => item is FeedItem.Notice));
        return found && items.Count > 0 ? 0 : 1;
    }

    private static RunnerSetup Setup(string claude, string folder, string? environmentName)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var environments = SettingsStore.LoadEnvironments().Environments;
        var environment = environments.FirstOrDefault(one => one.Name == environmentName) ?? environments.FirstOrDefault();

        return new RunnerSetup(
            claude,
            folder,
            environment?.ConfigDirectories.FirstOrDefault() ?? ClaudeConfigFolder.Resolve(null, home),
            home,
            environment?.Name ?? "",
            Guid.NewGuid().ToString(),
            Started: false,
            PermissionMode.Manual,
            "haiku",
            [.. Environment.GetEnvironmentVariables().Keys.Cast<string>()]);
    }

    private static string MarkIn(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("mark", out var mark)
                && mark.ValueKind == JsonValueKind.String
                    ? mark.GetString() ?? ""
                    : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    private static string ReadOrEmpty(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    private static bool WaitUntilGone(IReadOnlyList<int> processIds)
    {
        var waited = Stopwatch.StartNew();
        while (processIds.Any(IsAlive) && waited.Elapsed < Patience)
        {
            Thread.Sleep(100);
        }

        return !processIds.Any(IsAlive);
    }

    private static bool IsAlive(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}
