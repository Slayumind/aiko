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
    string? Model)
{
    /// The environment turned on the to-do list of the agent (D-344).
    public bool AgentTodos { get; init; }
}

/// How the terminal starts Claude Code for one conversation: the arguments and what changes in
/// the environment of the process (D-297, D-304).
public sealed record ClaudeLaunchPlan(IReadOnlyList<string> Arguments, IReadOnlyList<VariableChange> Variables)
{
    public const string ConfigVariableName = "CLAUDE_CONFIG_DIR";

    /// Gives newer models the to-do list tools that Claude Code leaves out for them (RESEARCH).
    public const string TodoToolsVariableName = "CLAUDE_CODE_ENABLE_TODO_TOOLS";

    /// Claude Code answers in JSON lines, asks the terminal for permissions and sends an answer
    /// piece by piece.
    private static readonly string[] ChannelArguments =
    [
        "-p",
        "--input-format", "stream-json",
        "--output-format", "stream-json",
        "--verbose",
        "--include-partial-messages",
        // Says back each message when it takes it, so a message sent during the work stops being
        // grey at that moment (D-341; spike of 2026-10-26).
        "--replay-user-messages",
        "--permission-prompt-tool", "stdio",
    ];

    /// A short Claude Code that only lists its commands for the menu of "/" (AIKO-330). It has no
    /// session, so no file of a conversation is left, and its hooks are off, so the bridge of Aiko
    /// does not count it as a session: it answers "initialize" in under a second (RESEARCH).
    public static ClaudeLaunchPlan ForCommands(string configDirectory, string userProfile, IEnumerable<string> inheritedVariables)
    {
        string[] arguments =
        [
            "-p",
            "--input-format", "stream-json",
            "--output-format", "stream-json",
            "--verbose",
            "--settings", """{"disableAllHooks":true}""",
        ];

        var variables = Leftovers(inheritedVariables);
        if (!ClaudeConfigFolder.IsDefault(configDirectory, userProfile))
        {
            variables.Add(new VariableChange(ConfigVariableName, configDirectory));
        }

        return new ClaudeLaunchPlan(arguments, variables);
    }

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

        var variables = Leftovers(inheritedVariables);

        // Environment 1 runs without the variable, like a plain claude and the VS Code panel (D-156).
        if (!ClaudeConfigFolder.IsDefault(request.ConfigDirectory, request.UserProfile))
        {
            variables.Add(new VariableChange(ConfigVariableName, request.ConfigDirectory));
        }

        // The session start hook reads these two. "terminal" keeps its reminder quiet: the form of
        // a new session already says which environment a folder belongs to.
        variables.Add(new VariableChange(SessionReminder.EnvironmentVariable, request.EnvironmentName));
        variables.Add(new VariableChange(SessionReminder.LaunchVariable, SessionReminder.LaunchedByTerminal));

        if (request.AgentTodos)
        {
            variables.Add(new VariableChange(TodoToolsVariableName, "1"));
        }

        return new ClaudeLaunchPlan(arguments, variables);
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
