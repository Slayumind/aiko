import Foundation

/// One fact as the settings window shows it. `tooLong` marks a fact that the model does not get:
/// it can only come from a file edited by hand, and the window asks the person to shorten it.
public struct MemoryPageFact: Sendable, Equatable {
    public let fact: MemoryFact

    /// In UTF-16 units, as MemoryRules counts it.
    public let length: Int

    public init(fact: MemoryFact, length: Int) {
        self.fact = fact
        self.length = length
    }

    public var tooLong: Bool { !MemoryRules.fits(length) }
}

/// One category with its count against its quota.
public struct MemoryPageSection: Sendable, Equatable {
    public let category: MemoryCategory
    public let quota: Int
    public let facts: [MemoryPageFact]

    public init(category: MemoryCategory, quota: Int, facts: [MemoryPageFact]) {
        self.category = category
        self.quota = quota
        self.facts = facts
    }

    public var count: Int { facts.count }

    /// No place for a fact added by hand. A file edited by hand may hold more than the quota.
    public var full: Bool { count >= quota }
}

/// What the memory block of the settings window shows: every number and mark on it. The window
/// only draws this, so the decisions can be tested without a window.
public struct MemoryPage: Sendable, Equatable {
    public let sections: [MemoryPageSection]
    public let setAside: Int

    public init(sections: [MemoryPageSection], setAside: Int) {
        self.sections = sections
        self.setAside = setAside
    }

    public var count: Int { sections.reduce(0) { $0 + $1.count } }

    public var capacity: Int { sections.reduce(0) { $0 + $1.quota } }

    /// Facts the model does not get because they are over the limit.
    public var tooLong: Int { sections.reduce(0) { $0 + $1.facts.filter(\.tooLong).count } }

    /// Nothing to show and nothing to erase.
    public var empty: Bool { count == 0 && setAside == 0 }

    public static func from(_ memory: MemoryFile) -> MemoryPage {
        MemoryPage(
            sections: MemoryRules.categories.map { category in
                MemoryPageSection(
                    category: category,
                    quota: MemoryRules.quota(category),
                    facts: memory.inCategory(category).map {
                        MemoryPageFact(fact: $0, length: MemoryRules.lengthOf($0))
                    })
            },
            setAside: memory.setAside.count)
    }

    /// The count under an edit field, for text that is not saved yet. Keys count too: they are a
    /// part of the same limit, and the person cannot see them shrink the room.
    public static func lengthOf(_ text: String?, _ meaning: String?, _ editing: MemoryFact? = nil) -> Int {
        MemoryRules.lengthOf(MemoryText.oneLine(text), MemoryText.oneLine(meaning), editing?.keys ?? [])
    }
}
