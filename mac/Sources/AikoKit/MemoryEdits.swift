import Foundation

/// What the model asks for in one call of the remember tool. With an id it changes that fact,
/// without one it adds a new fact.
public struct MemoryRequest: Sendable, Equatable {
    public var id: String?
    public var category: String?
    public var text: String?
    public var meaning: String?
    public var pin: Bool?
    public var keys: [String]?

    public init(
        id: String? = nil,
        category: String? = nil,
        text: String? = nil,
        meaning: String? = nil,
        pin: Bool? = nil,
        keys: [String]? = nil
    ) {
        self.id = id
        self.category = category
        self.text = text
        self.meaning = meaning
        self.pin = pin
        self.keys = keys
    }
}

/// The memory after a change and the answer the model reads. `changed` is false when the memory is
/// the one that came in, so the caller does not write the file for nothing.
public struct MemoryEdit: Sendable, Equatable {
    public let memory: MemoryFile
    public let reply: String
    public let changed: Bool

    public init(memory: MemoryFile, reply: String, changed: Bool) {
        self.memory = memory
        self.reply = reply
        self.changed = changed
    }
}

/// The memory after a message of the person, and the ids of the facts it refreshed.
public struct MemoryTouch: Sendable, Equatable {
    public let memory: MemoryFile
    public let touched: [String]

    public init(memory: MemoryFile, touched: [String]) {
        self.memory = memory
        self.touched = touched
    }
}

/// What the two memory tools and a message of the person do to the memory.
///
/// Nothing here reads a file or a clock: the memory and the day come in, the new memory and the
/// reply go out. The reply is written for the model, in English, and always says what happened
/// to the memory, because the model cannot see the file.
public enum MemoryEdits {
    public static func remember(_ memory: MemoryFile, _ request: MemoryRequest, _ today: DateOnly) -> MemoryEdit {
        MemoryText.isBlank(request.id) ? add(memory, request, today) : update(memory, request, today)
    }

    public static func forget(_ memory: MemoryFile, _ id: String?) -> MemoryEdit {
        guard let fact = memory.find(id) else {
            return refused(memory, noFact(id))
        }

        return MemoryEdit(memory: without(memory, fact), reply: "Forgot \(fact.id).", changed: true)
    }

    /// A key in the message means the fact came up again, so it is seen today. Facts already seen
    /// today are left alone and not named: the caller saves only when something moved.
    public static func touchByPrompt(_ memory: MemoryFile, _ prompt: String, _ today: DateOnly) -> MemoryTouch {
        let message = SearchedText(prompt)
        let touched = memory.facts
            .filter { fact in
                fact.seen != today && fact.keys.contains { MemoryRules.keyMatches($0, message) }
            }
            .map(\.id)
        guard !touched.isEmpty else {
            return MemoryTouch(memory: memory, touched: [])
        }

        var after = memory
        after.facts = memory.facts.map { fact in
            var fact = fact
            if touched.contains(fact.id) {
                fact.seen = today
            }
            return fact
        }
        return MemoryTouch(memory: after, touched: touched)
    }

    private static func add(_ memory: MemoryFile, _ request: MemoryRequest, _ today: DateOnly) -> MemoryEdit {
        guard let category = MemoryRules.categoryOf(request.category) else {
            let names = MemoryRules.categories.map(MemoryRules.nameOf).joined(separator: ", ")
            return refused(
                memory, "Unknown category \(request.category ?? ""). Use one of: \(names). Nothing was saved.")
        }

        let text = MemoryText.oneLine(request.text)
        if text.isEmpty {
            return refused(memory, "A new fact needs text. Nothing was saved.")
        }

        let pinned = request.pin ?? false
        let keys = MemoryRules.takesKeys(category, pinned: pinned) ? MemoryRules.cleanKeys(request.keys) : CleanedKeys.none
        let fact = MemoryFact(
            MemoryFact.idFor(memory.firstFreeNumber), category, text,
            meaning: MemoryText.oneLine(request.meaning),
            pinned: pinned,
            keys: keys.kept,
            seen: today,
            created: today)

        if !MemoryRules.fits(MemoryRules.lengthOf(fact)) {
            return refused(memory, tooLong(fact, "Nothing was saved."))
        }

        guard let room = makeRoom(memory, category) else {
            return refused(memory, category == .work ? workIsFull(memory) : allPinned(category))
        }

        return saved(room.memory, fact, keyNote(keys) + room.note)
    }

    private static func update(_ memory: MemoryFile, _ request: MemoryRequest, _ today: DateOnly) -> MemoryEdit {
        guard let existing = memory.find(request.id) else {
            return refused(memory, noFact(request.id))
        }

        let pinned = request.pin ?? existing.pinned
        let takesKeys = MemoryRules.takesKeys(existing.category, pinned: pinned)
        let asked = request.keys ?? []
        let given = takesKeys && !asked.isEmpty ? MemoryRules.cleanKeys(asked) : nil

        let text = MemoryText.oneLine(request.text)
        let meaning = MemoryText.oneLine(request.meaning)
        var updated = existing
        updated.text = text.isEmpty ? existing.text : text
        updated.meaning = meaning.isEmpty ? existing.meaning : meaning
        updated.pinned = pinned
        updated.keys = !takesKeys ? [] : given?.kept ?? existing.keys
        updated.seen = today

        if !MemoryRules.fits(MemoryRules.lengthOf(updated)) {
            return refused(memory, tooLong(updated, "Nothing changed."))
        }

        var after = memory
        after.facts = memory.facts.map { $0.id == existing.id ? updated : $0 }
        let note = given.map(keyNote) ?? ""
        return MemoryEdit(memory: after, reply: "Refreshed \(existing.id).\(note)", changed: true)
    }

    /// The memory with a free place in the category, or nil when there is none to make. Work
    /// habits are never dropped by age: an old habit is as true as a new one, so the model picks.
    private static func makeRoom(_ memory: MemoryFile, _ category: MemoryCategory) -> (memory: MemoryFile, note: String)? {
        let inCategory = memory.inCategory(category)
        if inCategory.count < MemoryRules.quota(category) {
            return (memory, "")
        }

        guard category != .work, let oldest = MemoryRules.oldestUnpinned(inCategory) else {
            return nil
        }

        return (without(memory, oldest), " Memory was full, forgot \(oldest.id): \(oldest.text)")
    }

    private static func saved(_ memory: MemoryFile, _ fact: MemoryFact, _ notes: String) -> MemoryEdit {
        MemoryEdit(
            memory: MemoryFile(MemoryFile.numberAfter(fact.number), memory.facts + [fact], memory.setAside),
            reply: "Saved \(fact.id).\(notes)",
            changed: true)
    }

    private static func refused(_ memory: MemoryFile, _ reply: String) -> MemoryEdit {
        MemoryEdit(memory: memory, reply: reply, changed: false)
    }

    private static func without(_ memory: MemoryFile, _ fact: MemoryFact) -> MemoryFile {
        MemoryFile(memory.nextId, memory.facts.filter { $0.id != fact.id }, memory.setAside)
    }

    private static func noFact(_ id: String?) -> String {
        "No fact \(id.map(MemoryText.trim) ?? ""). Nothing changed."
    }

    private static func tooLong(_ fact: MemoryFact, _ outcome: String) -> String {
        "Too long: the fact, its meaning and keys are \(MemoryRules.lengthOf(fact)) characters together, "
            + "and the limit is \(MemoryRules.maxLength). \(outcome) Shorten it to one thought and call again."
    }

    private static func keyNote(_ keys: CleanedKeys) -> String {
        keys.rejected.isEmpty
            ? ""
            : " Keys not kept, they are common words: \(keys.rejected.joined(separator: ", "))."
                + " Use names or terms, or no keys."
    }

    private static func workIsFull(_ memory: MemoryFile) -> String {
        let title = MemoryRules.title(.work)
        let lines = memory.inCategory(.work).map { "[\($0.id)] \($0.text)" }

        return "\"\(title)\" is full (\(MemoryRules.quota(.work)) facts). Nothing was saved.\n"
            + lines.joined(separator: "\n")
            + "\nMerge two close facts into one (remember with an id, then forget the other),"
            + " or forget the one that matters least. Then call again."
    }

    private static func allPinned(_ category: MemoryCategory) -> String {
        "\"\(MemoryRules.title(category))\" is full and every fact in it is pinned, so none can be forgotten"
            + " to make room. Nothing was saved. Merge two close facts into one (remember with an id, then"
            + " forget the other), then call again."
    }
}
