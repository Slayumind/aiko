import Foundation

public enum ConfigVariable: Sendable, Equatable {
    /// Leave CLAUDE_CONFIG_DIR as the terminal has it: Aiko knows nothing better.
    case keep

    /// Set it to the folder of the environment.
    case set

    /// Remove it, so Claude Code uses .claude the way it does with no variable at all. Setting it
    /// to .claude by hand is not the same: Claude Code then keeps .claude.json in another place.
    case clear
}

/// What the shim does with one start of Claude Code.
public struct ShimPlan: Sendable, Equatable {
    public let environment: AikoEnvironment?
    public let variable: ConfigVariable
    public let configDirectory: String?
    public let isExplicit: Bool

    public init(_ environment: AikoEnvironment?, _ variable: ConfigVariable, _ configDirectory: String?, _ isExplicit: Bool) {
        self.environment = environment
        self.variable = variable
        self.configDirectory = configDirectory
        self.isExplicit = isExplicit
    }

    public static let passThrough = ShimPlan(nil, .keep, nil, false)
}

/// Decides which environment a start of Claude Code belongs to.
///
/// The shim is one program under several names. Started as "claude", it follows the project
/// folder bindings. Started as a command such as "aiko-work", it runs that environment whatever
/// the folder says: an explicit command wins over a binding (D-159).
public enum ShimLaunch {
    public static let claudeName = "claude"

    public static func decide(
        _ platform: PlatformConventions,
        invokedAs: String,
        workingDirectory: String,
        settings: EnvironmentSettings,
        userProfile: String
    ) -> ShimPlan {
        let name = PathText.fileNameWithoutExtension(invokedAs)

        if name.caseInsensitiveCompare(claudeName) == .orderedSame {
            let bound = ProjectBinding.environmentFor(
                platform, workingDirectory: workingDirectory, settings: settings, userProfile: userProfile)
            return planFor(platform, bound, userProfile, isExplicit: false)
        }

        let chosen = settings.environments.first { $0.command.caseInsensitiveCompare(name) == .orderedSame }

        // A command left over from a renamed or removed environment. Starting Claude Code as it
        // is beats refusing to start at all.
        return chosen == nil ? .passThrough : planFor(platform, chosen, userProfile, isExplicit: true)
    }

    private static func planFor(
        _ platform: PlatformConventions,
        _ environment: AikoEnvironment?,
        _ userProfile: String,
        isExplicit: Bool
    ) -> ShimPlan {
        guard let environment, let folder = environment.configDirectories.first else {
            return .passThrough
        }

        return ClaudeConfigFolder.isDefault(platform, folder, userProfile)
            ? ShimPlan(environment, .clear, nil, isExplicit)
            : ShimPlan(environment, .set, folder, isExplicit)
    }
}
