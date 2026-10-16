import Foundation

/// What the terminal needs to know to start Claude Code for one conversation.
public struct LaunchRequest: Sendable, Equatable {
    public let configDirectory: String
    public let userProfile: String
    public let environmentName: String
    public let sessionId: String
    public let started: Bool
    public let mode: PermissionMode
    public let model: String?

    public init(
        configDirectory: String,
        userProfile: String,
        environmentName: String,
        sessionId: String,
        started: Bool,
        mode: PermissionMode,
        model: String?
    ) {
        self.configDirectory = configDirectory
        self.userProfile = userProfile
        self.environmentName = environmentName
        self.sessionId = sessionId
        self.started = started
        self.mode = mode
        self.model = model
    }
}

/// How the terminal starts Claude Code for one conversation: the arguments and what changes in
/// the environment of the process (D-297, D-304).
public struct ClaudeLaunchPlan: Sendable, Equatable {
    public let arguments: [String]
    public let variables: [VariableChange]

    public init(arguments: [String], variables: [VariableChange]) {
        self.arguments = arguments
        self.variables = variables
    }

    /// Claude Code answers in JSON lines, asks the terminal for permissions and sends an answer
    /// piece by piece.
    private static let channelArguments = [
        "-p",
        "--input-format", "stream-json",
        "--output-format", "stream-json",
        "--verbose",
        "--include-partial-messages",
        "--permission-prompt-tool", "stdio",
    ]

    /// A short Claude Code that only lists its commands for the menu of "/" (AIKO-330). It has no
    /// session, so no file of a conversation is left, and its hooks are off, so the bridge of Aiko
    /// does not count it as a session: it answers "initialize" in under a second (RESEARCH).
    public static func forCommands(
        _ platform: PlatformConventions, configDirectory: String, userProfile: String, inheritedVariables: [String]
    ) -> ClaudeLaunchPlan {
        let arguments = [
            "-p",
            "--input-format", "stream-json",
            "--output-format", "stream-json",
            "--verbose",
            "--settings", #"{"disableAllHooks":true}"#,
        ]

        var variables = inheritedVariables.filter(isLeftover).map { VariableChange($0, nil) }
        if !ClaudeConfigFolder.isDefault(platform, configDirectory, userProfile) {
            variables.append(VariableChange(ClaudeConfigFolder.variableName, configDirectory))
        }
        return ClaudeLaunchPlan(arguments: arguments, variables: variables)
    }

    /// inheritedVariables are the names in the environment of Aiko itself.
    public static func forRequest(
        _ platform: PlatformConventions, _ request: LaunchRequest, inheritedVariables: [String]
    ) -> ClaudeLaunchPlan {
        var arguments = channelArguments

        // A conversation gets its id from Aiko before the first turn, so the id is known even when
        // Claude Code never answers. After that the same id brings the conversation back.
        arguments.append(request.started ? "--resume" : "--session-id")
        arguments.append(request.sessionId)

        arguments.append("--permission-mode")
        arguments.append(PermissionModes.flagValue(request.mode))

        let model = (request.model ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if !model.isEmpty {
            arguments.append("--model")
            arguments.append(model)
        }

        var variables = inheritedVariables.filter(isLeftover).map { VariableChange($0, nil) }

        // Environment 1 runs without the variable, like a plain claude and the VS Code panel (D-156).
        if !ClaudeConfigFolder.isDefault(platform, request.configDirectory, request.userProfile) {
            variables.append(VariableChange(ClaudeConfigFolder.variableName, request.configDirectory))
        }

        // The session start hook reads these two. "terminal" keeps its reminder quiet: the form of
        // a new session already says which environment a folder belongs to.
        variables.append(VariableChange(SessionReminder.environmentVariable, request.environmentName))
        variables.append(VariableChange(SessionReminder.launchVariable, SessionReminder.launchedByTerminal))

        return ClaudeLaunchPlan(arguments: arguments, variables: variables)
    }

    /// What another Claude Code or another Aiko left in the environment. Aiko may itself be
    /// started from a session, and those variables would send the new one to the wrong place.
    private static func isLeftover(_ name: String) -> Bool {
        let upper = name.uppercased()
        return upper == "CLAUDECODE"
            || upper == ClaudeConfigFolder.variableName
            || upper.hasPrefix("CLAUDE_CODE_")
            || upper.hasPrefix("AIKO_")
    }
}
