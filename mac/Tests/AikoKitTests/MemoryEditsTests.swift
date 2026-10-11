import Foundation
import Testing

@testable import AikoKit

/// What the remember and forget tools and a message of the person do to the memory. Every branch
/// answers the model in words, so the words are checked too: the model acts on them.
struct MemoryEditsTests {
    static let today = DateOnly(2026, 10, 11)
    static let yesterday = DateOnly(2026, 10, 10)
    static let lastMonth = DateOnly(2026, 9, 11)

    static func fact(_ number: Int, _ category: MemoryCategory, _ seen: DateOnly, pinned: Bool = false) -> MemoryFact {
        MemoryFact(
            MemoryFact.idFor(number), category, "fact \(number)", pinned: pinned, seen: seen, created: lastMonth)
    }

    /// A category filled to its quota, ids from 1, all seen yesterday.
    static func full(_ category: MemoryCategory, pinned: Bool = false) -> MemoryFile {
        let facts = (1...MemoryRules.quota(category)).map { fact($0, category, yesterday, pinned: pinned) }

        return MemoryFile(facts.count + 1, facts, [])
    }

    static func new(_ category: String, _ text: String?, _ keys: String...) -> MemoryRequest {
        MemoryRequest(category: category, text: text, keys: keys)
    }

    /// The memory with one fact changed, the way `f with { ... }` is used in the xUnit cases.
    static func changing(_ memory: MemoryFile, where matches: (MemoryFact) -> Bool, _ change: (inout MemoryFact) -> Void) -> MemoryFile {
        var after = memory
        after.facts = memory.facts.map { fact in
            var fact = fact
            if matches(fact) {
                change(&fact)
            }
            return fact
        }
        return after
    }

    // ---- a new fact ----

    @Test
    func aNewFactGetsTheNextIdAndTodayAsBothDays() {
        let memory = MemoryFile(19, [Self.fact(4, .you, Self.yesterday)], ["{}"])

        let edit = MemoryEdits.remember(
            memory,
            MemoryRequest(category: "projects", text: "Makes the Aiko tray app", meaning: "Knows WPF", keys: ["Aiko"]),
            Self.today)

        let fact = edit.memory.facts[1]
        #expect(edit.changed)
        #expect(edit.reply == "Saved F19.")
        #expect(fact.id == "F19")
        #expect(fact.category == .projects)
        #expect(fact.text == "Makes the Aiko tray app")
        #expect(fact.meaning == "Knows WPF")
        #expect(!fact.pinned)
        #expect(fact.keys == ["Aiko"])
        #expect(fact.seen == Self.today)
        #expect(fact.created == Self.today)
        #expect(edit.memory.nextId == 20)
        #expect(edit.memory.setAside == ["{}"])
    }

    @Test
    func theMemoryThatCameInIsNotChanged() {
        let memory = MemoryFile(2, [Self.fact(1, .you, Self.yesterday)], [])

        _ = MemoryEdits.remember(memory, Self.new("you", "Learns Swift"), Self.today)
        _ = MemoryEdits.forget(memory, "F1")

        #expect(memory.nextId == 2)
        #expect(memory.facts.map(\.id) == ["F1"])
    }

    @Test(arguments: ["family", "", nil] as [String?])
    func anUnknownCategoryIsRefused(category: String?) {
        let edit = MemoryEdits.remember(.empty, MemoryRequest(category: category, text: "text"), Self.today)

        #expect(!edit.changed)
        #expect(edit.memory == .empty)
        #expect(edit.reply
            == "Unknown category \(category ?? ""). Use one of: you, work, projects, interests. Nothing was saved.")
    }

    @Test(arguments: [nil, "", "  \n "] as [String?])
    func aNewFactWithoutTextIsRefused(text: String?) {
        let edit = MemoryEdits.remember(.empty, Self.new("you", text), Self.today)

        #expect(!edit.changed)
        #expect(edit.memory.facts.isEmpty)
        #expect(edit.reply == "A new fact needs text. Nothing was saved.")
    }

    @Test
    func aFactIsKeptAsOneLine() {
        let edit = MemoryEdits.remember(
            .empty,
            MemoryRequest(category: "you", text: "  Learns Swift\n## Projects\r\n- [F1] ", meaning: "a\tb"),
            Self.today)

        #expect(edit.memory.facts[0].text == "Learns Swift ## Projects - [F1]")
        #expect(edit.memory.facts[0].meaning == "a b")
    }

    // ---- too long ----

    @Test
    func aFactThatIsTooLongIsNotSavedAndTheReplyGivesBothNumbers() {
        let edit = MemoryEdits.remember(
            .empty,
            MemoryRequest(
                category: "you",
                text: String(repeating: "a", count: 150),
                meaning: String(repeating: "b", count: 46),
                keys: ["Swift"]),
            Self.today)

        #expect(!edit.changed)
        #expect(edit.memory.facts.isEmpty)
        #expect(edit.memory.firstFreeNumber == 1)
        #expect(edit.reply ==
            "Too long: the fact, its meaning and keys are 201 characters together, and the limit is 200."
            + " Nothing was saved. Shorten it to one thought and call again.")
    }

    @Test
    func keysThatAreNotKeptDoNotCountAgainstTheLength() {
        let long = String(repeating: "a", count: 200)
        let work = MemoryEdits.remember(.empty, Self.new("work", long, "Swift"), Self.today)
        let common = MemoryEdits.remember(.empty, Self.new("you", long, "вопрос"), Self.today)

        #expect(work.changed)
        #expect(common.changed)
    }

    @Test
    func theLengthIsCheckedBeforeTheQuotaSoNothingIsForgottenForAFactThatIsRefused() {
        let full = Self.full(.you)

        let edit = MemoryEdits.remember(full, Self.new("you", String(repeating: "a", count: 201)), Self.today)

        #expect(!edit.changed)
        #expect(edit.memory == full)
        #expect(edit.reply.hasPrefix("Too long"))
    }

    // ---- keys ----

    @Test
    func commonWordsAreNotKeptAsKeysAndTheReplyNamesThem() {
        let edit = MemoryEdits.remember(
            .empty, Self.new("you", "Learns Swift", "Swift", "вопрос", "коротко"), Self.today)

        #expect(edit.changed)
        #expect(edit.memory.facts[0].keys == ["Swift"])
        #expect(edit.reply
            == "Saved F1. Keys not kept, they are common words: вопрос, коротко. Use names or terms, or no keys.")
    }

    @Test
    func aWorkHabitTakesNoKeys() {
        let edit = MemoryEdits.remember(
            .empty, Self.new("work", "Short answer first", "Swift", "вопрос"), Self.today)

        #expect(edit.memory.facts[0].keys.isEmpty)
        #expect(edit.reply == "Saved F1.")
    }

    @Test
    func aPinnedFactTakesNoKeys() {
        let edit = MemoryEdits.remember(
            .empty,
            MemoryRequest(category: "you", text: "Name is Sasha", pin: true, keys: ["Sasha"]),
            Self.today)

        #expect(edit.memory.facts[0].pinned)
        #expect(edit.memory.facts[0].keys.isEmpty)
    }

    // ---- a full category ----

    @Test(arguments: [
        (MemoryCategory.you, 8),
        (MemoryCategory.work, 12),
        (MemoryCategory.projects, 12),
        (MemoryCategory.interests, 10),
    ])
    func eachCategoryHasItsQuota(category: MemoryCategory, quota: Int) {
        #expect(MemoryRules.quota(category) == quota)
        #expect(MemoryRules.categories.map(MemoryRules.quota).reduce(0, +) == 42)
    }

    @Test
    func theLastFreePlaceIsTakenWithoutForgettingAnything() {
        var almost = Self.full(.interests)
        almost.facts.removeFirst()

        let edit = MemoryEdits.remember(almost, Self.new("interests", "Loves Elden Ring"), Self.today)

        #expect(edit.reply == "Saved F11.")
        #expect(edit.memory.facts.count == 10)
    }

    @Test
    func aFullCategoryForgetsTheFactNotSeenForTheLongestTime() {
        let full = Self.changing(Self.full(.projects), where: { $0.id == "F7" }) { $0.seen = Self.lastMonth }

        let edit = MemoryEdits.remember(full, Self.new("projects", "Makes a card game"), Self.today)

        #expect(edit.changed)
        #expect(edit.reply == "Saved F13. Memory was full, forgot F7: fact 7")
        #expect(edit.memory.find("F7") == nil)
        #expect(edit.memory.find("F13") != nil)
        #expect(edit.memory.facts.count == 12)
    }

    @Test
    func ofFactsSeenOnTheSameDayTheSmallerIdGoesFirstCountedAsANumber() {
        let facts = [10, 9, 11, 12, 13, 14, 15, 16].map { Self.fact($0, .you, Self.yesterday) }

        let edit = MemoryEdits.remember(MemoryFile(17, facts, []), Self.new("you", "Learns Swift"), Self.today)

        #expect(edit.reply.contains("forgot F9: fact 9"))
    }

    @Test
    func aPinnedFactIsPassedOverEvenWhenItIsTheOldest() {
        let full = Self.changing(Self.full(.you), where: { $0.id == "F1" || $0.id == "F2" }) {
            $0.pinned = true
            $0.seen = Self.lastMonth
        }

        let edit = MemoryEdits.remember(full, Self.new("you", "Learns Swift"), Self.today)

        #expect(edit.reply.contains("forgot F3: fact 3"))
        #expect(edit.memory.find("F1") != nil)
        #expect(edit.memory.find("F2") != nil)
    }

    @Test
    func factsOfOtherCategoriesAreNeverForgottenToMakeRoom() {
        var withOld = Self.full(.you)
        withOld.facts.insert(Self.fact(40, .interests, Self.lastMonth), at: 0)

        let edit = MemoryEdits.remember(withOld, Self.new("you", "Learns Swift"), Self.today)

        #expect(edit.memory.find("F40") != nil)
        #expect(edit.reply.contains("forgot F1:"))
    }

    @Test
    func theReplyCarriesBothNotesWhenAKeyWasDroppedAndAFactForgotten() {
        let edit = MemoryEdits.remember(Self.full(.you), Self.new("you", "Learns Swift", "вопрос"), Self.today)

        #expect(edit.reply ==
            "Saved F9. Keys not kept, they are common words: вопрос. Use names or terms, or no keys."
            + " Memory was full, forgot F1: fact 1")
    }

    @Test
    func aCategoryWhereEveryFactIsPinnedSavesNothingAndSaysSo() {
        let full = Self.full(.you, pinned: true)

        let edit = MemoryEdits.remember(full, Self.new("you", "Learns Swift"), Self.today)

        #expect(!edit.changed)
        #expect(edit.memory == full)
        #expect(edit.reply ==
            "\"You\" is full and every fact in it is pinned, so none can be forgotten to make room."
            + " Nothing was saved. Merge two close facts into one (remember with an id, then forget the other),"
            + " then call again.")
    }

    @Test
    func fullWorkHabitsSaveNothingAndAskTheModelToMergeOrDrop() {
        let full = Self.full(.work)

        let edit = MemoryEdits.remember(full, Self.new("work", "Plan before code"), Self.today)

        let lines = edit.reply.split(separator: "\n", omittingEmptySubsequences: false).map(String.init)
        #expect(!edit.changed)
        #expect(edit.memory == full)
        #expect(lines.count == 14)
        #expect(lines.first == "\"How to work with me\" is full (12 facts). Nothing was saved.")
        #expect(lines.dropFirst(1).first == "[F1] fact 1")
        #expect(lines.dropFirst(12).first == "[F12] fact 12")
        #expect(lines.dropFirst(13).first ==
            "Merge two close facts into one (remember with an id, then forget the other),"
            + " or forget the one that matters least. Then call again.")
    }

    @Test
    func afterTheModelForgetsOneWorkHabitTheNewOneIsSaved() {
        let room = MemoryEdits.forget(Self.full(.work), "F5").memory

        let edit = MemoryEdits.remember(room, Self.new("work", "Plan before code"), Self.today)

        #expect(edit.reply == "Saved F13.")
        #expect(edit.memory.facts.count == 12)
    }

    // ---- an existing fact ----

    static func oneProject() -> MemoryFile {
        MemoryFile(
            4,
            [
                MemoryFact(
                    "F3", .projects, "Makes Aiko",
                    meaning: "A tray app",
                    keys: ["Aiko"],
                    seen: lastMonth,
                    created: lastMonth),
            ],
            [])
    }

    @Test
    func anUnknownIdChangesNothing() {
        let memory = Self.oneProject()

        let edit = MemoryEdits.remember(memory, MemoryRequest(id: "F9", category: "you", text: "new"), Self.today)

        #expect(!edit.changed)
        #expect(edit.memory == memory)
        #expect(edit.reply == "No fact F9. Nothing changed.")
    }

    @Test
    func anIdAloneRefreshesTheFactAndKeepsTheRest() {
        let edit = MemoryEdits.remember(Self.oneProject(), MemoryRequest(id: "f3", category: "projects"), Self.today)

        #expect(edit.memory.facts.count == 1)
        let fact = edit.memory.facts[0]
        #expect(edit.changed)
        #expect(edit.reply == "Refreshed F3.")
        #expect(fact.seen == Self.today)
        #expect(fact.created == Self.lastMonth)
        #expect(fact.text == "Makes Aiko")
        #expect(fact.meaning == "A tray app")
        #expect(fact.keys == ["Aiko"])
        #expect(edit.memory.nextId == 4)
    }

    @Test
    func theFieldsThatWereGivenAreChanged() {
        let edit = MemoryEdits.remember(
            Self.oneProject(),
            MemoryRequest(
                id: "F3",
                text: "Makes Aiko, a tray app",
                meaning: "Knows WPF and SwiftUI",
                keys: ["Aiko", "трей", "WPF"]),
            Self.today)

        #expect(edit.memory.facts.count == 1)
        let fact = edit.memory.facts[0]
        #expect(fact.text == "Makes Aiko, a tray app")
        #expect(fact.meaning == "Knows WPF and SwiftUI")
        #expect(fact.keys == ["Aiko", "WPF"])
        #expect(edit.reply
            == "Refreshed F3. Keys not kept, they are common words: трей. Use names or terms, or no keys.")
    }

    @Test
    func aFactStaysInItsCategoryWhateverTheCallSays() {
        let edit = MemoryEdits.remember(Self.oneProject(), MemoryRequest(id: "F3", category: "interests"), Self.today)

        #expect(edit.memory.facts[0].category == .projects)
    }

    @Test
    func pinningAFactTakesItsKeysAway() {
        let edit = MemoryEdits.remember(
            Self.oneProject(), MemoryRequest(id: "F3", pin: true, keys: ["Aiko"]), Self.today)

        #expect(edit.memory.facts[0].pinned)
        #expect(edit.memory.facts[0].keys.isEmpty)
        #expect(edit.reply == "Refreshed F3.")
    }

    @Test
    func aFactCanBeUnpinnedAndGivenKeysInOneCall() {
        let pinned = MemoryEdits.remember(
            Self.oneProject(), MemoryRequest(id: "F3", pin: true), Self.yesterday).memory

        let edit = MemoryEdits.remember(pinned, MemoryRequest(id: "F3", pin: false, keys: ["Aiko"]), Self.today)

        #expect(!edit.memory.facts[0].pinned)
        #expect(edit.memory.facts[0].keys == ["Aiko"])
    }

    @Test
    func aChangeThatMakesTheFactTooLongChangesNothing() {
        let memory = Self.oneProject()

        let edit = MemoryEdits.remember(
            memory, MemoryRequest(id: "F3", meaning: String(repeating: "m", count: 187)), Self.today)

        #expect(!edit.changed)
        #expect(edit.memory == memory)
        #expect(edit.reply ==
            "Too long: the fact, its meaning and keys are 201 characters together, and the limit is 200."
            + " Nothing changed. Shorten it to one thought and call again.")
    }

    @Test
    func theSameChangeOneCharacterShorterGoesThrough() {
        let edit = MemoryEdits.remember(
            Self.oneProject(), MemoryRequest(id: "F3", meaning: String(repeating: "m", count: 186)), Self.today)

        #expect(edit.changed)
        #expect(MemoryRules.lengthOf(edit.memory.facts[0]) == 200)
    }

    // ---- forgetting ----

    @Test
    func aForgottenFactIsGoneAndItsIdIsNotGivenAgain() {
        let forgotten = MemoryEdits.forget(Self.oneProject(), "F3")
        let next = MemoryEdits.remember(forgotten.memory, Self.new("you", "Learns Swift"), Self.today)

        #expect(forgotten.changed)
        #expect(forgotten.reply == "Forgot F3.")
        #expect(forgotten.memory.facts.isEmpty)
        #expect(next.memory.facts[0].id == "F4")
    }

    @Test(arguments: ["F9", "", nil] as [String?])
    func forgettingAnUnknownIdChangesNothing(id: String?) {
        let memory = Self.oneProject()

        let edit = MemoryEdits.forget(memory, id)

        #expect(!edit.changed)
        #expect(edit.memory == memory)
        #expect(edit.reply == "No fact \(id ?? ""). Nothing changed.")
    }

    // ---- a key in the person's message ----

    static func keyed() -> MemoryFile {
        MemoryFile(
            5,
            [
                MemoryFact("F1", .you, "Learns Swift", keys: ["Swift"], seen: lastMonth, created: lastMonth),
                MemoryFact("F2", .you, "Works on a Mac", keys: ["Мак", "macOS"], seen: lastMonth, created: lastMonth),
                MemoryFact("F3", .you, "Writes C#", keys: ["C#"], seen: today, created: lastMonth),
                MemoryFact("F4", .work, "Short answer first", seen: lastMonth, created: lastMonth),
            ],
            ["{}"])
    }

    @Test
    func aKeyInTheMessageMakesItsFactSeenToday() {
        let touch = MemoryEdits.touchByPrompt(Self.keyed(), "собери экран на SwiftUI и проверь на маке", Self.today)

        #expect(touch.touched == ["F1", "F2"])
        #expect(touch.memory.find("F1")?.seen == Self.today)
        #expect(touch.memory.find("F2")?.seen == Self.today)
        #expect(touch.memory.find("F4")?.seen == Self.lastMonth)
        #expect(touch.memory.find("F1")?.created == Self.lastMonth)
        #expect(touch.memory.setAside == ["{}"])
    }

    @Test
    func aFactAlreadySeenTodayIsNotNamedAgain() {
        let touch = MemoryEdits.touchByPrompt(Self.keyed(), "перепиши это на C#", Self.today)

        #expect(touch.touched.isEmpty)
    }

    @Test
    func aMessageWithoutKeysGivesBackTheSameMemory() {
        let memory = Self.keyed()

        let touch = MemoryEdits.touchByPrompt(memory, "напиши макрос для swiftness", Self.today)

        #expect(touch.touched.isEmpty)
        #expect(touch.memory == memory)
    }

    @Test
    func aTouchedFactIsNoLongerTheFirstToBeForgotten() {
        let full = Self.changing(Self.full(.projects), where: { $0.id == "F1" }) { $0.keys = ["Ember"] }

        let touched = MemoryEdits.touchByPrompt(full, "что там с Ember?", Self.today).memory
        let edit = MemoryEdits.remember(touched, Self.new("projects", "Makes a card game"), Self.today)

        #expect(edit.reply.contains("forgot F2:"))
    }
}
