import Foundation

public enum NameProblem: Sendable, Equatable {
    case none
    case empty
    case tooLong
    case taken
}

public struct Renamed: Sendable, Equatable {
    public let settings: EnvironmentSettings
    public let suggestedCommand: String?

    public init(_ settings: EnvironmentSettings, _ suggestedCommand: String?) {
        self.settings = settings
        self.suggestedCommand = suggestedCommand
    }
}

public struct FolderBinding: Sendable, Equatable {
    public let folder: String
    public let environment: String

    public init(_ folder: String, _ environment: String) {
        self.folder = folder
        self.environment = environment
    }
}

/// What one change in the settings window does to the environments (D-178, D-179, D-180).
///
/// Every change is applied at once and saved, so each one has to leave a list that makes sense on
/// its own: the ring and the default follow a renamed environment, a folder is bound to one
/// environment at most, and the environment in .claude cannot be removed.
public enum EnvironmentEdits {
    public static let maxNameLength = 40

    public static func checkName(
        _ settings: EnvironmentSettings, _ current: String, _ wanted: String
    ) -> NameProblem {
        let name = wanted.trimmingCharacters(in: .whitespacesAndNewlines)
        if name.isEmpty {
            return .empty
        }

        if name.count > maxNameLength {
            return .tooLong
        }

        return settings.environments.contains {
            $0.name != current && $0.name.caseInsensitiveCompare(name) == .orderedSame
        } ? .taken : .none
    }

    /// Once the commands are installed, terminals and scripts know them by name, so a rename keeps
    /// the command and only suggests the new one (D-180). Before that the command follows the name.
    public static func rename(
        _ settings: EnvironmentSettings, _ current: String, _ wanted: String, commandsInstalled: Bool
    ) -> Renamed {
        let name = wanted.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let environment = find(settings, current),
              name != current,
              checkName(settings, current, name) == .none else {
            return Renamed(settings, nil)
        }

        let kept = environment.command
        let byName = LaunchCommand.fromEnvironmentName(name)
        var renamed = environment
        renamed.name = name
        if commandsInstalled {
            renamed.customCommand = customOrNil(name, kept)
        }

        var updated = settings
        updated.environments = settings.environments.map { $0 == environment ? renamed : $0 }
        if settings.ringEnvironment == current {
            updated.ringEnvironment = name
        }
        if settings.defaultEnvironment == current {
            updated.defaultEnvironment = name
        }

        let suggestion = commandsInstalled && byName != kept && checkCommand(updated, name, byName) == .none
            ? byName
            : nil

        return Renamed(updated, suggestion)
    }

    /// The command an environment brings into the checklist as the person's own. Once commands are
    /// installed the one in use is kept, the same rule as a rename in settings (D-180), so going
    /// through the checklist again cannot quietly turn aiko-work into aiko-work-kodland.
    public static func startingCommand(_ existing: AikoEnvironment?, commandsInstalled: Bool) -> String? {
        guard let existing else { return nil }
        return existing.customCommand ?? (commandsInstalled ? existing.command : nil)
    }

    public static func checkCommand(
        _ settings: EnvironmentSettings, _ environment: String, _ command: String
    ) -> CommandProblem {
        LaunchCommand.check(command, settings.environments.filter { $0.name != environment }.map(\.command))
    }

    public static func setCommand(
        _ settings: EnvironmentSettings, _ environment: String, _ command: String
    ) -> EnvironmentSettings {
        let wanted = command.trimmingCharacters(in: .whitespacesAndNewlines)
        if checkCommand(settings, environment, wanted) != .none {
            return settings
        }

        return change(settings, environment) { environment in
            var copy = environment
            copy.customCommand = customOrNil(environment.name, wanted)
            return copy
        }
    }

    public static func setDirectMode(
        _ settings: EnvironmentSettings, _ environment: String, _ on: Bool
    ) -> EnvironmentSettings {
        change(settings, environment) { environment in
            var copy = environment
            copy.directMode = on
            return copy
        }
    }

    public static func setPersona(
        _ settings: EnvironmentSettings, _ environment: String, _ on: Bool
    ) -> EnvironmentSettings {
        change(settings, environment) { environment in
            var copy = environment
            copy.persona = on
            return copy
        }
    }

    public static func setDefault(_ settings: EnvironmentSettings, _ environment: String) -> EnvironmentSettings {
        guard find(settings, environment) != nil else { return settings }
        var copy = settings
        copy.defaultEnvironment = environment
        return copy
    }

    /// Every bound folder with its environment, sorted by folder. The order does not depend on the
    /// environment, so a row stays where it is when its environment changes.
    public static func bindings(_ settings: EnvironmentSettings) -> [FolderBinding] {
        settings.environments
            .flatMap { environment in environment.projectFolders.map { FolderBinding($0, environment.name) } }
            .enumerated()
            .sorted { left, right in
                let order = left.element.folder.caseInsensitiveCompare(right.element.folder)
                return order == .orderedSame ? left.offset < right.offset : order == .orderedAscending
            }
            .map(\.element)
    }

    /// Binds the folder to this environment and takes it away from any other one.
    public static func bind(
        _ settings: EnvironmentSettings, _ folder: String, _ environment: String
    ) -> EnvironmentSettings {
        guard find(settings, environment) != nil,
              !folder.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return settings
        }

        var copy = settings
        copy.environments = settings.environments.map { each in
            var next = each
            let others = each.projectFolders.filter { !RealClaude.sameFolder($0, folder) }
            next.projectFolders = each.name == environment ? others + [folder] : others
            return next
        }
        return copy
    }

    public static func unbind(_ settings: EnvironmentSettings, _ folder: String) -> EnvironmentSettings {
        var copy = settings
        copy.environments = settings.environments.map { each in
            var next = each
            next.projectFolders = each.projectFolders.filter { !RealClaude.sameFolder($0, folder) }
            return next
        }
        return copy
    }

    /// A new folder goes to the environment that is not the default: binding a folder to the
    /// environment it already runs in would change nothing.
    public static func forNewBinding(
        _ platform: PlatformConventions, _ settings: EnvironmentSettings, _ userProfile: String
    ) -> AikoEnvironment? {
        let fallback = settings.defaultEnvironmentIn(platform, userProfile)
        return settings.environments.first { $0.name != fallback?.name } ?? fallback
    }

    /// The .claude folder belongs to VS Code and Claude Desktop too, so Aiko never lets it go.
    public static func canRemove(
        _ platform: PlatformConventions, _ settings: EnvironmentSettings, _ environment: String, _ userProfile: String
    ) -> Bool {
        guard let found = find(settings, environment) else { return false }
        return found != settings.first(platform, userProfile)
    }

    public static func remove(
        _ platform: PlatformConventions, _ settings: EnvironmentSettings, _ environment: String, _ userProfile: String
    ) -> EnvironmentSettings {
        guard canRemove(platform, settings, environment, userProfile) else {
            return settings
        }

        var copy = settings
        copy.environments = settings.environments.filter { $0.name != environment }
        if settings.ringEnvironment == environment {
            copy.ringEnvironment = nil
        }
        if settings.defaultEnvironment == environment {
            copy.defaultEnvironment = nil
        }
        return copy
    }

    private static func find(_ settings: EnvironmentSettings, _ name: String) -> AikoEnvironment? {
        settings.environments.first { $0.name == name }
    }

    private static func change(
        _ settings: EnvironmentSettings,
        _ environment: String,
        _ change: (AikoEnvironment) -> AikoEnvironment
    ) -> EnvironmentSettings {
        guard find(settings, environment) != nil else { return settings }
        var copy = settings
        copy.environments = settings.environments.map { $0.name == environment ? change($0) : $0 }
        return copy
    }

    /// A command equal to the one the name makes is stored as none, so the file stays as short as it was.
    private static func customOrNil(_ name: String, _ command: String) -> String? {
        command == LaunchCommand.fromEnvironmentName(name) ? nil : command
    }
}
