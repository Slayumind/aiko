namespace Aiko.Core.Terminal;

/// What the short Claude Code of one environment needs to list its commands.
public sealed record CommandSetup(
    string Executable,
    string WorkingDirectory,
    string ConfigDirectory,
    string UserProfile,
    IReadOnlyList<string> InheritedVariables);

/// Asks a short Claude Code for the commands it knows, for the menu of "/" (AIKO-330).
///
/// The process starts, gets "initialize", gives its commands and is asked to leave. One list per
/// environment is enough for the life of the window: skills and plugins rarely change while it is
/// open. done is called once, on the thread post brings the work to, with the list, or with an
/// empty list when Claude Code did not start or left without one.
public sealed class CommandLoader(Func<IConversationProcess> newProcess, Action<Action> post)
{
    private const string RequestId = "commands-1";

    public void Load(CommandSetup setup, Action<IReadOnlyList<SlashCommand>> done)
    {
        var plan = ClaudeLaunchPlan.ForCommands(setup.ConfigDirectory, setup.UserProfile, setup.InheritedVariables);
        var process = newProcess();
        var gate = new object();
        var answered = false;

        void Answer(IReadOnlyList<SlashCommand> commands)
        {
            lock (gate)
            {
                if (answered)
                {
                    return;
                }

                answered = true;
            }

            process.Stop();
            post(() => done(commands));
        }

        var started = process.Start(
            new ProcessStart(setup.Executable, plan.Arguments, plan.Variables, setup.WorkingDirectory),
            line =>
            {
                if (StreamEvent.FromLine(line) is StreamEvent.Commands { RequestId: RequestId } commands)
                {
                    Answer(commands.List);
                }
            },
            _ => Answer([]));

        if (!started || !process.Write(HostLine.Initialize(RequestId)))
        {
            Answer([]);
        }
    }
}
