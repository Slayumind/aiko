using System.Diagnostics;
using System.IO;
using System.Windows;
using Aiko.Core;
using Aiko.Core.Terminal;

namespace Aiko.App;

/// How a session gets its real terminal: why it may not start (D-421), and the command line of Claude Code or a shell.
sealed partial class TerminalHost
{
    /// The terminal of a session, started the first time the session is shown.
    private TerminalSurface? TerminalOf(TerminalSession session)
    {
        if (_terminals.TryGetValue(session.Id, out var known))
        {
            return known;
        }

        if (_problems.ContainsKey(session.Id))
        {
            return null;
        }

        // A session whose folder is gone does not start somewhere else: Claude Code would not find
        // its conversation there, and a command line would open in a folder nobody chose.
        if (!Directory.Exists(session.Folder))
        {
            _problems[session.Id] = (StartProblem.FolderGone, 0);
            return null;
        }

        if (Launch(session) is not { } launch)
        {
            _problems[session.Id] = (StartProblem.NoClaude, 0);
            return null;
        }

        var terminal = new TerminalSurface(launch, blocks: session.Kind == SessionKind.Shell);
        terminal.Exited += code => Ended(session.Id, code);
        terminal.StartFailed += error =>
        {
            TerminalLog.StartFailed(session.Environment, session.Kind == SessionKind.Shell, error);
            _terminals.Remove(session.Id);
            _problems[session.Id] = (StartProblem.Failed, error);
            Draw();
        };
        if (terminal.Blocks is { } blocks)
        {
            blocks.Folder = session.Folder;
            blocks.ExplainAsked += (block, output) => Ask(
                session,
                ShellAsk.Explain(ShellLaunch.Find().Name, block.Command, block.ExitCode ?? 1, output, Language()),
                answer => blocks.ShowExplanation(block.StartedAt, answer));
            terminal.FindClosed += () => _panel.Editor.FocusText();
        }

        terminal.FullScreenChanged += on =>
        {
            // vim and less read the keys themselves; back in the blocks, the editor has them again.
            if (!on && _model.Session(_sessions)?.Id == session.Id)
            {
                _panel.Editor.FocusText();
            }
        };
        terminal.DrawnFirst += () =>
        {
            // The loader gives way to the terminal; the keys stay with the editor.
            Draw();
            if (_model.Session(_sessions)?.Id == session.Id)
            {
                _panel.Editor.FocusText();
            }
        };
        if (session.Kind == SessionKind.Claude)
        {
            terminal.TitleChanged += title => TitleCame(session.Id, title);
            terminal.Typed += () => Typed(session.Id);
        }

        _terminals[session.Id] = terminal;
        // An event left from an earlier run is not news.
        Forget(session.Id);
        _events.Forget(session.Id);
        _seenAt[session.Id] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var shell = session.Kind == SessionKind.Shell;
        TerminalLog.Started(session.Environment, shell, session.Started);
        if (!shell && !session.Started)
        {
            // From now on the conversation is Claude Code's, and the next start resumes it.
            _sessions = _sessions.MarkStarted(session.Id);
        }

        _sessions = _sessions.Used(session.Id, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Save();
        return terminal;
    }

    /// The official page on installing Claude Code.
    private const string ClaudeInstallPage = "https://code.claude.com/docs/en/setup";

    /// "Start again": the session tries once more, with Claude Code found or the folder back.
    private void RetryStart()
    {
        if (_model.Session(_sessions) is { } session)
        {
            _problems.Remove(session.Id);
            Draw();
        }
    }

    /// Whether Claude Code saved the conversation, in any project folder of the environment: the
    /// folder of the session may have moved since, and a long name gets a tail of Claude Code's own.
    private static bool ConversationOnDisk(string config, string id)
    {
        var projects = Path.Combine(config, "projects");
        try
        {
            return Directory.Exists(projects)
                && Directory.EnumerateDirectories(projects).Any(folder => File.Exists(Path.Combine(folder, id + ".jsonl")));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Unsure: resuming is what a started session did before.
            return true;
        }
    }

    private PtyLaunch? Launch(TerminalSession session)
    {
        if (_settings.Environments.FirstOrDefault(one => one.Name == session.Environment) is not { } environment)
        {
            return null;
        }

        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var config = environment.ConfigDirectories.FirstOrDefault() ?? ClaudeConfigFolder.Resolve(null, home);
        var folder = Directory.Exists(session.Folder) ? session.Folder : home;
        var inherited = System.Environment.GetEnvironmentVariables().Keys.Cast<string>().ToList();

        if (session.Kind == SessionKind.Shell)
        {
            var plan = ClaudeLaunchPlan.ForShell(config, home, inherited);
            var shell = Shell();
            return new PtyLaunch(shell.CommandLine, folder, Variables(plan));
        }

        if (ClaudeLauncher.FindClaude() is not { } claude)
        {
            return null;
        }

        var claudePlan = ClaudeLaunchPlan.For(
            new LaunchRequest(
                config, home, environment.Name, session.Id, session.Started, TerminalEvents.HooksFile(),
                ThisComputer.Folders.PicturesFolder(session.Id), session.Started && ConversationOnDisk(config, session.Id)),
            inherited);
        return new PtyLaunch(CommandLine(claude, claudePlan.Arguments), folder, Variables(claudePlan));
    }
}
