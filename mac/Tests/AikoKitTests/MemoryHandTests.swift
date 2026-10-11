import Foundation
import Testing

@testable import AikoKit

/// What the person does to the memory in the settings window, and what the window shows of it.
/// Nothing here may happen behind the person's back: a full category refuses instead of dropping
/// an old fact, and every refusal has a name the window can put into words.
struct MemoryHandTests {
    static let today = DateOnly(2026, 10, 11)
    static let lastMonth = DateOnly(2026, 9, 11)

    static func fact(
        _ number: Int, _ category: MemoryCategory, _ text: String = "", _ keys: String...,
        meaning: String = "", pinned: Bool = false
    ) -> MemoryFact {
        MemoryFact(
            MemoryFact.idFor(number), category, text.isEmpty ? "fact \(number)" : text,
            meaning: meaning, pinned: pinned, keys: keys, seen: lastMonth, created: lastMonth)
    }

    static func full(_ category: MemoryCategory) -> MemoryFile {
        let facts = (1...MemoryRules.quota(category)).map { fact($0, category) }

        return MemoryFile(facts.count + 1, facts, [])
    }

    // ---- a new fact ----

    @Test
    func aFactAddedByHandIsOneLineHasNoKeysAndIsSeenToday() {
        let memory = MemoryFile(19, [Self.fact(4, .you)], ["{}"])

        let edit = MemoryHand.add(memory, .work, "  Сначала \n короткий ответ ", " Потом\tподробности ", Self.today)

        let fact = edit.memory.facts[1]
        #expect(edit.outcome == .done)
        #expect(edit.changed)
        #expect(fact.id == "F19")
        #expect(fact.text == "Сначала короткий ответ")
        #expect(fact.meaning == "Потом подробности")
        #expect(fact.keys.isEmpty)
        #expect(!fact.pinned)
        #expect(fact.seen == Self.today)
        #expect(fact.created == Self.today)
        #expect(edit.memory.nextId == 20)
        #expect(edit.memory.setAside == ["{}"])
    }

    @Test(arguments: [nil, "", "  \n "] as [String?])
    func aFactWithoutTextIsNotAdded(text: String?) {
        let edit = MemoryHand.add(.empty, .you, text, "meaning", Self.today)

        #expect(edit.outcome == .empty)
        #expect(!edit.changed)
        #expect(edit.memory == .empty)
    }

    @Test
    func aFactOverTheLimitIsNotAddedAndOneAtTheLimitIs() {
        let text = String(repeating: "a", count: 150)
        let atTheLimit = MemoryHand.add(.empty, .you, text, String(repeating: "b", count: 50), Self.today)
        let over = MemoryHand.add(.empty, .you, text, String(repeating: "b", count: 51), Self.today)

        #expect(atTheLimit.outcome == .done)
        #expect(over.outcome == .tooLong)
        #expect(over.memory.facts.isEmpty)
    }

    @Test(arguments: [MemoryCategory.you, .work, .projects, .interests])
    func aFullCategoryRefusesAndDropsNothing(category: MemoryCategory) {
        let memory = Self.full(category)

        let edit = MemoryHand.add(memory, category, "One more", "", Self.today)

        #expect(edit.outcome == .full)
        #expect(edit.memory == memory)
    }

    @Test
    func aFullCategoryDoesNotCloseTheOthers() {
        let edit = MemoryHand.add(Self.full(.you), .work, "One more", "", Self.today)

        #expect(edit.outcome == .done)
    }

    // ---- an edit ----

    @Test
    func anEditChangesTheWordsAndKeepsTheRest() {
        let memory = MemoryFile(3, [Self.fact(1, .projects, "Makes Aiko", "Aiko", meaning: "Own product")], [])

        let edit = MemoryHand.edit(memory, "F1", "Makes Aiko, a tray app", "Own product")

        let fact = edit.memory.facts[0]
        #expect(edit.outcome == .done)
        #expect(fact.text == "Makes Aiko, a tray app")
        #expect(fact.keys == ["Aiko"])
        #expect(fact.seen == Self.lastMonth)
        #expect(fact.created == Self.lastMonth)
    }

    @Test
    func clearingTheMeaningFieldClearsTheMeaning() {
        let memory = MemoryFile(2, [Self.fact(1, .you, "Learns Swift", meaning: "Explain through C#")], [])

        let edit = MemoryHand.edit(memory, "F1", "Learns Swift", "  ")

        #expect(edit.memory.facts[0].meaning == "")
    }

    @Test
    func anEditThatChangesNothingDoesNotWrite() {
        let memory = MemoryFile(2, [Self.fact(1, .you, "Learns Swift")], [])

        let edit = MemoryHand.edit(memory, "F1", " Learns  Swift ", "")

        #expect(edit.outcome == .nothingToDo)
        #expect(!edit.changed)
    }

    @Test
    func anEditCountsTheKeysOfTheFactAgainstTheLimit() {
        let memory = MemoryFile(2, [Self.fact(1, .projects, "Makes Aiko", "Code Crawler")], [])

        let fits = MemoryHand.edit(memory, "F1", String(repeating: "a", count: MemoryRules.maxLength - 12), "")
        let over = MemoryHand.edit(memory, "F1", String(repeating: "a", count: MemoryRules.maxLength - 11), "")

        #expect(fits.outcome == .done)
        #expect(over.outcome == .tooLong)
    }

    @Test
    func anEditCanShortenAFactThatWasTooLong() {
        let memory = MemoryFile(2, [Self.fact(1, .work, String(repeating: "a", count: 312))], [])

        let edit = MemoryHand.edit(memory, "F1", "Short now", "")

        #expect(edit.outcome == .done)
        #expect(!MemoryPage.from(edit.memory).sections[1].facts[0].tooLong)
    }

    @Test
    func anEditToNoTextIsRefused() {
        let memory = MemoryFile(2, [Self.fact(1, .you)], [])

        #expect(MemoryHand.edit(memory, "F1", " ", "meaning").outcome == .empty)
    }

    // ---- pin, remove, erase ----

    @Test
    func aPinnedFactLosesItsKeysAndDoesNotGetThemBack() {
        let memory = MemoryFile(2, [Self.fact(1, .projects, "Makes Aiko", "Aiko")], [])

        let pinned = MemoryHand.pin(memory, "F1", true)
        let unpinned = MemoryHand.pin(pinned.memory, "F1", false)

        #expect(pinned.memory.facts[0].pinned)
        #expect(pinned.memory.facts[0].keys.isEmpty)
        #expect(!unpinned.memory.facts[0].pinned)
        #expect(unpinned.memory.facts[0].keys.isEmpty)
    }

    @Test
    func pinningWhatIsPinnedDoesNotWrite() {
        let memory = MemoryFile(2, [Self.fact(1, .you, pinned: true)], [])

        #expect(MemoryHand.pin(memory, "F1", true).outcome == .nothingToDo)
    }

    @Test
    func removingAFactKeepsTheOthersAndTheNumbers() {
        let memory = MemoryFile(3, [Self.fact(1, .you), Self.fact(2, .work)], [])

        let edit = MemoryHand.remove(memory, "F1")

        #expect(edit.memory.facts.map(\.id) == ["F2"])
        #expect(edit.memory.firstFreeNumber == 3)
    }

    @Test
    func aFactThatIsGoneIsNamedAsGoneByEveryAction() {
        let memory = MemoryFile(2, [Self.fact(1, .you)], [])

        #expect(MemoryHand.edit(memory, "F9", "text", "").outcome == .noFact)
        #expect(MemoryHand.pin(memory, "F9", true).outcome == .noFact)
        #expect(MemoryHand.remove(memory, "F9").outcome == .noFact)
    }

    @Test
    func erasingTakesTheFactsAndTheRecordsKeptAsideButNotTheNumbers() {
        let memory = MemoryFile(8, [Self.fact(1, .you), Self.fact(7, .work)], ["{}"])

        let edit = MemoryHand.eraseAll(memory)
        let next = MemoryHand.add(edit.memory, .you, "A new start", "", Self.today)

        #expect(edit.outcome == .done)
        #expect(edit.memory.facts.isEmpty)
        #expect(edit.memory.setAside.isEmpty)
        #expect(next.memory.facts[0].id == "F8")
    }

    @Test
    func erasingAnEmptyMemoryDoesNotWrite() {
        #expect(MemoryHand.eraseAll(.empty).outcome == .nothingToDo)
    }

    // ---- what the window shows ----

    @Test
    func thePageHasTheFourCategoriesInOrderWithCountsAndQuotas() {
        let memory = MemoryFile(4, [Self.fact(1, .work), Self.fact(2, .you), Self.fact(3, .work)], ["{}", "1"])

        let page = MemoryPage.from(memory)

        #expect(page.sections.map(\.category) == [.you, .work, .projects, .interests])
        #expect(page.sections.map(\.count) == [1, 2, 0, 0])
        #expect(page.sections.map(\.quota) == [8, 12, 12, 10])
        #expect(page.count == 3)
        #expect(page.capacity == 42)
        #expect(page.setAside == 2)
        #expect(!page.empty)
    }

    @Test
    func anEmptyMemoryIsEmptyAndOneWithOnlyRecordsAsideIsNot() {
        #expect(MemoryPage.from(.empty).empty)
        #expect(!MemoryPage.from(MemoryFile(1, [], ["{}"])).empty)
    }

    @Test
    func aFactOverTheLimitIsMarkedAndCounted() {
        let memory = MemoryFile(
            3,
            [
                Self.fact(1, .work, String(repeating: "a", count: 200)),
                Self.fact(2, .work, String(repeating: "a", count: 312)),
            ],
            [])

        let page = MemoryPage.from(memory)

        #expect(!page.sections[1].facts[0].tooLong)
        #expect(page.sections[1].facts[1].tooLong)
        #expect(page.sections[1].facts[1].length == 312)
        #expect(page.tooLong == 1)
    }

    @Test
    func aCategoryAtItsQuotaIsFull() {
        let page = MemoryPage.from(Self.full(.you))

        #expect(page.sections[0].full)
        #expect(!page.sections[1].full)
    }

    @Test
    func theCountUnderTheFieldIsTheCountTheSaveWillCheck() {
        let fact = Self.fact(1, .projects, "Makes Aiko", "Aiko", "C#")

        #expect(MemoryPage.lengthOf(nil, nil) == 0)
        #expect(MemoryPage.lengthOf("  one \n two ", " ab ") == 9)
        #expect(MemoryPage.lengthOf("one two", "ab", fact) == 9 + 6)
        #expect(MemoryPage.lengthOf(fact.text, fact.meaning, fact) == MemoryRules.lengthOf(fact))
    }
}
