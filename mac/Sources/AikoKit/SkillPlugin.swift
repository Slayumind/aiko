import CryptoKit
import Foundation

/// The one plugin with all of Aiko's skills (D-234). The files ship with Aiko in its "plugins" folder
/// and are copied into the local marketplace, from where Claude Code installs them. The skills are
/// switched on and off together, by enabling the plugin (D-237).
public struct SkillPlugin: Sendable, Equatable {
    public let name: String
    public let description: String

    public init(name: String, description: String) {
        self.name = name
        self.description = description
    }

    public static let folderName = "plugins"

    public static let skillsFolder = "skills"

    /// Where the marketplace lists it: a path inside the marketplace folder.
    public var source: String { "./\(SkillPlugin.folderName)/\(name)" }

    /// Claude Code updates a plugin from a folder only when its version changes. The plugin keeps its
    /// own version and gets the hash of its files as build metadata, so every change of a file is a new
    /// version, and an unchanged copy is never installed again.
    public static func contentHash(_ files: [(path: String, content: Data)]) -> String {
        var sha = SHA256()
        let sorted = files.sorted {
            Array($0.path.replacingOccurrences(of: "\\", with: "/").utf8)
                .lexicographicallyPrecedes(Array($1.path.replacingOccurrences(of: "\\", with: "/").utf8))
        }

        for file in sorted {
            sha.update(data: Data((file.path.replacingOccurrences(of: "\\", with: "/") + "\n").utf8))
            sha.update(data: file.content)
        }

        let hex = sha.finalize().map { String(format: "%02x", $0) }.joined()
        return String(hex.prefix(12))
    }

    /// The manifest with "1.0.0+hash" as its version. Null when the manifest is not a plugin of ours.
    public static func versionedManifest(_ manifestJson: String, _ contentHash: String) -> String? {
        guard var manifest = parse(manifestJson), let nameNode = manifest["name"], isValue(nameNode) else {
            return nil
        }

        let own = manifest["version"]?.stringValue?.components(separatedBy: "+").first ?? "0.0.0"
        manifest["version"] = .string("\(own)+\(contentHash)")
        return JsonNode.object(manifest).toJsonString(indented: true, escaping: .relaxed) + "\n"
    }

    /// The version a copied manifest carries, to see whether the copy is still current.
    public static func versionOf(_ manifestJson: String?) -> String? {
        parse(manifestJson)?["version"]?.stringValue
    }

    /// Name and description from the manifest, for the marketplace list. Null for any other plugin.
    public static func fromManifest(_ manifestJson: String?) -> SkillPlugin? {
        guard let manifest = parse(manifestJson),
              let name = manifest["name"]?.stringValue,
              name == SkillCatalog.pluginName else {
            return nil
        }

        return SkillPlugin(name: name, description: manifest["description"]?.stringValue ?? "")
    }

    private static func isValue(_ node: JsonNode) -> Bool {
        switch node {
        case .object, .array, .null: return false
        default: return true
        }
    }

    private static func parse(_ json: String?) -> JsonObject? {
        guard let json, !json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            return nil
        }
        return JsonNode.parse(json)?.objectValue
    }
}
