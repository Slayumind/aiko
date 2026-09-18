import Foundation

/// What has to change in Aiko's command folder so it holds exactly the commands of the environments.
public struct CommandLinksPlan: Sendable, Equatable {
    public let toAdd: [String]
    public let toRemove: [String]

    public init(toAdd: [String], toRemove: [String]) {
        self.toAdd = toAdd
        self.toRemove = toRemove
    }

    public var isEmpty: Bool { toAdd.isEmpty && toRemove.isEmpty }
}

/// The folder holds the shim under Claude Code's own name, and one link per command: aiko-work,
/// aiko-personal (claude.exe, aiko-work.exe on Windows). Renaming an environment, a command typed by
/// hand or a removed environment leaves a link behind or asks for a new one. Anything else in the
/// folder that is not a command of ours is left alone.
public enum CommandLinks {
    public static func shimFileName(_ platform: PlatformConventions) -> String {
        platform.executableName(ShimLaunch.claudeName)
    }

    public static func fileNameFor(_ platform: PlatformConventions, _ command: String) -> String {
        platform.executableName(command)
    }

    public static func plan(
        _ platform: PlatformConventions,
        _ settings: EnvironmentSettings,
        _ filesInFolder: [String]
    ) -> CommandLinksPlan {
        var wanted: [String] = []
        for environment in settings.environments {
            let name = fileNameFor(platform, environment.command)
            if !wanted.contains(where: { $0.caseInsensitiveCompare(name) == .orderedSame }) {
                wanted.append(name)
            }
        }

        let shim = shimFileName(platform)
        let present = filesInFolder
            .map(PathText.fileName)
            .filter { $0.lowercased().hasSuffix(platform.executableSuffix.lowercased()) }
            .filter { $0.caseInsensitiveCompare(shim) != .orderedSame }

        return CommandLinksPlan(
            toAdd: wanted.filter { w in !present.contains { $0.caseInsensitiveCompare(w) == .orderedSame } },
            toRemove: present.filter { p in !wanted.contains { $0.caseInsensitiveCompare(p) == .orderedSame } })
    }
}
