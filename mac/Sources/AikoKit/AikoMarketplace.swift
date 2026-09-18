import Foundation

/// Aiko's own plugin marketplace: a folder on this computer with one marketplace.json (D-201, D-234).
/// Claude Code reads a local marketplace in place, so nothing is downloaded from anywhere.
public enum AikoMarketplace {
    public static let name = "aiko"

    public static func pluginId(_ plugin: String) -> String { "\(plugin)@\(name)" }

    public static func isOurs(_ pluginId: String) -> Bool { pluginId.hasSuffix("@" + name) }

    /// Where Claude Code looks for the marketplace file inside a marketplace folder.
    public static func fileIn(_ platform: PlatformConventions, _ marketplaceFolder: String) -> String {
        platform.join(marketplaceFolder, ".claude-plugin", "marketplace.json")
    }

    /// The command Claude Code runs to get the persona plugin. On Windows it names the local data
    /// folder through %LOCALAPPDATA%, which the shell expands, so the command stays plain ASCII even
    /// when the user name has Cyrillic letters or spaces. macOS has no such variable, so the command
    /// spells the real path. Nil when no command fits Claude Code's rules.
    public static func personaCommand(
        _ platform: PlatformConventions, _ bridgePath: String, _ folders: AikoFolders
    ) -> String? {
        var path = bridgePath
        if let variable = platform.localDataVariable,
           bridgePath.lowercased().hasPrefix((folders.installedAppFolder + String(platform.directorySeparator)).lowercased()) {
            let base = trimSeparators(platform, folders.localBase)
            let rest = String(bridgePath.dropFirst(base.count + 1))
            path = variable + String(platform.directorySeparator) + rest
        }

        let command = "\"\(path)\" \(PersonaPluginOutput.verb) \(PersonaPlugin.name)"
        return isValidCommand(command) ? command : nil
    }

    /// Claude Code's rules for a command source: printable ASCII, at most 500 characters, and no
    /// run of four spaces, so the person can read the whole command they are asked to accept.
    public static func isValidCommand(_ command: String) -> Bool {
        !command.isEmpty
            && command.count <= 500
            && command.unicodeScalars.allSatisfy { $0.value >= 0x20 && $0.value <= 0x7E }
            && !command.contains("    ")
    }

    /// The persona from the bridge, and the skills plugin from a folder inside the marketplace.
    public static func json(_ personaCommand: String, skills: SkillPlugin? = nil) -> String {
        var plugins: [JsonNode] = [
            .object(JsonObject([
                ("name", .string(PersonaPlugin.name)),
                ("description", .string("Aiko's persona and the hooks behind her face in the tray.")),
                ("source", .object(JsonObject([
                    ("source", .string("command")),
                    ("command", .string(personaCommand)),
                ]))),
            ]))
        ]

        if let skills {
            plugins.append(.object(JsonObject([
                ("name", .string(skills.name)),
                ("description", .string(skills.description)),
                ("source", .string(skills.source)),
            ])))
        }

        let root = JsonNode.object(JsonObject([
            ("name", .string(name)),
            ("owner", .object(JsonObject([("name", .string("Aiko"))]))),
            ("plugins", .array(plugins)),
        ]))

        // The relaxed encoder, so a person who opens the file can read the command.
        return root.toJsonString(indented: true, escaping: .relaxed) + "\n"
    }

    private static func trimSeparators(_ platform: PlatformConventions, _ folder: String) -> String {
        var text = folder
        while text.last == platform.directorySeparator { text.removeLast() }
        return text
    }
}
