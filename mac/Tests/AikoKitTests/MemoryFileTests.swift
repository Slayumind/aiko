import Foundation
import Testing

@testable import AikoKit

/// memory.json is the person's own data. What is checked here: nothing in it is lost on the way
/// through Aiko, not even the entries Aiko cannot use.
struct MemoryFileTests {
    static let monday = DateOnly(2026, 10, 5)
    static let friday = DateOnly(2026, 10, 9)

    static func twoFacts() -> MemoryFile {
        MemoryFile(
            19,
            [
                MemoryFact(
                    "F3", .you, "Учит Swift, \"я дописала\"",
                    meaning: "Explain Swift ideas through C#",
                    keys: ["Swift", "C++"],
                    seen: friday,
                    created: monday),
                MemoryFact("F12", .work, "Short answer first", pinned: true, seen: monday, created: monday),
            ],
            [])
    }

    // ---- the trip through the file ----

    @Test
    func theMemorySurvivesATripThroughTheFile() {
        let read = MemoryFile.fromJson(Self.twoFacts().toJson())

        #expect(!read.unreadable)
        #expect(read.memory.nextId == 19)
        #expect(read.memory.setAside.isEmpty)
        #expect(read.memory.facts.count == 2)

        let swift = read.memory.facts[0]
        #expect(swift.id == "F3")
        #expect(swift.category == .you)
        #expect(swift.text == "Учит Swift, \"я дописала\"")
        #expect(swift.meaning == "Explain Swift ideas through C#")
        #expect(!swift.pinned)
        #expect(swift.keys == ["Swift", "C++"])
        #expect(swift.seen == Self.friday)
        #expect(swift.created == Self.monday)

        let habit = read.memory.facts[1]
        #expect(habit.category == .work)
        #expect(habit.pinned)
        #expect(habit.keys.isEmpty)
        #expect(habit.meaning == "")
    }

    @Test
    func aSecondTripWritesTheSameText() {
        let once = Self.twoFacts().toJson()

        #expect(MemoryFile.fromJson(once).memory.toJson() == once)
    }

    @Test
    func theFileIsIndentedAndNamesItsSchema() {
        let json = Self.twoFacts().toJson()

        #expect(json.hasPrefix("{\n  \"schema\": 1,\n  \"nextId\": 19,"))
        #expect(json.contains("\"category\": \"you\""))
        #expect(json.contains("\"seen\": \"2026-10-09\""))
        #expect(json.contains("\"created\": \"2026-10-05\""))
        #expect(json.contains("\"setAside\": []"))
        #expect(json.hasSuffix("}\n"))
    }

    @Test
    func cyrillicAndPlusSignsStayReadableInTheFile() {
        let json = Self.twoFacts().toJson()

        #expect(json.contains("Учит Swift, \\\"я дописала\\\""))
        #expect(json.contains("\"C++\""))
        #expect(!json.contains("\\u"))
    }

    @Test
    func anEmptyMemoryIsStillAWholeFile() {
        let file = JsonNode.parse(MemoryFile.empty.toJson())

        #expect(file?["schema"]?.int64Value == 1)
        #expect(file?["nextId"]?.int64Value == 1)
        #expect(file?["facts"]?.arrayValue?.count == 0)
        #expect(file?["setAside"]?.arrayValue?.count == 0)
    }

    // ---- no file, and a file that is not the memory ----

    @Test(arguments: [nil, "", "  \r\n "] as [String?])
    func noFileYetIsAnEmptyMemoryWithNothingToKeepAside(json: String?) {
        let read = MemoryFile.fromJson(json)

        #expect(!read.unreadable)
        #expect(read.memory.facts.isEmpty)
        #expect(read.memory.nextId == 1)
    }

    @Test(arguments: [
        "not json at all",
        "{ \"facts\": [",
        "[]",
        "42",
        "{ \"facts\": \"none\" }",
        "{ \"facts\": { \"F1\": {} } }",
    ])
    func aFileThatIsNotTheMemoryReadsAsEmptyAndIsMarked(json: String) {
        let read = MemoryFile.fromJson(json)

        #expect(read.unreadable)
        #expect(read.memory.facts.isEmpty)
        #expect(read.memory.setAside.isEmpty)
        #expect(read.memory.nextId == 1)
    }

    @Test(arguments: [
        "{ \"schema\": 2, \"nextId\": 5, \"facts\": [] }",
        "{ \"schema\": 99999999999, \"facts\": [] }",
    ])
    func aFileFromANewerAikoIsMarkedAndNotReadByHalves(json: String) {
        let read = MemoryFile.fromJson(json)

        #expect(read.unreadable)
        #expect(read.memory.facts.isEmpty)
    }

    @Test(arguments: ["{}", "{ \"schema\": 1 }", "{ \"facts\": null }"])
    func anObjectWithoutFactsIsAnEmptyMemory(json: String) {
        let read = MemoryFile.fromJson(json)

        #expect(!read.unreadable)
        #expect(read.memory.facts.isEmpty)
    }

    // ---- fields that are missing ----

    @Test
    func aFactWithOnlyWhatIsNeededGetsTheDefaults() {
        let facts = MemoryFile.fromJson("""
            { "facts": [ { "id": "F4", "category": "Interests", "text": "Loves Slay the Spire", "seen": "2026-10-09" } ] }
            """).memory.facts

        #expect(facts.count == 1)
        #expect(facts.first?.category == .interests)
        #expect(facts.first?.meaning == "")
        #expect(facts.first?.pinned == false)
        #expect(facts.first?.keys == [])
        #expect(facts.first?.created == Self.friday)
    }

    @Test
    func fieldsAikoDoesNotKnowAreDroppedFromAGoodFact() {
        let read = MemoryFile.fromJson("""
            { "next_id": 7, "facts": [
              { "id": "F4", "category": "projects", "text": "Makes Aiko", "scope": "personal", "refreshed": 3, "seen": "2026-10-09" }
            ] }
            """)

        #expect(read.memory.facts.count == 1)
        #expect(read.memory.setAside.isEmpty)
        #expect(!read.memory.toJson().contains("scope"))
        #expect(!read.memory.toJson().contains("refreshed"))
    }

    @Test
    func moreKeysThanAFactMayHaveAreCutToThree() {
        let facts = MemoryFile.fromJson("""
            { "facts": [ { "id": "F1", "category": "you", "text": "t", "keys": ["A", "B", "C", "D"], "seen": "2026-10-09" } ] }
            """).memory.facts

        #expect(facts.count == 1)
        #expect(facts.first?.keys == ["A", "B", "C"])
    }

    // ---- entries Aiko cannot use ----

    @Test(arguments: [
        #"{ "category": "you", "text": "no id", "seen": "2026-10-09" }"#,
        #"{ "id": "7", "category": "you", "text": "id without F", "seen": "2026-10-09" }"#,
        #"{ "id": "F0", "category": "you", "text": "id zero", "seen": "2026-10-09" }"#,
        #"{ "id": "F07", "category": "you", "text": "a second way to write 7", "seen": "2026-10-09" }"#,
        #"{ "id": 7, "category": "you", "text": "id is a number", "seen": "2026-10-09" }"#,
        #"{ "id": "F7", "category": "family", "text": "unknown category", "seen": "2026-10-09" }"#,
        #"{ "id": "F7", "text": "no category", "seen": "2026-10-09" }"#,
        #"{ "id": "F7", "category": "you", "text": "   ", "seen": "2026-10-09" }"#,
        #"{ "id": "F7", "category": "you", "seen": "2026-10-09" }"#,
        #"{ "id": "F7", "category": "you", "text": ["a list"], "seen": "2026-10-09" }"#,
        #"{ "id": "F7", "category": "you", "text": "no day" }"#,
        #"{ "id": "F7", "category": "you", "text": "a day that is not one", "seen": "09.10.2026" }"#,
        #"{ "id": "F7", "category": "you", "text": "pinned is a word", "pinned": "yes", "seen": "2026-10-09" }"#,
        #"{ "id": "F7", "category": "you", "text": "keys is a word", "keys": "Swift", "seen": "2026-10-09" }"#,
        #"{ "id": "F7", "category": "you", "text": "a key is a number", "keys": ["Swift", 5], "seen": "2026-10-09" }"#,
        #"{ "id": "F7", "category": "you", "text": "meaning is a number", "meaning": 5, "seen": "2026-10-09" }"#,
        #"{ "id": "F7", "category": "you", "text": "created is not a day", "seen": "2026-10-09", "created": "soon" }"#,
        "\"just a string\"",
        "17",
        "null",
    ])
    func anEntryThatCannotBeUsedIsSetAsideAsItWasWritten(entry: String) {
        let read = MemoryFile.fromJson("{ \"facts\": [ " + entry + " ] }")

        #expect(!read.unreadable)
        #expect(read.memory.facts.isEmpty)
        #expect(read.memory.setAside == [entry])
    }

    @Test
    func theGoodFactsAroundABadEntryAreKept() {
        let read = MemoryFile.fromJson("""
            { "facts": [
              { "id": "F1", "category": "you", "text": "first", "seen": "2026-10-09" },
              { "id": "F2", "category": "family", "text": "моя сестра", "extra": { "deep": [1, 2] } },
              { "id": "F3", "category": "work", "text": "third", "seen": "2026-10-09" }
            ] }
            """)

        #expect(read.memory.facts.map(\.id) == ["F1", "F3"])
        #expect(read.memory.setAside == [
            #"{ "id": "F2", "category": "family", "text": "моя сестра", "extra": { "deep": [1, 2] } }"#,
        ])
    }

    @Test
    func aSecondFactWithAnIdAlreadyTakenIsSetAside() {
        let read = MemoryFile.fromJson("""
            { "facts": [
              { "id": "F1", "category": "you", "text": "first", "seen": "2026-10-09" },
              { "id": "F1", "category": "you", "text": "second", "seen": "2026-10-09" }
            ] }
            """)

        #expect(read.memory.facts.map(\.text) == ["first"])
        #expect(read.memory.setAside.count == 1)
        #expect(read.memory.setAside.first?.contains("second") == true)
    }

    @Test
    func whatWasSetAsideSurvivesEveryLaterTripThroughTheFile() {
        let broken = #"{ "id": "F2", "category": "family", "text": "моя сестра + \"кот\"", "n": 1.50 }"#
        let first = MemoryFile.fromJson("""
            { "schema": 1, "nextId": 3, "facts": [
              { "id": "F1", "category": "you", "text": "first", "seen": "2026-10-09" },
            """ + broken + "] }")

        let written = first.memory.toJson()
        let second = MemoryFile.fromJson(written)
        let third = MemoryFile.fromJson(second.memory.toJson())

        #expect(first.memory.setAside == [broken])
        #expect(written.contains(broken))
        #expect(second.memory.setAside == [broken])
        #expect(third.memory.setAside == [broken])
        #expect(third.memory.facts.count == 1)
    }

    @Test
    func anEntryInSetAsideIsNeverReadBackAsAFact() {
        let read = MemoryFile.fromJson("""
            { "facts": [], "setAside": [ { "id": "F5", "category": "you", "text": "looks fine", "seen": "2026-10-09" } ] }
            """)

        #expect(read.memory.facts.isEmpty)
        #expect(read.memory.setAside.count == 1)
    }

    // ---- the number of the next fact ----

    @Test(arguments: [
        (#"{ "nextId": 19, "facts": [ { "id": "F4", "category": "you", "text": "t", "seen": "2026-10-09" } ] }"#, 19),
        (#"{ "nextId": 2, "facts": [ { "id": "F4", "category": "you", "text": "t", "seen": "2026-10-09" } ] }"#, 5),
        (#"{ "facts": [ { "id": "F10", "category": "you", "text": "t", "seen": "2026-10-09" } ] }"#, 11),
        (#"{ "nextId": "many", "facts": [ { "id": "F4", "category": "you", "text": "t", "seen": "2026-10-09" } ] }"#, 5),
        (#"{ "nextId": -3, "facts": [] }"#, 1),
        (#"{ "nextId": 8, "facts": [] }"#, 8),
    ])
    func theNextIdIsNeverOneAFactAlreadyHas(json: String, next: Int) {
        #expect(MemoryFile.fromJson(json).memory.nextId == next)
    }

    @Test
    func aMemoryBuiltWithASmallNextIdIsWrittenWithAFreeOne() {
        var memory = Self.twoFacts()
        memory.nextId = 1

        #expect(memory.firstFreeNumber == 13)
        #expect(memory.toJson().contains("\"nextId\": 13"))
    }

    // ---- ids ----

    @Test(arguments: [
        ("F1", 1),
        ("F12", 12),
        ("F0", nil),
        ("F-1", nil),
        ("F 1", nil),
        ("F01", nil),
        ("f1", nil),
        ("F", nil),
        ("12", nil),
        ("F99999999999", nil),
        (nil, nil),
    ] as [(String?, Int?)])
    func anIdIsFAndANumberFromOneUp(id: String?, number: Int?) {
        #expect(MemoryFact.numberOf(id) == number)
    }

    @Test
    func aFactIsFoundByItsIdHoweverTheModelTypedIt() {
        #expect(Self.twoFacts().find(" f12 ")?.id == "F12")
        #expect(Self.twoFacts().find("F1") == nil)
        #expect(Self.twoFacts().find(nil) == nil)
    }
}
