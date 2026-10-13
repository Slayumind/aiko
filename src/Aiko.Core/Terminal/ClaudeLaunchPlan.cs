namespace Aiko.Core.Terminal;

/// One environment variable of the Claude Code the terminal starts. A null value means the
/// variable is removed, not set to an empty string.
public sealed record VariableChange(string Name, string? Value);

/// What the terminal needs to know to start Claude Code for one conversation.
public sealed record LaunchRequest(
    string ConfigDirectory,
    string UserProfile,
    string EnvironmentName,
    string SessionId,
    bool Started,
    PermissionMode Mode,
    string? Model);

/// How the terminal starts Claude Code for one conversation: the arguments and what changes in
/// the environment of the process (D-297, D-304).
public sealed record ClaudeLaunchPlan(IReadOnlyList<string> Arguments, IReadOnlyList<VariableChange> Variables)
{
    public const string ConfigVariableName = "CLAUDE_CONFIG_DIR";

    /// Claude Code answers in JSON lines, asks the terminal for permissions and sends an answer
    /// piece by piece.
    private static readonly string[] ChannelArguments =
    [
        "-p",
        "--input-format", "stream-json",
        "--output-format", "stream-json",
        "--verbose",
        "--include-partial-messages",
        "--permission-prompt-tool", "stdio",
    ];

    /// inheritedVariables are the names in the environment of Aiko itself.
    public static ClaudeLaunchPlan For(LaunchRequest request, IEnumerable<string> inheritedVariables)
    {
        var arguments = new List<string>(ChannelArguments);

        // A conversation gets its id from Aiko before the first turn, so the id is known even when
        // Claude Code never answers. After that the same id brings the conversation back.
        arguments.Add(request.Started ? "--resume" : "--session-id");
        arguments.Add(request.SessionId);

        arguments.Add("--permission-mode");
        arguments.Add(PermissionModes.FlagValue(request.Mode));

        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            arguments.Add("--model");
            arguments.Add(request.Model.Trim());
        }

        var variables = new List<VariableChange>();
        foreach (var name in inheritedVariables)
        {
            if (IsLeftover(name))
            {
                variables.Add(new VariableChange(name, null));
            }
        }

        // Environment 1 runs without the variable, like a plain claude and the VS Code panel (D-156).
        if (!ClaudeConfigFolder.IsDefault(request.ConfigDirectory, request.UserProfile))
        {
            variables.Add(new VariableChange(ConfigVariableName, request.ConfigDirectory));
        }

        // The session start hook reads these two. "terminal" keeps its reminder quiet: the form of
        // a new session already says which environment a folder belongs to.
        variables.Add(new VariableChange(SessionReminder.EnvironmentVariable, request.EnvironmentName));
        variables.Add(new VariableChange(SessionReminder.LaunchVariable, SessionReminder.LaunchedByTerminal));

        return new ClaudeLaunchPlan(arguments, variables);
    }

    /// What another Claude Code or another Aiko left in the environment. Aiko may itself be
    /// started from a session, and those variables would send the new one to the wrong place.
    private static bool IsLeftover(string name) =>
        name.Equals("CLAUDECODE", StringComparison.OrdinalIgnoreCase)
        || name.Equals(ConfigVariableName, StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("CLAUDE_CODE_", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("AIKO_", StringComparison.OrdinalIgnoreCase);
}
