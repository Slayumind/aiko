import Foundation
import Testing

@testable import AikoKit

struct SkillCatalogTests {
    @Test
    func everySkillHasAShortNameAndIsListedOnce() throws {
        let shortName = try NSRegularExpression(pattern: "^[a-z0-9]+(-[a-z0-9]+)*$")

        #expect(SkillCatalog.all.count == 13)
        for name in SkillCatalog.all {
            #expect(shortName.firstMatch(in: name, range: NSRange(location: 0, length: name.utf16.count)) != nil)
            #expect(!name.hasPrefix("aiko-"))
        }
        #expect(SkillCatalog.all.count == Set(SkillCatalog.all).count)
    }

    @Test
    func everySkillIsInExactlyOneDomainAndTheListFollowsTheDomains() {
        #expect(SkillCatalog.groups.map(\.domain) == [.projects, .games])
        for group in SkillCatalog.groups {
            #expect(!group.skills.isEmpty)
        }
        #expect(SkillCatalog.groups.flatMap(\.skills) == SkillCatalog.all)
        #expect(SkillCatalog.groups[0].skills.contains("calendar"))
        #expect(SkillCatalog.groups[1].skills.contains("playtest"))
    }

    @Test
    func aSkillIsCalledThroughThePluginName() {
        #expect(SkillCatalog.call("copy") == "/aiko:copy")
        #expect(SkillCatalog.call("gamedesign-research") == "/aiko:gamedesign-research")
    }
}
