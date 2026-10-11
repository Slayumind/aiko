import Foundation
import Testing

@testable import AikoKit

/// The two helpers the memory has only in Swift: text read in .NET's terms, and JSON values as
/// they were written. Windows gets both from .NET itself, so these cases hold what .NET 10
/// answers, measured there on 2026-10-11 for every UTF-16 unit of the basic plane.
struct MemoryTextTests {
    // ---- white space ----

    @Test(arguments: [
        (0x20, true), (0x09, true), (0x0A, true), (0x0B, true), (0x0C, true), (0x0D, true),
        (0x85, true), (0xA0, true), (0x2003, true), (0x2028, true), (0x2029, true), (0x3000, true),
        (0x1C, false), (0x1F, false), (0x200B, false), (0xFEFF, false), (0x61, false),
    ])
    func whiteSpaceIsWhatDotNetTrimsAndSplitsOn(value: Int, expected: Bool) {
        #expect(MemoryText.isWhiteSpace(Unicode.Scalar(value)!) == expected)
    }

    @Test
    func aTextIsTrimmedAndPutOnOneLineAsOnWindows() {
        #expect(MemoryText.trim("\u{A0} f12\t\r\n") == "f12")
        #expect(MemoryText.trim(" \u{2003} ") == "")
        #expect(MemoryText.oneLine("  a\u{A0}\u{2003}b\r\nc\u{1F}d  ") == "a b c\u{1F}d")
        #expect(MemoryText.oneLine(nil) == "")
        #expect(MemoryText.isBlank(nil))
        #expect(MemoryText.isBlank(" \u{85}\n"))
        #expect(!MemoryText.isBlank(" \u{1F} "))
    }

    // ---- a word character ----

    @Test(arguments: [
        ("a", true), ("Z", true), ("7", true), ("_", true), ("я", true), ("Ё", true), ("日", true),
        ("\u{301}", true), ("\u{663}", true), ("\u{203F}", true),
        ("-", false), ("'", false), ("#", false), (".", false), (" ", false),
        ("\u{2167}", false), ("\u{B2}", false), ("\u{200D}", false), ("\u{93E}", false),
    ])
    func aWordCharacterIsALetterAMarkADigitOrAConnector(character: String, expected: Bool) {
        #expect(MemoryText.isWordUnit(character.utf16.first!) == expected)
    }

    @Test
    func halfOfALetterAboveTheBasicPlaneIsNotAWordCharacter() {
        #expect("\u{1D49C}".utf16.allSatisfy { !MemoryText.isWordUnit($0) })
        #expect(MemoryRules.keyMatches("x", "\u{1D49C}x"))
    }

    // ---- the same letter in another case ----

    @Test(arguments: [
        // Я я, Ё ё, S s, the Kelvin sign and k, the capital ẞ and ß, Σ σ, ǅ ǆ, Ǆ ǅ.
        ("\u{42F}", "\u{44F}", true), ("\u{401}", "\u{451}", true), ("S", "s", true), ("\u{212A}", "k", true),
        ("\u{1E9E}", "\u{DF}", true), ("\u{3A3}", "\u{3C3}", true), ("\u{1C5}", "\u{1C6}", true),
        ("\u{1C4}", "\u{1C5}", true),

        // The final sigma ς and σ or Σ, the long ſ and s or S, the micro sign and μ, the dotted İ
        // and i, the dotless ı and i or I, е and ё, e and é.
        ("\u{3C2}", "\u{3C3}", false), ("\u{3C2}", "\u{3A3}", false), ("\u{17F}", "s", false),
        ("\u{17F}", "S", false), ("\u{B5}", "\u{3BC}", false), ("\u{130}", "i", false), ("\u{131}", "i", false),
        ("\u{131}", "I", false), ("\u{435}", "\u{451}", false), ("e", "\u{E9}", false),
    ])
    func twoLettersAreTheSameWhenTheirSmallLetterIs(left: String, right: String, same: Bool) {
        #expect((MemoryText.folded(left) == MemoryText.folded(right)) == same)
        #expect(MemoryRules.keyMatches(left, right) == same)
    }

    @Test
    func categoriesAndIdsFoldOnlyAToZ() {
        #expect(MemoryRules.categoryOf(" WORK\t") == .work)
        #expect(MemoryRules.categoryOf("wor\u{212A}") == nil)
        #expect(MemoryFile(2, [MemoryFact("F1", .you, "t")], []).find("\u{FF26}1") == nil)
    }

    // ---- what .NET does at the edges of the file, which no xUnit case holds ----

    @Test
    func entriesSetAsideFollowTheBracketWithNothingBetweenThem() {
        let memory = MemoryFile(1, [], ["{ \"x\":1 }", "2", "\"three\""])

        #expect(memory.toJson()
            == "{\n  \"schema\": 1,\n  \"nextId\": 1,\n  \"facts\": [],\n  \"setAside\": [{ \"x\":1 },2,\"three\"\n  ]\n}\n")
    }

    @Test
    func theNumberAfterTheLargestIdWrapsAroundAsItDoesIn32Bits() {
        let last = MemoryFile(3, [MemoryFact("F2147483647", .you, "t")], [])
        #expect(last.firstFreeNumber == 3)

        let edit = MemoryEdits.remember(
            MemoryFile(2_147_483_647, [], []), MemoryRequest(category: "you", text: "t"), DateOnly(2026, 10, 11))
        #expect(edit.reply == "Saved F2147483647.")
        #expect(edit.memory.toJson().contains("\"nextId\": 1,"))
    }

    @Test(arguments: [
        ("2024-02-29", true), ("0001-01-01", true), ("9999-12-31", true),
        ("2023-02-29", false), ("0000-01-01", false), ("2026-13-01", false), ("2026-04-31", false),
        ("2026-1-01", false), (" 2026-10-09", false), ("2026-10-09T00:00:00", false),
        ("\u{662}\u{660}\u{662}\u{666}-10-09", false), ("", false),
    ])
    func aDayIsReadOnlyInTheOneShapeItIsWrittenIn(text: String, valid: Bool) {
        #expect((MemoryFile.day(text) != nil) == valid)
    }

    // ---- JSON as it was written ----

    @Test
    func theItemsOfAListKeepTheirSpacesAndTheirNumbers() {
        #expect(JsonRawText.items(" [ { \"a\" : [1, 2] } ,1.50,\"x,]\\\"y\" , null,[ ] ] ")
            == ["{ \"a\" : [1, 2] }", "1.50", "\"x,]\\\"y\"", "null", "[ ]"])
        #expect(JsonRawText.items("[]") == [])
        #expect(JsonRawText.items("{}") == nil)
    }

    @Test
    func theMembersOfAnObjectComeInOrderWithRepeatedNamesKept() {
        let members = JsonRawText.members("{ \"id\" : \"a\\u0062\", \"n\":{\"id\":2}, \"id\": 7 }")

        #expect(members?.map(\.key) == ["id", "n", "id"])
        #expect(members?.map(\.value) == ["\"a\\u0062\"", "{\"id\":2}", "7"])
        #expect(JsonRawText.members("[1]") == nil)
        #expect(JsonRawText.members("{}")?.isEmpty == true)
    }

    @Test
    func theIdOfAMessageGoesBackWithTheTextItCameWith() {
        let reply = MemoryServer.answer(
            "{\"id\": 1.50 ,\"method\":\"ping\"}", .empty, DateOnly(2026, 10, 11), "0.4.0")

        #expect(reply.line == "{\"jsonrpc\":\"2.0\",\"id\":1.50,\"result\":{}}")
    }
}
