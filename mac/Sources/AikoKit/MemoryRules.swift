import Foundation

/// The numbers and shapes of the memory: how many facts a category holds, how long a fact may be,
/// what may be a key and when a key is found in a message.
///
/// The limits are sized so the whole memory fits one hook output, which Claude Code cuts at
/// 10,000 characters: 42 facts of 200 characters and their headings (MemoryPrompt.facts).
public enum MemoryRules {
    /// Text, meaning and keys of one fact together, counted in UTF-16 units as string.Length does
    /// on Windows: an emoji is 2. Claude Code counts the hook output the same way.
    public static let maxLength = 200

    public static let maxKeys = 3

    /// How many more letters a word may have after a key: «маком» is still «мак», «макрос» is not.
    public static let maxKeyTail = 2

    public static let categories: [MemoryCategory] = [.you, .work, .projects, .interests]

    public static func quota(_ category: MemoryCategory) -> Int {
        switch category {
        case .you: return 8
        case .work: return 12
        case .projects: return 12
        case .interests: return 10
        }
    }

    /// The heading the model reads. English whatever the interface language, like the persona.
    public static func title(_ category: MemoryCategory) -> String {
        switch category {
        case .you: return "You"
        case .work: return "How to work with me"
        case .projects: return "Projects"
        case .interests: return "Interests and taste"
        }
    }

    /// The word in the file and in the tool call.
    public static func nameOf(_ category: MemoryCategory) -> String {
        switch category {
        case .you: return "you"
        case .work: return "work"
        case .projects: return "projects"
        case .interests: return "interests"
        }
    }

    public static func categoryOf(_ name: String?) -> MemoryCategory? {
        guard let name else {
            return nil
        }

        let trimmed = MemoryText.trim(name)
        return categories.first { MemoryText.sameIgnoringAsciiCase(nameOf($0), trimmed) }
    }

    public static func lengthOf(_ text: String, _ meaning: String, _ keys: [String]) -> Int {
        text.utf16.count + meaning.utf16.count + keys.reduce(0) { $0 + $1.utf16.count }
    }

    public static func lengthOf(_ fact: MemoryFact) -> Int {
        lengthOf(fact.text, fact.meaning, fact.keys)
    }

    public static func fits(_ length: Int) -> Bool {
        length <= maxLength
    }

    /// Pinned facts never age, and work habits are rules, not topics: neither needs keys.
    public static func takesKeys(_ category: MemoryCategory, pinned: Bool) -> Bool {
        category != .work && !pinned
    }

    /// A key must look like a name or a term: a capital letter first, or a Latin letter, a digit,
    /// #, + or a dot anywhere. A common word as a key would refresh its fact on every other message.
    public static func isNameLike(_ key: String) -> Bool {
        guard let first = key.utf16.first else {
            return false
        }

        let startsWithCapital = Unicode.Scalar(first)?.properties.generalCategory == .uppercaseLetter
        return startsWithCapital || key.utf16.contains(where: isTermUnit)
    }

    /// Keys are trimmed, the ones that are not names are listed apart so the reply can name them,
    /// and the first three good ones are kept.
    public static func cleanKeys(_ keys: [String]?) -> CleanedKeys {
        let given = (keys ?? []).map(MemoryText.trim).filter { !$0.isEmpty }

        return CleanedKeys(
            kept: Array(given.filter(isNameLike).prefix(maxKeys)),
            rejected: given.filter { !isNameLike($0) })
    }

    /// A key is found when it starts a word of the message and the word goes on for at most two
    /// more letters, so a case ending or a short suffix still counts. Letters are any letters,
    /// not only Latin ones, and case does not matter.
    public static func keyMatches(_ key: String, _ prompt: String) -> Bool {
        keyMatches(key, SearchedText(prompt))
    }

    /// The same question for a message that is searched for many keys.
    ///
    /// Windows asks a regular expression, (?<!\w)key\w{0,2}(?!\w). This walks the message by hand
    /// instead, one UTF-16 unit at a time as that expression does: the expressions of Foundation
    /// have another idea of \w and of letters above the basic plane.
    static func keyMatches(_ key: String, _ prompt: SearchedText) -> Bool {
        let word = MemoryText.folded(key)
        guard !word.isEmpty, word.count <= prompt.units.count else {
            return false
        }

        for start in 0...(prompt.units.count - word.count) where prompt.has(word, at: start) {
            if start > 0 && MemoryText.isWordUnit(prompt.units[start - 1]) {
                continue
            }

            // With three or more letters after the key, whatever the tail takes still has a
            // letter behind it, so the word is another word.
            var tail = 0
            var at = start + word.count
            while at < prompt.units.count, tail <= maxKeyTail, MemoryText.isWordUnit(prompt.units[at]) {
                tail += 1
                at += 1
            }

            if tail <= maxKeyTail {
                return true
            }
        }

        return false
    }

    /// The fact that goes first when a category is full: the one not seen for the longest time,
    /// and of two seen on the same day the one with the smaller id. Nil when every fact is pinned.
    public static func oldestUnpinned(_ facts: [MemoryFact]) -> MemoryFact? {
        facts.filter { !$0.pinned }.min { ($0.seen, $0.number) < ($1.seen, $1.number) }
    }

    private static func isTermUnit(_ unit: UInt16) -> Bool {
        (unit | 0x20 >= 0x61 && unit | 0x20 <= 0x7A && unit < 0x80)
            || (unit >= 0x30 && unit <= 0x39)
            || unit == 0x23 || unit == 0x2B || unit == 0x2E
    }
}

/// `rejected` holds the keys that looked like common words.
public struct CleanedKeys: Sendable, Equatable {
    public let kept: [String]
    public let rejected: [String]

    public init(kept: [String], rejected: [String]) {
        self.kept = kept
        self.rejected = rejected
    }

    public static let none = CleanedKeys(kept: [], rejected: [])
}
