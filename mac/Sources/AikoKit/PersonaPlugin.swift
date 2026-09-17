import CryptoKit
import Foundation

/// The files of the aiko-persona plugin. The bridge writes them into a folder and prints its path,
/// and Claude Code copies that folder into its plugin cache (D-201). The version Claude Code keeps
/// is a hash of this content, so nothing here counts versions by hand.
public enum PersonaPlugin {
    public static let name = "aiko-persona"

    /// The hook argument the bridge answers to (D-206).
    public static let hookArgument = "hook"

    /// The events that change Aiko's face (D-206). Notification is filtered by the bridge, which
    /// keeps only the kinds that mean "waiting for you".
    public static let hookEvents = [
        "UserPromptSubmit", "PostToolUse", "PermissionRequest", "Notification", "Stop", "StopFailure", "SessionEnd",
    ]

    /// Relative path with forward slashes to file content, in the order the paths sort.
    public static func files(_ temperament: Temperament, _ bridgePath: String) -> [(path: String, content: String)] {
        [
            (".claude-plugin/plugin.json", manifest()),
            ("hooks/hooks.json", hooks(bridgePath)),
            ("output-styles/aiko.md", PersonaPrompt.styleFile(temperament)),
        ]
    }

    /// Twelve hex characters of SHA-256 over the paths and contents. The bridge names the folder
    /// after it, so a changed persona lands in a new folder and never half-overwrites the old one.
    public static func contentHash(_ files: [(path: String, content: String)]) -> String {
        var sha = SHA256()
        for file in files.sorted(by: { $0.path < $1.path }) {
            sha.update(data: Data("\(file.path)\n\(file.content)\n".utf8))
        }

        let hex = sha.finalize().map { String(format: "%02x", $0) }.joined()
        return String(hex.prefix(12))
    }

    private static func manifest() -> String {
        let object = JsonObject([
            ("name", .string(name)),
            ("description", .string("Aiko's persona and the hooks behind her face in the tray.")),
        ])
        return JsonNode.object(object).toJsonString(indented: true, escaping: .relaxed) + "\n"
    }

    /// The command and its argument go separately, so no shell reads the path: the install path
    /// has spaces and may have letters that are not ASCII. async keeps the session from waiting.
    private static func hooks(_ bridgePath: String) -> String {
        var events = JsonObject()
        for name in hookEvents {
            events[name] = .array([
                .object(JsonObject([
                    ("hooks", .array([
                        .object(JsonObject([
                            ("type", .string("command")),
                            ("command", .string(bridgePath)),
                            ("args", .array([.string(hookArgument)])),
                            ("async", .bool(true)),
                        ]))
                    ]))
                ]))
            ])
        }

        let root = JsonObject([("hooks", .object(events))])
        return JsonNode.object(root).toJsonString(indented: true, escaping: .relaxed) + "\n"
    }
}
