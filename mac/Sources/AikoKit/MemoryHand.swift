import Foundation

/// How a change the person made in the settings window ended.
public enum MemoryHandOutcome: Sendable, Equatable {
    case done

    /// The fact already says this; the file is not written.
    case nothingToDo

    /// A fact needs text.
    case empty

    /// The fact with its meaning and keys is over the limit.
    case tooLong

    /// The category has no free place, and nothing is dropped to make one.
    case full

    /// The fact is gone, for example the model forgot it while the window was open.
    case noFact
}

/// The memory after a change by hand. `changed` says whether to write the file.
public struct MemoryHandEdit: Sendable, Equatable {
    public let memory: MemoryFile
    public let outcome: MemoryHandOutcome

    public init(memory: MemoryFile, outcome: MemoryHandOutcome) {
        self.memory = memory
        self.outcome = outcome
    }

    public var changed: Bool { outcome == .done }
}

/// What the person does to the memory in the settings window: add, edit, pin, remove, erase.
///
/// These are not the tools of the model. A tool call answers the model in words and may drop the
/// oldest fact to make room. A person sees the list, so nothing is dropped behind their back: a
/// full category refuses, and the window says so. The outcome is a value, and the words for it
/// are the window's, in the person's language.
public enum MemoryHand {
    /// A fact typed by hand has no keys: the person did not name any, and guessing them here
    /// would refresh the fact on words they never chose. It may be in any language.
    public static func add(
        _ memory: MemoryFile, _ category: MemoryCategory, _ text: String?, _ meaning: String?, _ today: DateOnly
    ) -> MemoryHandEdit {
        let fact = MemoryFact(
            MemoryFact.idFor(memory.firstFreeNumber), category, MemoryText.oneLine(text),
            meaning: MemoryText.oneLine(meaning),
            seen: today,
            created: today)

        if let problem = problem(fact) {
            return MemoryHandEdit(memory: memory, outcome: problem)
        }

        return memory.inCategory(category).count >= MemoryRules.quota(category)
            ? MemoryHandEdit(memory: memory, outcome: .full)
            : done(MemoryFile(MemoryFile.numberAfter(fact.number), memory.facts + [fact], memory.setAside))
    }

    /// Unlike the tool, an empty meaning here means "no meaning": the person cleared the field.
    /// The day the fact was last seen does not move: editing a fact is not living by it.
    public static func edit(_ memory: MemoryFile, _ id: String?, _ text: String?, _ meaning: String?) -> MemoryHandEdit {
        guard let existing = memory.find(id) else {
            return MemoryHandEdit(memory: memory, outcome: .noFact)
        }

        var edited = existing
        edited.text = MemoryText.oneLine(text)
        edited.meaning = MemoryText.oneLine(meaning)
        if let problem = problem(edited) {
            return MemoryHandEdit(memory: memory, outcome: problem)
        }

        return edited == existing ? MemoryHandEdit(memory: memory, outcome: .nothingToDo) : done(replace(memory, edited))
    }

    /// A pinned fact never ages, so it needs no keys and loses the ones it had, as with the tool.
    /// Taking the pin off does not bring them back.
    public static func pin(_ memory: MemoryFile, _ id: String?, _ pinned: Bool) -> MemoryHandEdit {
        guard let existing = memory.find(id) else {
            return MemoryHandEdit(memory: memory, outcome: .noFact)
        }

        if existing.pinned == pinned {
            return MemoryHandEdit(memory: memory, outcome: .nothingToDo)
        }

        var changed = existing
        changed.pinned = pinned
        changed.keys = MemoryRules.takesKeys(existing.category, pinned: pinned) ? existing.keys : []
        return done(replace(memory, changed))
    }

    public static func remove(_ memory: MemoryFile, _ id: String?) -> MemoryHandEdit {
        guard let existing = memory.find(id) else {
            return MemoryHandEdit(memory: memory, outcome: .noFact)
        }

        return done(MemoryFile(memory.nextId, memory.facts.filter { $0.id != existing.id }, memory.setAside))
    }

    /// Every fact and every record kept aside. The numbers go on from where they were: a session
    /// that is open may still hold the old ids, and a new fact must not answer to one of them.
    public static func eraseAll(_ memory: MemoryFile) -> MemoryHandEdit {
        memory.facts.isEmpty && memory.setAside.isEmpty
            ? MemoryHandEdit(memory: memory, outcome: .nothingToDo)
            : done(MemoryFile(memory.firstFreeNumber, [], []))
    }

    private static func problem(_ fact: MemoryFact) -> MemoryHandOutcome? {
        if fact.text.isEmpty {
            return .empty
        }

        return MemoryRules.fits(MemoryRules.lengthOf(fact)) ? nil : .tooLong
    }

    private static func replace(_ memory: MemoryFile, _ fact: MemoryFact) -> MemoryFile {
        MemoryFile(memory.nextId, memory.facts.map { $0.id == fact.id ? fact : $0 }, memory.setAside)
    }

    private static func done(_ memory: MemoryFile) -> MemoryHandEdit {
        MemoryHandEdit(memory: memory, outcome: .done)
    }
}
