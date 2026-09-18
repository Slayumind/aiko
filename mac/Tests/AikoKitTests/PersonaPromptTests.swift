import Foundation
import Testing

@testable import AikoKit

struct PersonaPromptTests {
    static let bridge = #"C:\Users\Тест Юзер\AppData\Local\Slayumind.Aiko\current\Aiko.Bridge.exe"#

    @Test(arguments: Temperament.allCases)
    func guardrailsAreTheLastBlockAtEveryTemperament(temperament: Temperament) {
        let prompt = PersonaPrompt.compose(temperament)

        #expect(prompt.hasSuffix(PersonaPrompt.guardrails.trimmedAtEnd() + "\n"))
        #expect(prompt.contains("commit messages"))
        #expect(prompt.contains("security warnings"))
    }

    /// The guardrails come last and tell Aiko to go neutral. Read alone, "neutral" takes the whole
    /// voice block with it, including the line that says she is a woman — which is how a live
    /// session ended up writing about her in the masculine. The rule has to be repeated inside the
    /// guardrails themselves, after the list, or it is not there when it is needed.
    @Test(arguments: Temperament.allCases)
    func theSilentPlacesKeepTheLanguageAndTheFeminineForms(temperament: Temperament) throws {
        let prompt = PersonaPrompt.compose(temperament)
        let start = try #require(prompt.range(of: "# Where you stay silent"))
        let guardrails = String(prompt[start.lowerBound...])

        #expect(guardrails.contains("feminine forms"))
        #expect(guardrails.contains("the user's language"))
    }

    /// A working day is a row of reports, plans and warnings. Without this the persona goes quiet
    /// at the first one and never comes back.
    @Test(arguments: Temperament.allCases)
    func theSilenceBelongsToThePlaceAndNotToTheSession(temperament: Temperament) {
        let prompt = PersonaPrompt.compose(temperament)

        #expect(prompt.contains("the silence belongs to the place"))
        #expect(prompt.contains("not one long silent place"))
    }

    @Test
    func eachTemperamentHasItsOwnBlock() {
        let blocks = Temperament.allCases.map(PersonaPrompt.compose)

        #expect(blocks.count == Set(blocks).count)
        #expect(PersonaPrompt.compose(.musou).contains("# Temperament: musou"))
    }

    @Test(arguments: Temperament.allCases)
    func onlyTheAllowedJapaneseAppears(temperament: Temperament) throws {
        let style = PersonaPrompt.styleFile(temperament)
        let regex = try NSRegularExpression(pattern: "[\u{3040}-\u{30ff}\u{4e00}-\u{9fff}]+")
        let text = style as NSString
        let runs = Set(
            regex.matches(in: style, range: NSRange(location: 0, length: text.length))
                .map { text.substring(with: $0.range) })

        for run in runs {
            #expect(PersonaPrompt.allowedJapanese.contains(run), "unexpected Japanese: \(run)")
        }
    }

    /// Being a woman is who Aiko is, not how loud she talks, so it lives next to her name. Russian
    /// examples cover the short adjectives too («готова», «уверена»): those slipped past the old rule.
    @Test(arguments: Temperament.allCases)
    func whoSheIsSaysSheIsAWomanWithRussianExamples(temperament: Temperament) throws {
        let prompt = PersonaPrompt.compose(temperament)
        let end = try #require(prompt.range(of: "# How you talk"))
        let character = String(prompt[..<end.lowerBound])

        #expect(character.contains("You are a woman"))
        for form in ["проверила", "нашла", "готова", "уверена"] {
            #expect(character.contains(form))
        }
    }

    /// A game named at the end of every report about patches and checklists reads as a tic. A game is
    /// named only when the thing at hand really works like it, at every temperament.
    @Test(arguments: Temperament.allCases)
    func aGameIsNamedOnlyForARealLikeness(temperament: Temperament) {
        let prompt = PersonaPrompt.compose(temperament)

        #expect(prompt.contains("Name a game only when"))
        #expect(prompt.contains("never as the closing joke"))
        #expect(!prompt.contains("in any topic"))
        #expect(!prompt.contains("creative task"))
    }

    @Test
    func theFavouriteGamesAreNamed() {
        let prompt = PersonaPrompt.compose(.normal)

        for game in ["Undertale", "Souls", "Elden Ring", "Slay the Spire", "Skyrim"] {
            #expect(prompt.contains(game))
        }
    }

    @Test(arguments: Temperament.allCases)
    func theStyleKeepsTheCodingInstructionsAndTurnsOnWithThePlugin(temperament: Temperament) {
        let style = PersonaPrompt.styleFile(temperament)

        #expect(style.hasPrefix("---\nname: Aiko\n"))
        #expect(style.contains("\nkeep-coding-instructions: true\n"))
        #expect(style.contains("\nforce-for-plugin: true\n"))
    }

    @Test
    func thePluginHasAManifestAStyleAndHooks() {
        let files = PersonaPlugin.files(.normal, Self.bridge)

        #expect(files.map(\.path).sorted() == [".claude-plugin/plugin.json", "hooks/hooks.json", "output-styles/aiko.md"])
        let manifest = JsonNode.parse(files.first { $0.path == ".claude-plugin/plugin.json" }?.content)
        #expect(manifest?["name"]?.stringValue == "aiko-persona")
    }

    @Test
    func hooksRunTheBridgeWithoutAShellEvenWithSpacesAndCyrillicInThePath() throws {
        let hooksFile = try #require(PersonaPlugin.files(.normal, Self.bridge).first { $0.path == "hooks/hooks.json" })
        let events = try #require(JsonNode.parse(hooksFile.content)?["hooks"]?.objectValue)

        #expect(events.keys == PersonaPlugin.hookEvents)
        for member in events.members {
            let hook = try #require(member.value.arrayValue?.first?["hooks"]?.arrayValue?.first)
            #expect(hook["type"]?.stringValue == "command")
            #expect(hook["command"]?.stringValue == Self.bridge)
            #expect(hook["args"]?.arrayValue?.compactMap(\.stringValue) == ["hook"])
            #expect(hook["async"]?.boolValue == true)
        }
    }

    @Test
    func theHashIsStableAndFollowsTheContent() throws {
        let normal = PersonaPlugin.contentHash(PersonaPlugin.files(.normal, Self.bridge))

        #expect(normal.count == 12)
        #expect(normal.allSatisfy { $0.isHexDigit && !$0.isUppercase })
        #expect(normal == PersonaPlugin.contentHash(PersonaPlugin.files(.normal, Self.bridge)))
        #expect(normal != PersonaPlugin.contentHash(PersonaPlugin.files(.musou, Self.bridge)))
        #expect(normal != PersonaPlugin.contentHash(PersonaPlugin.files(.normal, #"D:\Aiko.Bridge.exe"#)))
    }
}

extension String {
    /// The same as TrimEnd() in C#: white space at the end goes.
    func trimmedAtEnd() -> String {
        var text = self
        while let last = text.unicodeScalars.last, CharacterSet.whitespacesAndNewlines.contains(last) {
            text.unicodeScalars.removeLast()
        }
        return text
    }
}
