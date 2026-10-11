import AikoKit
import AppKit
import Foundation

/// What the memory block is showing right now, and every click on it. The twin of the fields and
/// the "changes" part of MemoryBlock.cs.
///
/// What a click does to the memory is decided in AikoKit.MemoryHand and written by MemoryStore.
/// This class only remembers which rows are open and which form is on screen.
@MainActor
final class MemoryBlockModel: ObservableObject {
    @Published private(set) var memory = MemoryStore.load()
    @Published private(set) var open: Set<MemoryCategory> = []

    /// The fact shown as two fields instead of two lines.
    @Published private(set) var editing: String?

    /// The category of the fact being typed in, or nil when the form is closed.
    @Published private(set) var adding: MemoryCategory?

    @Published private(set) var erasing = false

    /// What went wrong with the last click, in the person's words. Shown once, above the buttons.
    @Published private(set) var problem: String?

    init() {
        tryState()
    }

    var page: MemoryPage { MemoryPage.from(memory) }

    /// A door for snapshots: AIKO_TRY_MEMORY=open, edit, add or erase draws the block in that
    /// state, so every state of the mockup can be looked at without clicking.
    private func tryState() {
        guard let state = ProcessInfo.processInfo.environment["AIKO_TRY_MEMORY"], !state.isEmpty else {
            return
        }

        open = Set(MemoryRules.categories)
        editing = state == "edit" ? memory.inCategory(.work).first?.id : nil
        adding = state == "add" ? .work : nil
        erasing = state == "erase"
        if state == "add" || state == "erase" {
            open = []
        }
    }

    /// The model may have saved a fact since the page was built.
    func reload() {
        memory = MemoryStore.load()
    }

    // ---- what the block shows, with nothing written ----

    func toggle(_ category: MemoryCategory) {
        show {
            if open.remove(category) == nil {
                open.insert(category)
            }
        }
    }

    /// The form opens on "How to work with me": that is what people most often tell by hand.
    func startAdding() {
        let withRoom = page.sections.filter { !$0.full }.map(\.category)
        show { adding = withRoom.contains(.work) ? .work : (withRoom.first ?? .work) }
    }

    func pick(_ category: MemoryCategory) {
        show { adding = category }
    }

    func stopAdding() {
        show { adding = nil }
    }

    func startEditing(_ fact: MemoryFact) {
        show { editing = fact.id }
    }

    func stopEditing() {
        show { editing = nil }
    }

    func askToErase() {
        show { erasing = true }
    }

    func keepMemory() {
        show { erasing = false }
    }

    private func show(_ change: () -> Void) {
        problem = nil
        change()
    }

    // ---- changes of the memory itself ----

    func add(_ text: String, _ meaning: String) {
        guard let category = adding else { return }

        apply({ MemoryHand.add($0, category, text, meaning, Self.today()) }, then: {
            adding = nil
            open.insert(category)
        })
    }

    func edit(_ fact: MemoryFact, _ text: String, _ meaning: String) {
        apply({ MemoryHand.edit($0, fact.id, text, meaning) })
    }

    func pin(_ fact: MemoryFact) {
        apply({ MemoryHand.pin($0, fact.id, !fact.pinned) })
    }

    func remove(_ fact: MemoryFact) {
        apply({ MemoryHand.remove($0, fact.id) })
    }

    func eraseAll() {
        apply(MemoryHand.eraseAll, then: {
            erasing = false
            open = []
        })
    }

    /// The file is read again under the lock, so a fact the model saved a moment ago is not lost,
    /// and then the block is drawn from what was written.
    private func apply(_ change: (MemoryFile) -> MemoryHandEdit, then: () -> Void = {}) {
        guard let edit = MemoryStore.change(change) else {
            problem = Strings.memoryWriteFailed
            return
        }

        problem = edit.outcome == .full ? Strings.memoryFull : nil
        memory = edit.memory

        switch edit.outcome {
        case .done, .nothingToDo, .noFact:
            editing = nil
            then()
        case .empty, .tooLong, .full:
            break
        }
    }

    /// A memory that was never written gets an empty file first, so there is something to open.
    func openFile() {
        guard MemoryStore.ensureFile() else {
            show { problem = Strings.memoryWriteFailed }
            return
        }

        if !NSWorkspace.shared.open(URL(fileURLWithPath: MemoryStore.path)) {
            Log.write("memory: the file could not be opened")
        }
    }

    /// The day on the clock of this Mac, as the bridge writes it.
    private static func today() -> DateOnly {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = .current
        let day = calendar.dateComponents([.year, .month, .day], from: Date())
        return DateOnly(day.year ?? 1, day.month ?? 1, day.day ?? 1)
    }
}
