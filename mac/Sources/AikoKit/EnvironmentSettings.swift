import Foundation

/// One environment: a name the user chose and the Claude Code config folders inside it.
/// Each environment has one folder today, but the core keeps a list from the start.
public struct AikoEnvironment: Sendable, Equatable {
    public var name: String
    public var configDirectories: [String]
    public var directMode: Bool
    public var persona: Bool

    /// A command name the person typed. Nil means the command follows the environment name.
    public var customCommand: String?

    /// Project folders bound to this environment. Claude Code started inside them runs here.
    public var projectFolders: [String]

    public init(
        _ name: String,
        _ configDirectories: [String],
        directMode: Bool = false,
        persona: Bool = false,
        customCommand: String? = nil,
        projectFolders: [String] = []
    ) {
        self.name = name
        self.configDirectories = configDirectories
        self.directMode = directMode
        self.persona = persona
        self.customCommand = customCommand
        self.projectFolders = projectFolders
    }

    public var command: String { customCommand ?? LaunchCommand.fromEnvironmentName(name) }

    public func holds(_ folder: String) -> Bool {
        configDirectories.contains {
            PathText.trimOneEndSeparator($0)
                .caseInsensitiveCompare(PathText.trimOneEndSeparator(folder)) == .orderedSame
        }
    }
}

public struct EnvironmentSettings: Sendable, Equatable {
    public var environments: [AikoEnvironment]

    /// Which environment the ring shows. The click on the tray icon swaps it, so it is a name,
    /// not an index: renaming an environment must not silently point at the other one.
    public var ringEnvironment: String?

    /// Which environment runs in folders bound to none. A name, for the same reason as the ring.
    public var defaultEnvironment: String?

    public init(_ environments: [AikoEnvironment], ringEnvironment: String? = nil, defaultEnvironment: String? = nil) {
        self.environments = environments
        self.ringEnvironment = ringEnvironment
        self.defaultEnvironment = defaultEnvironment
    }

    /// See AppSettings.currentSchema for why this is written from the start.
    /// 2 added custom commands, project folders and the default environment. A file of schema 1
    /// reads as before, with none of them set.
    /// 3 added the persona flag. Older files read with the persona off.
    public static let currentSchema = 3

    /// The tray has a ring and a dot, so Aiko keeps two environments (D-152).
    public static let maxEnvironments = 2

    public static let empty = EnvironmentSettings([])

    public var hasEnvironments: Bool { !environments.isEmpty }

    public var ring: AikoEnvironment? {
        environments.first { $0.name == ringEnvironment } ?? environments.first
    }

    public var dot: AikoEnvironment? {
        environments.count < 2 ? nil : environments.first { $0.name != ring?.name }
    }

    /// The click swaps the ring and the dot; with one environment there is nothing to swap.
    public func swapRing() -> EnvironmentSettings {
        guard let other = dot else { return self }
        var copy = self
        copy.ringEnvironment = other.name
        return copy
    }

    /// Environment 1 is always the one in .claude: the VS Code panel, Claude Desktop and a plain
    /// claude all use that folder, whatever Aiko says (D-156).
    public func first(_ platform: PlatformConventions, _ userProfile: String) -> AikoEnvironment? {
        environments.first { environment in
            environment.configDirectories.contains { ClaudeConfigFolder.isDefault(platform, $0, userProfile) }
        }
    }

    /// The environment beside the one in .claude. Without a .claude environment there is no
    /// second one either: the wizard sets up environment 1 first.
    public func second(_ platform: PlatformConventions, _ userProfile: String) -> AikoEnvironment? {
        first(platform, userProfile) == nil ? nil : Array(kept(platform, userProfile).dropFirst()).first
    }

    /// Environments beyond the two Aiko keeps. A list from before the two-environment rule can
    /// have them. The core never drops one by itself: removing an environment also takes Aiko's
    /// line out of that folder's settings, and that is the person's call in settings.
    public func extras(_ platform: PlatformConventions, _ userProfile: String) -> [AikoEnvironment] {
        let inUse = kept(platform, userProfile)
        return environments.filter { !inUse.contains($0) }
    }

    /// Environment 1 first, then the others with the ring ahead of the rest: the ring was the
    /// person's own choice of what to watch.
    private func kept(_ platform: PlatformConventions, _ userProfile: String) -> [AikoEnvironment] {
        let head = first(platform, userProfile)
        let others = environments
            .filter { $0 != head }
            .enumerated()
            .sorted { left, right in
                let a = left.element.name == ringEnvironment ? 0 : 1
                let b = right.element.name == ringEnvironment ? 0 : 1
                return a == b ? left.offset < right.offset : a < b
            }
            .map(\.element)

        let all = head == nil ? others : [head!] + others
        return Array(all.prefix(EnvironmentSettings.maxEnvironments))
    }

    /// `Default(userProfile)` on Windows; `default` is a keyword in Swift.
    public func defaultEnvironmentIn(_ platform: PlatformConventions, _ userProfile: String) -> AikoEnvironment? {
        environments.first { $0.name == defaultEnvironment }
            ?? first(platform, userProfile)
            ?? environments.first
    }

    // MARK: - the file

    public static func fromJson(_ json: String?) -> EnvironmentSettings {
        guard let json, !json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              let root = JsonNode.parse(json)?.objectValue else {
            return empty
        }

        let read = (root["environments"]?.arrayValue ?? []).compactMap { node -> AikoEnvironment? in
            guard let object = node.objectValue,
                  let name = object["name"]?.stringValue,
                  !name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                  let directories = object["configDirectories"]?.arrayValue,
                  !directories.isEmpty else {
                return nil
            }

            let command = object["command"]?.stringValue
            return AikoEnvironment(
                name,
                directories.compactMap(\.stringValue),
                directMode: object["directMode"]?.boolValue ?? false,
                persona: object["persona"]?.boolValue ?? false,
                customCommand: (command?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ?? true) ? nil : command,
                projectFolders: (object["projectFolders"]?.arrayValue ?? [])
                    .compactMap(\.stringValue)
                    .filter { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty })
        }

        guard !read.isEmpty else {
            return empty
        }

        return EnvironmentSettings(
            read,
            ringEnvironment: root["ringEnvironment"]?.stringValue,
            defaultEnvironment: root["defaultEnvironment"]?.stringValue)
    }

    public func toJson() -> String {
        var root = JsonObject()
        root["schemaVersion"] = .number(String(EnvironmentSettings.currentSchema))
        if let ringName = ringEnvironment ?? ring?.name {
            root["ringEnvironment"] = .string(ringName)
        }
        if let defaultName = defaultEnvironment {
            root["defaultEnvironment"] = .string(defaultName)
        }

        root["environments"] = .array(environments.map { environment in
            var file = JsonObject()
            file["name"] = .string(environment.name)
            file["configDirectories"] = .array(environment.configDirectories.map(JsonNode.string))
            file["directMode"] = .bool(environment.directMode)
            file["persona"] = .bool(environment.persona)
            if let custom = environment.customCommand {
                file["command"] = .string(custom)
            }
            if !environment.projectFolders.isEmpty {
                file["projectFolders"] = .array(environment.projectFolders.map(JsonNode.string))
            }
            return .object(file)
        })

        return JsonNode.object(root).toJsonString(indented: true) + "\n"
    }
}
