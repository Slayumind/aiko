namespace Aiko.Core.Terminal;

/// One environment variable of the program the terminal starts. A null value means the variable
/// is removed, not set to an empty string.
public sealed record VariableChange(string Name, string? Value);

/// What the terminal needs to know to start Claude Code for one session. HooksFile holds the
/// settings with the hook of the terminal (TerminalHooks); empty when it could not be written.
public sealed record LaunchRequest(
    string ConfigDirectory,
    string UserProfile,
    string EnvironmentName,
    string SessionId,
    bool Started,
    string HooksFile = "");

/// How the terminal starts a program in a real terminal (D-349, D-360): the arguments of Claude
/// Code and what changes in the environment of the process. Claude Code chooses its own mode and
/// model, so only the id of the conversation is passed.
public sealed record ClaudeLaunchPlan(IReadOnlyList<string> Arguments, IReadOnlyList<VariableChange> Variables)
{
    public const string ConfigVariableName = "CLAUDE_CONFIG_DIR";

    /// inheritedVariables are the names in the environment of Aiko itself.
    public static ClaudeLaunchPlan For(LaunchRequest request, IEnumerable<string> inheritedVariables)
    {
        // A conversation gets its id from Aiko before the first turn, so the id is known even when
        // Claude Code never answers. After that the same id brings the conversation back.
        List<string> arguments = [request.Started ? "--resume" : "--session-id", request.SessionId];

        // The hook of the terminal tells the row what the session does, in every environment, with
        // the personality or without it.
        if (request.HooksFile.Length > 0)
        {
            arguments.AddRange(["--settings", request.HooksFile]);
        }

        var variables = Environment(request.ConfigDirectory, request.UserProfile, inheritedVariables);

        // The session start hook reads these two. "terminal" keeps its reminder quiet: the session
        // was started in the folder and the environment the person chose.
        variables.Add(new VariableChange(SessionReminder.EnvironmentVariable, request.EnvironmentName));
        variables.Add(new VariableChange(SessionReminder.LaunchVariable, SessionReminder.LaunchedByTerminal));

        // The hook of the terminal writes to the file of this session, whatever id Claude Code uses.
        variables.Add(new VariableChange(TerminalEvent.SessionVariable, request.SessionId));

        return new ClaudeLaunchPlan(arguments, variables);
    }

    /// A shell of a session (D-350): the environment of the session, so a claude typed in it runs
    /// in the same environment, without the variables of the session start hook.
    public static ClaudeLaunchPlan ForShell(string configDirectory, string userProfile, IEnumerable<string> inheritedVariables) =>
        new([], Environment(configDirectory, userProfile, inheritedVariables));

    private static List<VariableChange> Environment(string configDirectory, string userProfile, IEnumerable<string> inheritedVariables)
    {
        var variables = Leftovers(inheritedVariables);

        // Environment 1 runs without the variable, like a plain claude and the VS Code panel (D-156).
        if (!ClaudeConfigFolder.IsDefault(configDirectory, userProfile))
        {
            variables.Add(new VariableChange(ConfigVariableName, configDirectory));
        }

        return variables;
    }

    private static List<VariableChange> Leftovers(IEnumerable<string> inheritedVariables) =>
        [.. inheritedVariables.Where(IsLeftover).Select(name => new VariableChange(name, null))];

    /// What a session of Claude Code leaves for its children (seen in 2.1.312, RESEARCH). Aiko may
    /// itself be started from a session, and these would tie the new one to it. The person's own
    /// settings of Claude Code, such as CLAUDE_CODE_GIT_BASH_PATH, are not among them and stay.
    private static readonly string[] SessionVariables =
    [
        "CLAUDECODE", "CLAUDE_PID", "CLAUDE_EFFORT", ConfigVariableName,
        "CLAUDE_CODE_ENTRYPOINT", "CLAUDE_CODE_CHILD_SESSION", "CLAUDE_CODE_EXECPATH", "CLAUDE_CODE_SSE_PORT",
    ];

    private static readonly string[] SessionPrefixes = ["CLAUDE_CODE_SESSION_", "CLAUDE_CODE_MESSAGING_", "AIKO_"];

    /// What another Claude Code or another Aiko left in the environment.
    private static bool IsLeftover(string name) =>
        SessionVariables.Any(variable => name.Equals(variable, StringComparison.OrdinalIgnoreCase))
        || SessionPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
