import Foundation

/// The four shelves of the memory. Each has its own quota (MemoryRules), so a busy month of
/// projects cannot push out who the person is.
public enum MemoryCategory: Sendable, Equatable, CaseIterable {
    case you
    case work
    case projects
    case interests
}

/// One thing Aiko remembers about the person.
///
/// `seen` is the day the fact last came up, and it decides which fact goes first when a category
/// is full. Keys are the names the person would say when it comes up again: a key in their message
/// moves `seen` by itself (MemoryEdits.touchByPrompt).
public struct MemoryFact: Sendable, Equatable {
    public static let idPrefix = "F"

    /// The day a fact has when nobody gave it one: 0001-01-01, which is DateOnly.MinValue on Windows.
    public static let noDay = DateOnly(1, 1, 1)

    public var id: String
    public var category: MemoryCategory
    public var text: String

    /// What the fact changes in Aiko's answers. Empty when the text says it all.
    public var meaning: String

    /// A pinned fact is never forgotten to make room.
    public var pinned: Bool

    public var keys: [String]
    public var seen: DateOnly
    public var created: DateOnly

    public init(
        _ id: String,
        _ category: MemoryCategory,
        _ text: String,
        meaning: String = "",
        pinned: Bool = false,
        keys: [String] = [],
        seen: DateOnly = MemoryFact.noDay,
        created: DateOnly = MemoryFact.noDay
    ) {
        self.id = id
        self.category = category
        self.text = text
        self.meaning = meaning
        self.pinned = pinned
        self.keys = keys
        self.seen = seen
        self.created = created
    }

    /// 12 for F12. Ids are compared as numbers: as text, F10 would come before F9.
    public var number: Int { MemoryFact.numberOf(id) ?? 0 }

    public static func idFor(_ number: Int) -> String {
        idPrefix + String(number)
    }

    /// Nil for anything that is not F and a number from 1 up, written the one way idFor writes it:
    /// F07 is not an id, or it would be a second fact 7. The number has to fit 32 bits, as on
    /// Windows.
    public static func numberOf(_ id: String?) -> Int? {
        guard let id else {
            return nil
        }

        let bytes = Array(id.utf8)
        let prefix = Array(idPrefix.utf8)
        guard bytes.count > prefix.count, Array(bytes.prefix(prefix.count)) == prefix else {
            return nil
        }

        let digits = bytes.dropFirst(prefix.count)
        guard digits.allSatisfy({ $0 >= UInt8(ascii: "0") && $0 <= UInt8(ascii: "9") }),
              let number = Int32(String(decoding: digits, as: UTF8.self)),
              number > 0,
              Array(idFor(Int(number)).utf8) == bytes else {
            return nil
        }

        return Int(number)
    }
}
