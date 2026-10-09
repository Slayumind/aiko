import Foundation

/// What a group of skills is about. The settings page and the site show the skills under these.
public enum SkillDomain: Sendable, Equatable {
    case projects
    case games
}

/// Skills of one domain, in the order they are listed.
public struct SkillGroup: Sendable, Equatable {
    public let domain: SkillDomain
    public let skills: [String]

    public init(domain: SkillDomain, skills: [String]) {
        self.domain = domain
        self.skills = skills
    }
}

/// Aiko's own skills, grouped by domain, in the order the settings page lists them. All of them ship
/// inside one plugin, so Claude Code shows each one as aiko:name (D-234).
public enum SkillCatalog {
    /// The plugin that holds every skill.
    public static let pluginName = "aiko"

    public static let groups: [SkillGroup] = [
        SkillGroup(domain: .projects, skills: ["copy", "docs-hygiene", "release-gate", "calendar", "drive"]),
        SkillGroup(
            domain: .games,
            skills: ["gamedesign-research", "balance", "playtest", "polishing", "blender-to-unity", "texturing", "glb-for-web", "palette"]),
    ]

    /// Every skill, domain by domain.
    public static let all: [String] = groups.flatMap(\.skills)

    /// What the person types to call the skill. The short /name may belong to Claude Code itself or
    /// to a skill of the person's own; the full name always reaches this one.
    public static func call(_ skill: String) -> String { "/\(pluginName):\(skill)" }
}
