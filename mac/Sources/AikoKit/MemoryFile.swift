import Foundation

/// The memory as it lies in memory.json: the facts, the number of the next one, and the entries
/// Aiko could not use.
///
/// The file is the person's own data and they may edit it by hand. So an entry that cannot be
/// used is not dropped: its JSON is kept in `setAside` as it was written and goes back into the
/// file on the next save, where the person can still find and repair it.
public struct MemoryFile: Sendable, Equatable {
    /// See AppSettings.currentSchema for why this is written from the start.
    public static let currentSchema = 1

    public static let dateFormat = "yyyy-MM-dd"

    public var nextId: Int
    public var facts: [MemoryFact]
    public var setAside: [String]

    public init(_ nextId: Int, _ facts: [MemoryFact], _ setAside: [String]) {
        self.nextId = nextId
        self.facts = facts
        self.setAside = setAside
    }

    public static let empty = MemoryFile(1, [], [])

    /// The number the next new fact gets. An id is never used twice, even when the file says a
    /// smaller number than the facts in it: the model holds ids from earlier in the session.
    public var firstFreeNumber: Int {
        max(max(nextId, 1), MemoryFile.numberAfter(facts.map(\.number).max() ?? 0))
    }

    /// One more, counted in 32 bits as on Windows. After F2147483647 the count wraps around to a
    /// negative number there, and the next id is then taken from `nextId` or from 1. Counting
    /// further here would give an id that neither core reads back.
    static func numberAfter(_ number: Int) -> Int {
        Int(Int32(truncatingIfNeeded: number) &+ 1)
    }

    /// The model types ids by hand, so f12 finds F12.
    public func find(_ id: String?) -> MemoryFact? {
        guard let id else {
            return nil
        }

        let trimmed = MemoryText.trim(id)
        return facts.first { MemoryText.sameIgnoringAsciiCase($0.id, trimmed) }
    }

    /// `In` on Windows; `in` is a keyword here.
    public func inCategory(_ category: MemoryCategory) -> [MemoryFact] {
        facts.filter { $0.category == category }
    }

    /// No file yet is an empty memory. A file that is not the memory at all is an empty memory
    /// too, but it is marked: the next save would put this empty memory over text somebody may
    /// want back, so the caller keeps that file aside first (the same idea as JsonText).
    /// A file from a newer Aiko counts as unreadable for the same reason.
    public static func fromJson(_ json: String?) -> MemoryRead {
        guard let json, !MemoryText.isBlank(json) else {
            return MemoryRead(memory: empty, unreadable: false)
        }

        guard let root = JsonNode.parse(json), let memory = read(root, json) else {
            return MemoryRead(memory: empty, unreadable: true)
        }

        return MemoryRead(memory: memory, unreadable: false)
    }

    /// Written by hand and not through JsonNode: the entries set aside have to go back as they
    /// were written, and a JsonNode would write them its own way.
    ///
    /// The lines end with "\n". On Windows they end with "\r\n"; each core reads both.
    public func toJson() -> String {
        var lines = ["{", "  \"schema\": \(MemoryFile.currentSchema),", "  \"nextId\": \(firstFreeNumber),"]
        lines += MemoryFile.list("facts", facts.map(MemoryFile.writtenFact), between: ",\n", comma: true)
        lines += MemoryFile.list("setAside", setAside, between: ",", comma: false)
        lines.append("}")
        return lines.joined(separator: "\n") + "\n"
    }

    private static func read(_ root: JsonNode, _ json: String) -> MemoryFile? {
        guard let file = root.objectValue, !isFromNewerAiko(file) else {
            return nil
        }

        let written = JsonRawText.members(json) ?? []
        var facts: [MemoryFact] = []
        var setAside: [String] = []

        if !absent(file, "facts") {
            guard let entries = file["facts"]?.arrayValue,
                  let texts = itemsOf("facts", written), texts.count == entries.count else {
                return nil
            }

            for (entry, text) in zip(entries, texts) {
                if let fact = factIn(entry), !facts.contains(where: { $0.id == fact.id }) {
                    facts.append(fact)
                } else {
                    setAside.append(text)
                }
            }
        }

        if file["setAside"]?.arrayValue != nil {
            setAside += itemsOf("setAside", written) ?? []
        }

        let memory = MemoryFile(int32(file["nextId"]) ?? 1, facts, setAside)
        return MemoryFile(memory.firstFreeNumber, facts, setAside)
    }

    /// The items of one list of the file, each as it was written. The last member of that name
    /// counts, which is the one JsonNode reads too.
    private static func itemsOf(_ name: String, _ written: [(key: String, value: String)]) -> [String]? {
        written.last { $0.key == name }.flatMap { JsonRawText.items($0.value) }
    }

    private static func isFromNewerAiko(_ file: JsonObject) -> Bool {
        guard let schema = file["schema"], case .number = schema else {
            return false
        }

        guard let version = int32(schema) else {
            return true
        }

        return version > currentSchema
    }

    /// Nil when the entry cannot be used. A field that is missing has a default where one makes
    /// sense; a field of the wrong type never does, because guessing would change what was meant.
    private static func factIn(_ node: JsonNode) -> MemoryFact? {
        guard let entry = node.objectValue,
              let id = entry["id"]?.stringValue,
              MemoryFact.numberOf(id) != nil,
              let category = MemoryRules.categoryOf(entry["category"]?.stringValue),
              let text = entry["text"]?.stringValue,
              !MemoryText.isBlank(text),
              let seen = date(entry["seen"]) else {
            return nil
        }

        let meaning = absent(entry, "meaning") ? "" : entry["meaning"]?.stringValue
        let pinned = absent(entry, "pinned") ? false : entry["pinned"]?.boolValue
        let keys = absent(entry, "keys") ? [] : texts(entry["keys"])
        let created = absent(entry, "created") ? seen : date(entry["created"])
        guard let meaning, let pinned, let keys, let created else {
            return nil
        }

        return MemoryFact(
            id, category, text,
            meaning: meaning,
            pinned: pinned,
            keys: Array(keys.prefix(MemoryRules.maxKeys)),
            seen: seen,
            created: created)
    }

    private static func absent(_ entry: JsonObject, _ name: String) -> Bool {
        guard let value = entry[name] else {
            return true
        }

        return value == .null
    }

    /// A whole number that fits 32 bits, as TryGetInt32 reads it.
    private static func int32(_ node: JsonNode?) -> Int? {
        guard let value = node?.int64Value, let number = Int32(exactly: value) else {
            return nil
        }

        return Int(number)
    }

    private static func texts(_ node: JsonNode?) -> [String]? {
        guard let items = node?.arrayValue else {
            return nil
        }

        let strings = items.compactMap(\.stringValue)
        return strings.count == items.count ? strings : nil
    }

    private static func date(_ node: JsonNode?) -> DateOnly? {
        node?.stringValue.flatMap(day)
    }

    /// A day written as 2026-10-09 and nothing else: four digits, two and two, and a day the
    /// calendar has.
    static func day(_ text: String) -> DateOnly? {
        let units = Array(text.utf16)
        guard units.count == 10, units[4] == 0x2D, units[7] == 0x2D else {
            return nil
        }

        func number(_ range: Range<Int>) -> Int? {
            var value = 0
            for unit in units[range] {
                guard unit >= 0x30, unit <= 0x39 else { return nil }
                value = value * 10 + Int(unit - 0x30)
            }
            return value
        }

        guard let year = number(0..<4), let month = number(5..<7), let day = number(8..<10),
              year >= 1, month >= 1, month <= 12, day >= 1, day <= daysIn(month, year) else {
            return nil
        }

        return DateOnly(year, month, day)
    }

    static func written(_ day: DateOnly) -> String {
        String(format: "%04d-%02d-%02d", day.year, day.month, day.day)
    }

    private static func daysIn(_ month: Int, _ year: Int) -> Int {
        switch month {
        case 2: return (year % 4 == 0 && year % 100 != 0) || year % 400 == 0 ? 29 : 28
        case 4, 6, 9, 11: return 30
        default: return 31
        }
    }

    /// One list of the file as its lines. An empty list stays on the line of its name.
    ///
    /// The facts start on a line of their own. The entries set aside follow the bracket with no
    /// space and no line break between them, because that is what the JSON writer of .NET does
    /// with text it is given as it is, and the two cores write the same file.
    private static func list(_ name: String, _ items: [String], between: String, comma: Bool) -> [String] {
        let end = comma ? "," : ""
        guard !items.isEmpty else {
            return ["  \"\(name)\": []\(end)"]
        }

        let open = "  \"\(name)\": ["
        let body = items.joined(separator: between)
        return (between.hasSuffix("\n") ? [open, body] : [open + body]) + ["  ]\(end)"]
    }

    /// One fact, indented as an item of the facts list. A string of JSON has no line break in it,
    /// so every line of the fact can be moved right as a whole.
    private static func writtenFact(_ fact: MemoryFact) -> String {
        let object = JsonObject([
            ("id", .string(fact.id)),
            ("category", .string(MemoryRules.nameOf(fact.category))),
            ("text", .string(fact.text)),
            ("meaning", .string(fact.meaning)),
            ("pinned", .bool(fact.pinned)),
            ("keys", .array(fact.keys.map(JsonNode.string))),
            ("seen", .string(written(fact.seen))),
            ("created", .string(written(fact.created))),
        ])

        // The relaxed escaping keeps Cyrillic, quotes and + as they are. The file is read by a
        // person in a text editor and never put into a web page.
        return JsonNode.object(object).toJsonString(indented: true, escaping: .relaxed)
            .split(separator: "\n", omittingEmptySubsequences: false)
            .map { "    " + $0 }
            .joined(separator: "\n")
    }
}

/// What reading memory.json gave. `unreadable` means the text was there and was not a memory file.
public struct MemoryRead: Sendable, Equatable {
    public let memory: MemoryFile
    public let unreadable: Bool

    public init(memory: MemoryFile, unreadable: Bool) {
        self.memory = memory
        self.unreadable = unreadable
    }
}
