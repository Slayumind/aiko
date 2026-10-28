namespace Aiko.Core.Terminal;

/// What the short Claude Code of one environment needs to list its commands.
public sealed record CommandSetup(
    string Executable,
    string WorkingDirectory,
    string ConfigDirectory,
    string UserProfile,
    IReadOnlyList<string> InheritedVariables);

/// What Claude Code offers in one environment: the commands of the menu of "/" and the models.
public sealed record ClaudeOffer(IReadOnlyList<SlashCommand> Commands, IReadOnlyList<ClaudeModel> Models)
{
    public static readonly ClaudeOffer None = new([], []);
}

/// Asks a short Claude Code for the commands and the models it knows (AIKO-330, AIKO-387).
///
/// The process starts, gets "initialize", names its commands and models and is asked to leave.
/// One answer per environment is enough for the life of the window: skills, plugins and models
/// rarely change while it is open. done is called once, on the thread post brings the work to,
/// with the offer, or with an empty one when Claude Code did not start or left without one.
public sealed class CommandLoader(Func<IConversationProcess> newProcess, Action<Action> post)
{
    private const string RequestId = "commands-1";

    /// Claude Code answers in under a second. One that hangs on its start is stopped after this,
    /// so no process waits until Aiko quits.
    private static readonly TimeSpan MostWait = TimeSpan.FromSeconds(20);

    public void Load(CommandSetup setup, Action<ClaudeOffer> done)
    {
        var plan = ClaudeLaunchPlan.ForCommands(setup.ConfigDirectory, setup.UserProfile, setup.InheritedVariables);
        var process = newProcess();
        var gate = new object();
        var answered = false;

        void Answer(ClaudeOffer offer)
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
            post(() => done(offer));
        }

        var started = process.Start(
            new ProcessStart(setup.Executable, plan.Arguments, plan.Variables, setup.WorkingDirectory),
            line =>
            {
                if (StreamEvent.FromLine(line) is StreamEvent.Commands { RequestId: RequestId } commands)
                {
                    Answer(new ClaudeOffer(commands.List, commands.Models));
                }
            },
            _ => Answer(ClaudeOffer.None));

        if (!started || !process.Write(HostLine.Initialize(RequestId)))
        {
            Answer(ClaudeOffer.None);
            return;
        }

        _ = Task.Delay(MostWait).ContinueWith(_ => Answer(ClaudeOffer.None), TaskScheduler.Default);
    }
}
