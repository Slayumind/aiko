import Foundation

/// What the terminal needs to know to start Claude Code for one session.
public struct LaunchRequest: Sendable, Equatable {
    public let configDirectory: String
    public let userProfile: String
    public let environmentName: String
    public let sessionId: String
    public let started: Bool

    public init(configDirectory: String, userProfile: String, environmentName: String, sessionId: String, started: Bool) {
        self.configDirectory = configDirectory
        self.userProfile = userProfile
        self.environmentName = environmentName
        self.sessionId = sessionId
        self.started = started
    }
}

/// How the terminal starts a program in a real terminal (D-349, D-360): the arguments of Claude
/// Code and what changes in the environment of the process. Claude Code chooses its own mode and
/// model, so only the id of the conversation is passed.
///
/// The twin of ClaudeLaunchPlan.cs.
public struct ClaudeLaunchPlan: Sendable, Equatable {
    public let arguments: [String]
    public let variables: [VariableChange]

    public init(arguments: [String], variables: [VariableChange]) {
        self.arguments = arguments
        self.variables = variables
    }

    /// inheritedVariables are the names in the environment of Aiko itself.
    public static func forRequest(
        _ platform: PlatformConventions, _ request: LaunchRequest, inheritedVariables: [String]
    ) -> ClaudeLaunchPlan {
        // A conversation gets its id from Aiko before the first turn, so the id is known even when
        // Claude Code never answers. After that the same id brings the conversation back.
        let arguments = [request.started ? "--resume" : "--session-id", request.sessionId]

        var variables = environment(
            platform, configDirectory: request.configDirectory, userProfile: request.userProfile,
            inheritedVariables: inheritedVariables)

        // The session start hook reads these two. "terminal" keeps its reminder quiet: the session
        // was started in the folder and the environment the person chose.
        variables.append(VariableChange(SessionReminder.environmentVariable, request.environmentName))
        variables.append(VariableChange(SessionReminder.launchVariable, SessionReminder.launchedByTerminal))

        return ClaudeLaunchPlan(arguments: arguments, variables: variables)
    }

    /// A shell of a session (D-350): the environment of the session, so a claude typed in it runs
    /// in the same environment, without the variables of the session start hook.
    public static func forShell(
        _ platform: PlatformConventions, configDirectory: String, userProfile: String, inheritedVariables: [String]
    ) -> ClaudeLaunchPlan {
        ClaudeLaunchPlan(
            arguments: [],
            variables: environment(
                platform, configDirectory: configDirectory, userProfile: userProfile,
                inheritedVariables: inheritedVariables))
    }

    private static func environment(
        _ platform: PlatformConventions, configDirectory: String, userProfile: String, inheritedVariables: [String]
    ) -> [VariableChange] {
        var variables = inheritedVariables.filter(isLeftover).map { VariableChange($0, nil) }

        // Environment 1 runs without the variable, like a plain claude and the VS Code panel (D-156).
        if !ClaudeConfigFolder.isDefault(platform, configDirectory, userProfile) {
            variables.append(VariableChange(ClaudeConfigFolder.variableName, configDirectory))
        }

        return variables
    }

    /// What a session of Claude Code leaves for its children (seen in 2.1.312, RESEARCH). Aiko may
    /// itself be started from a session, and these would tie the new one to it. The person's own
    /// settings of Claude Code, such as CLAUDE_CODE_GIT_BASH_PATH, are not among them and stay.
    private static let sessionVariables: Set<String> = [
        "CLAUDECODE", "CLAUDE_PID", "CLAUDE_EFFORT", ClaudeConfigFolder.variableName,
        "CLAUDE_CODE_ENTRYPOINT", "CLAUDE_CODE_CHILD_SESSION", "CLAUDE_CODE_EXECPATH", "CLAUDE_CODE_SSE_PORT",
    ]

    private static let sessionPrefixes = ["CLAUDE_CODE_SESSION_", "CLAUDE_CODE_MESSAGING_", "AIKO_"]

    /// What another Claude Code or another Aiko left in the environment.
    private static func isLeftover(_ name: String) -> Bool {
        let upper = name.uppercased()
        return sessionVariables.contains(upper) || sessionPrefixes.contains { upper.hasPrefix($0) }
    }
}
