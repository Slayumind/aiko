import Foundation
import Testing

@testable import AikoKit

struct SkillPluginTests {
    private func file(_ path: String, _ text: String) -> (path: String, content: Data) {
        (path, Data(text.utf8))
    }

    @Test
    func theHashChangesWithAnyFileAndNotWithTheOrderOrTheSlashes() {
        let files = [file("skills/copy/SKILL.md", "a"), file(".claude-plugin/plugin.json", "{}")]

        let hash = SkillPlugin.contentHash(files)

        #expect(hash.count == 12)
        #expect(hash.allSatisfy { $0.isHexDigit && !$0.isUppercase })
        #expect(hash == SkillPlugin.contentHash(files.reversed()))
        #expect(
            hash
                == SkillPlugin.contentHash([
                    file(#"skills\copy\SKILL.md"#, "a"), file(#".claude-plugin\plugin.json"#, "{}"),
                ]))
        #expect(
            hash
                != SkillPlugin.contentHash([
                    file("skills/copy/SKILL.md", "b"), file(".claude-plugin/plugin.json", "{}"),
                ]))
    }

    @Test
    func theCopyCarriesThePluginVersionWithTheHash() throws {
        let manifest = try #require(
            SkillPlugin.versionedManifest(#"{ "name": "aiko", "version": "1.2.0", "license": "Apache-2.0" }"#, "0123456789ab"))

        let root = try #require(JsonNode.parse(manifest)?.objectValue)
        #expect(root["version"]?.stringValue == "1.2.0+0123456789ab")
        #expect(root["license"]?.stringValue == "Apache-2.0")
        #expect(SkillPlugin.versionOf(manifest) == "1.2.0+0123456789ab")

        // Copied again, the old hash is replaced, not added to.
        #expect(SkillPlugin.versionOf(SkillPlugin.versionedManifest(manifest, "ffffffffffff")) == "1.2.0+ffffffffffff")
        #expect(
            SkillPlugin.versionOf(SkillPlugin.versionedManifest(#"{ "name": "aiko" }"#, "0123456789ab"))
                == "0.0.0+0123456789ab")
        #expect(SkillPlugin.versionedManifest("not json", "0123456789ab") == nil)
    }

    @Test
    func onlyTheSkillsPluginIsReadFromAManifest() {
        #expect(
            SkillPlugin.fromManifest(#"{ "name": "aiko", "description": "Text." }"#)
                == SkillPlugin(name: "aiko", description: "Text."))
        #expect(SkillPlugin.fromManifest(#"{ "name": "aiko-copy" }"#) == nil)
        #expect(SkillPlugin.fromManifest(#"{ "name": "someone-else" }"#) == nil)
        #expect(SkillPlugin.fromManifest(nil) == nil)
    }

    @Test
    func theMarketplaceListsThePersonaAndThenTheSkillsPlugin() {
        let json = AikoMarketplace.json(
            #""x.exe" plugin aiko-persona"#, skills: SkillPlugin(name: "aiko", description: "All skills."))

        let plugins = JsonNode.parse(json)?["plugins"]?.arrayValue ?? []
        #expect(plugins.compactMap { $0["name"]?.stringValue } == ["aiko-persona", "aiko"])
        #expect(plugins[1]["source"]?.stringValue == "./plugins/aiko")
        #expect(JsonNode.parse(AikoMarketplace.json(#""x.exe" plugin aiko-persona"#))?["plugins"]?.arrayValue?.count == 1)
    }

    // ---- the plugin in the repository ----

    static func repository() -> URL {
        var folder = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
        while folder.path != "/" && !FileManager.default.fileExists(atPath: folder.appendingPathComponent("Aiko.slnx").path) {
            folder = folder.deletingLastPathComponent()
        }
        return folder
    }

    static func pluginFolder() -> URL {
        repository().appendingPathComponent(SkillPlugin.folderName).appendingPathComponent(SkillCatalog.pluginName)
    }

    static func shippedSkills() -> [String] {
        let skills = pluginFolder().appendingPathComponent(SkillPlugin.skillsFolder)
        return ((try? FileManager.default.contentsOfDirectory(atPath: skills.path)) ?? [])
            .filter { !$0.hasPrefix(".") }
            .sorted()
    }

    @Test
    func onePluginShipsAndItHoldsEverySkillOfTheCatalog() throws {
        let plugins = try FileManager.default.contentsOfDirectory(
            atPath: Self.repository().appendingPathComponent(SkillPlugin.folderName).path)
        #expect(plugins.filter { !$0.hasPrefix(".") } == [SkillCatalog.pluginName])

        let manifestPath = Self.pluginFolder().appendingPathComponent(".claude-plugin/plugin.json")
        let manifest = SkillPlugin.fromManifest(try String(contentsOf: manifestPath, encoding: .utf8))
        #expect(manifest?.name == SkillCatalog.pluginName)
        let description = (manifest?.description ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        #expect(description.isEmpty == false)

        #expect(Self.shippedSkills() == SkillCatalog.all.sorted())
    }

    @Test(arguments: SkillPluginTests.shippedSkills())
    func aShippedSkillIsNamedAfterItsFolder(name: String) throws {
        let path = Self.pluginFolder()
            .appendingPathComponent(SkillPlugin.skillsFolder)
            .appendingPathComponent(name)
            .appendingPathComponent("SKILL.md")
        let skill = try String(contentsOf: path, encoding: .utf8).replacingOccurrences(of: "\r\n", with: "\n")

        #expect(skill.hasPrefix("---"))
        #expect(skill.contains("\nname: \(name)\n"))
        #expect(skill.contains("\ndescription: "))
        #expect(!skill.lowercased().contains("slayumind"))
    }

    @Test
    func thePublicMarketplaceListsTheOnePlugin() throws {
        let path = Self.repository().appendingPathComponent(".claude-plugin/marketplace.json")
        let json = try #require(JsonNode.parse(try String(contentsOf: path, encoding: .utf8)))
        let listed = (json["plugins"]?.arrayValue ?? []).map {
            ($0["name"]?.stringValue, $0["source"]?.stringValue)
        }

        #expect(listed.count == 1)
        #expect(listed.first?.0 == SkillCatalog.pluginName)
        #expect(listed.first?.1 == "./plugins/\(SkillCatalog.pluginName)")
    }
}
