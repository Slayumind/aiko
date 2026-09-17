import Foundation

public enum PluginStepKind: Sendable, Equatable {
    case addMarketplace
    case removeMarketplace
    case install
    case enable
    case disable
    case update
    case uninstall
}

public struct PluginStep: Sendable, Equatable {
    public let kind: PluginStepKind
    public let target: String

    public init(_ kind: PluginStepKind, _ target: String) {
        self.kind = kind
        self.target = target
    }

    /// The claude command line for this step. User scope, so the plugin follows the account folder.
    public var arguments: [String] {
        switch kind {
        case .addMarketplace: return ["plugin", "marketplace", "add", target]
        case .removeMarketplace: return ["plugin", "marketplace", "remove", target]
        case .install: return ["plugin", "install", target, "-y", "--scope", "user"]
        case .enable: return ["plugin", "enable", target, "--scope", "user"]
        case .disable: return ["plugin", "disable", target, "--scope", "user"]
        case .uninstall: return ["plugin", "uninstall", target, "--scope", "user"]
        case .update: return ["plugin", "update", target, "-y", "--scope", "user"]
        }
    }
}

/// What one Claude Code folder has of Aiko's plugins right now. Other plugins are not looked at:
/// they are the person's own and Aiko never touches them.
public struct PluginState: Sendable, Equatable {
    public let marketplaceFolder: String?
    public let installed: Set<String>
    public let enabled: Set<String>

    public init(marketplaceFolder: String?, installed: Set<String>, enabled: Set<String>) {
        self.marketplaceFolder = marketplaceFolder
        self.installed = installed
        self.enabled = enabled
    }

    public static let nothing = PluginState(marketplaceFolder: nil, installed: [], enabled: [])

    /// Reads settings.json, plugins/installed_plugins.json and plugins/known_marketplaces.json.
    /// A missing or broken file reads as nothing of ours there.
    public static func read(
        settingsJson: String?, installedJson: String?, knownMarketplacesJson: String?
    ) -> PluginState {
        PluginState(
            marketplaceFolder: marketplaceIn(knownMarketplacesJson),
            installed: installedIn(installedJson),
            enabled: enabledIn(settingsJson))
    }

    private static func marketplaceIn(_ json: String?) -> String? {
        parse(json)?[AikoMarketplaceIds.name]?.objectValue?["installLocation"]?.stringValue
    }

    private static func installedIn(_ json: String?) -> Set<String> {
        var ours: Set<String> = []
        guard let plugins = parse(json)?["plugins"]?.objectValue else {
            return ours
        }

        for member in plugins.members where AikoMarketplaceIds.isOurs(member.key) {
            let entries = member.value.arrayValue ?? []
            if entries.contains(where: { $0["scope"]?.stringValue == "user" }) {
                ours.insert(member.key)
            }
        }

        return ours
    }

    private static func enabledIn(_ json: String?) -> Set<String> {
        var ours: Set<String> = []
        guard let enabled = parse(json)?["enabledPlugins"]?.objectValue else {
            return ours
        }

        for member in enabled.members where AikoMarketplaceIds.isOurs(member.key) {
            if member.value.boolValue == true {
                ours.insert(member.key)
            }
        }

        return ours
    }

    private static func parse(_ json: String?) -> JsonObject? {
        guard let json, !json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return nil
        }
        return JsonNode.parse(json)?.objectValue
    }
}

/// Which of Aiko's plugins a folder should have, and the steps from what it has (D-197, D-234).
public enum PluginPlan {
    /// The persona, and the skills plugin while the skills are on and this Aiko ships any. Only where
    /// the persona is on. EnvironmentSettings is not ported yet, so the flag comes in on its own.
    public static func desired(personaOn: Bool, persona: PersonaSettings, skills: [String]) -> Set<String> {
        var desired: Set<String> = []
        if !personaOn {
            return desired
        }

        desired.insert(AikoMarketplaceIds.pluginId(PersonaPlugin.name))
        if persona.skillsOn && !skills.isEmpty {
            desired.insert(AikoMarketplaceIds.pluginId(SkillCatalog.pluginName))
        }

        return desired
    }

    /// A plugin of ours that this Aiko no longer ships, like the one-skill plugins before D-234.
    public static func isRetired(_ pluginId: String) -> Bool {
        AikoMarketplaceIds.isOurs(pluginId)
            && pluginId != AikoMarketplaceIds.pluginId(PersonaPlugin.name)
            && pluginId != AikoMarketplaceIds.pluginId(SkillCatalog.pluginName)
    }

    public static func steps(
        desired: Set<String>, state: PluginState, marketplaceFolder: String
    ) -> [PluginStep] {
        var steps: [PluginStep] = []

        if !desired.isEmpty {
            if state.marketplaceFolder == nil {
                steps.append(PluginStep(.addMarketplace, marketplaceFolder))
            } else if !sameFolder(state.marketplaceFolder!, marketplaceFolder) {
                // A marketplace of the same name from another place, for example a build run from
                // the source folder. Claude Code keeps one place per name.
                steps.append(PluginStep(.removeMarketplace, AikoMarketplaceIds.name))
                steps.append(PluginStep(.addMarketplace, marketplaceFolder))
            }
        }

        for id in ordered(desired) {
            if !state.installed.contains(id) {
                steps.append(PluginStep(.install, id))
            } else if !state.enabled.contains(id) {
                steps.append(PluginStep(.enable, id))
            }
        }

        // Turned off, never uninstalled: the files stay, and turning the persona on again is quick.
        let toDisable = state.enabled.filter { !desired.contains($0) && !(isRetired($0) && state.installed.contains($0)) }
        for id in ordered(toDisable) {
            steps.append(PluginStep(.disable, id))
        }

        // A retired plugin is the exception: nothing would ever turn it on again. Claude Code removes
        // it even after it left the marketplace (checked 2026-09-17).
        for id in ordered(state.installed.filter(isRetired)) {
            steps.append(PluginStep(.uninstall, id))
        }

        return steps
    }

    /// Everything of Aiko's that Claude Code keeps for a folder, for when Aiko or the environment
    /// goes. The plugins first: without the marketplace Claude Code may not know how to remove them.
    public static func removal(_ state: PluginState) -> [PluginStep] {
        var steps = ordered(state.installed).map { PluginStep(.uninstall, $0) }

        if state.marketplaceFolder != nil {
            steps.append(PluginStep(.removeMarketplace, AikoMarketplaceIds.name))
        }

        return steps
    }

    /// Ordinal order, as StringComparer.Ordinal sorts on Windows.
    private static func ordered(_ ids: Set<String>) -> [String] {
        ids.sorted { Array($0.utf8).lexicographicallyPrecedes(Array($1.utf8)) }
    }

    private static func sameFolder(_ a: String, _ b: String) -> Bool {
        trimmed(a).caseInsensitiveCompare(trimmed(b)) == .orderedSame
    }

    private static func trimmed(_ path: String) -> String {
        var text = path
        while text.count > 1, text.hasSuffix("/") || text.hasSuffix("\\") {
            text.removeLast()
        }
        return text
    }
}
