import Testing

@testable import AikoKit

struct PersonaSettingsTests {
    @Test
    func byDefaultAikoIsNormalAndChibi() {
        let persona = PersonaSettings.default

        #expect(persona.temperament == .normal)
        #expect(persona.face == .chibi)
    }

    @Test
    func thePersonaSurvivesATripThroughTheFile() {
        var persona = PersonaSettings.default
        persona.temperament = .musou
        persona.face = .emoji

        let back = PersonaSettings.fromJson(persona.toJson())

        #expect(back == persona)
    }

    @Test
    func choicesAreWrittenAsWords() {
        var persona = PersonaSettings.default
        persona.temperament = .musou
        persona.face = .emoji

        let json = persona.toJson()

        #expect(json.contains("\"Musou\""))
        #expect(json.contains("\"Emoji\""))
    }

    @Test(arguments: [
        "", "not json at all", #"{ "temperament": "#, #"{ "temperament": "Shouting" }"#, #"{ "face": "Pixel" }"#,
    ])
    func aBrokenFileOrAnUnknownChoiceReadsAsTheDefaults(json: String) {
        #expect(PersonaSettings.fromJson(json) == PersonaSettings.default)
    }

    /// The skills had switches of their own until 0.2.1 and one switch for all until 0.3.1. They
    /// come with Aiko now (D-285), and a file from those days must still read.
    @Test(arguments: [
        #"{ "temperament": "Quiet", "disabledSkills": ["aiko-copy", "aiko-palette"] }"#,
        #"{ "temperament": "Quiet", "skillsOn": false }"#,
    ])
    func aFileWithASkillsSwitchOfAnOlderAikoStillReads(json: String) {
        let persona = PersonaSettings.fromJson(json)

        #expect(persona.temperament == .quiet)
        #expect(!persona.toJson().contains("skillsOn"))
        #expect(!persona.toJson().contains("disabledSkills"))
    }
}
