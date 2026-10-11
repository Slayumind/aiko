import Foundation
import Testing

@testable import AikoKit

/// The two texts the model reads about the memory, and the note for a turn. Claude Code cuts a
/// hook output at 10,000 characters, so the worst case of each is measured here. It counts UTF-16
/// units, and so do these cases.
struct MemoryPromptTests {
    static let hookOutputLimit = 10_000

    static let today = DateOnly(2026, 10, 11)

    // ---- the rules ----

    @Test
    func theRulesEndWithTheDay() {
        let rules = MemoryPrompt.rules(DateOnly(2026, 1, 9))

        #expect(rules.hasPrefix("# Aiko memory\n\n"))
        #expect(rules.hasSuffix("\n\nToday: 2026-01-09."))
    }

    @Test
    func theRulesFitOneHookOutput() {
        #expect((1...Self.hookOutputLimit).contains(MemoryPrompt.rules(Self.today).utf16.count))
    }

    @Test
    func theRulesSayNothingAboutSidesOrScope() {
        let rules = MemoryPrompt.rules(Self.today)

        #expect(!rules.contains("scope"))
        #expect(!rules.contains("side"))
        #expect(!rules.contains("{"))
    }

    @Test(arguments: [
        "at most 200 characters",
        "One thought per fact",
        "\"migration is half done\"",
        "only what stays true",
        "refresh it by id, once per session",
        "When \"How to work with me\" is full",
        "Do not skip the save",
        "Save silently",
    ])
    func theRulesCarryWhatTheProductAddedToTheSpike(phrase: String) {
        #expect(MemoryText.oneLine(MemoryPrompt.rules(Self.today)).contains(phrase))
    }

    @Test
    func theRulesNameTheLimitsTheCodeEnforces() {
        let rules = MemoryPrompt.rules(Self.today)

        #expect(rules.contains("at most \(MemoryRules.maxLength) characters"))
        #expect(rules.contains("1-\(MemoryRules.maxKeys) keys"))
        #expect(rules.contains("\"\(MemoryRules.title(.work))\""))
        #expect(rules.contains(String(MemoryPrompt.factsHeading.dropFirst(2))))
    }

    @Test
    func theRulesHaveNoCarriageReturnsWhateverTheCheckoutDid() {
        #expect(!MemoryPrompt.rules(Self.today).unicodeScalars.contains("\r"))
    }

    // ---- the facts ----

    @Test
    func anEmptyMemoryShowsEveryCategoryAsEmpty() {
        #expect(MemoryPrompt.facts(.empty) ==
            "# Aiko memory: facts\n\n"
            + "## You (0/8)\n- (empty)\n\n"
            + "## How to work with me (0/12)\n- (empty)\n\n"
            + "## Projects (0/12)\n- (empty)\n\n"
            + "## Interests and taste (0/10)\n- (empty)")
    }

    @Test
    func aFactShowsItsMeaningPinAndKeysOnlyWhenItHasThem() {
        let today = Self.today
        let memory = MemoryFile(
            20,
            [
                MemoryFact("F12", .projects, "Makes Aiko", meaning: "A tray app", keys: ["Aiko", "трей"], seen: today),
                MemoryFact("F1", .you, "Name is Sasha", meaning: "Call them Sasha", pinned: true, seen: today),
                MemoryFact("F2", .you, "Learns Swift", keys: ["Swift"], seen: today),
                MemoryFact("F3", .work, "Short answer first", seen: today),
                MemoryFact("F4", .work, "Plan before code", pinned: true, seen: today),
            ],
            ["{ \"id\": \"F9\", \"text\": \"set aside\" }"])

        #expect(MemoryPrompt.facts(memory) ==
            "# Aiko memory: facts\n\n"
            + "## You (2/8)\n"
            + "- [F1] Name is Sasha — Call them Sasha · pinned\n"
            + "- [F2] Learns Swift · keys: Swift\n\n"
            + "## How to work with me (2/12)\n"
            + "- [F3] Short answer first\n"
            + "- [F4] Plan before code · pinned\n\n"
            + "## Projects (1/12)\n"
            + "- [F12] Makes Aiko — A tray app · keys: Aiko, трей\n\n"
            + "## Interests and taste (0/10)\n- (empty)")
    }

    @Test
    func theFactsCarryNoDates() {
        let memory = MemoryFile(
            2, [MemoryFact("F1", .you, "Learns Swift", seen: Self.today, created: Self.today)], [])

        #expect(!MemoryPrompt.facts(memory).contains("2026"))
    }

    /// Every category at its quota, every fact at the length limit and with the longest line a
    /// fact of that kind can have: three keys of one letter where keys are allowed (each key
    /// costs a separator the limit does not count), a pin where they are not.
    @Test(arguments: [1, 99_958, 999_999_958])
    func aFullMemoryOfTheLongestFactsFitsOneHookOutput(firstNumber: Int) {
        let memory = Self.fullOfLongestFacts(firstNumber, pinEverything: false)

        #expect(memory.facts.count == 42)
        #expect(memory.facts.allSatisfy { MemoryRules.lengthOf($0) == MemoryRules.maxLength })
        #expect((9_000...Self.hookOutputLimit).contains(MemoryPrompt.facts(memory).utf16.count))
    }

    @Test
    func aFullMemoryOfPinnedFactsFitsToo() {
        let memory = Self.fullOfLongestFacts(99_958, pinEverything: true)

        #expect(memory.facts.allSatisfy { $0.pinned })
        #expect((9_000...Self.hookOutputLimit).contains(MemoryPrompt.facts(memory).utf16.count))
    }

    @Test
    func theLongestFactsAreOnesTheToolWouldReallySave() {
        var memory = MemoryFile.empty
        memory.nextId = 99_958
        for category in MemoryRules.categories {
            for _ in 0..<MemoryRules.quota(category) {
                let edit = MemoryEdits.remember(
                    memory,
                    MemoryRequest(
                        category: MemoryRules.nameOf(category),
                        text: String(repeating: "t", count: 100),
                        meaning: String(repeating: "m", count: category == .work ? 100 : 97),
                        pin: category == .work,
                        keys: ["A", "B", "C"]),
                    Self.today)

                #expect(edit.reply.hasPrefix("Saved"))
                memory = edit.memory
            }
        }

        #expect(MemoryPrompt.facts(memory)
            == MemoryPrompt.facts(Self.fullOfLongestFacts(99_958, pinEverything: false)))
    }

    static func fullOfLongestFacts(_ firstNumber: Int, pinEverything: Bool) -> MemoryFile {
        var facts: [MemoryFact] = []
        for category in MemoryRules.categories {
            for _ in 0..<MemoryRules.quota(category) {
                let keyed = !pinEverything && MemoryRules.takesKeys(category, pinned: false)
                facts.append(MemoryFact(
                    MemoryFact.idFor(firstNumber + facts.count), category, String(repeating: "t", count: 100),
                    meaning: String(repeating: "m", count: keyed ? 97 : 100),
                    pinned: !keyed,
                    keys: keyed ? ["A", "B", "C"] : [],
                    seen: today,
                    created: today))
            }
        }

        return MemoryFile(firstNumber + facts.count, facts, [])
    }

    // ---- the note for a turn ----

    @Test
    func theNoteTellsTheModelToStaySilentWhenThereIsNothingToSave() {
        #expect(MemoryNudge.text.hasPrefix("Aiko memory check:"))
        #expect(MemoryNudge.text.contains("call `remember` before you start the task"))
        #expect(MemoryNudge.text.hasSuffix("ignore this note and do not mention it."))
        #expect(!MemoryNudge.text.contains("\n"))
    }

    @Test(arguments: [
        ("у меня баг", true),
        ("I like short answers", true),
        ("запусти тесты", false),
        ("", false),
        (nil, false),
    ] as [(String?, Bool)])
    func theNoteGoesToMessagesInTheFirstPerson(prompt: String?, nudge: Bool) {
        #expect(MemoryNudge.applies(prompt) == nudge)
    }
}
