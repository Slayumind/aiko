import Testing

@testable import AikoKit

struct PersonaSettingsTests {
    @Test
    func byDefaultAikoIsNormalChibiWithTheSkillsOn() {
        let persona = PersonaSettings.default

        #expect(persona.temperament == .normal)
        #expect(persona.face == .chibi)
        #expect(persona.skillsOn)
    }

    @Test
    func thePersonaSurvivesATripThroughTheFile() {
        var persona = PersonaSettings.default
        persona.temperament = .musou
        persona.face = .emoji
        persona.skillsOn = false

        let back = PersonaSettings.fromJson(persona.toJson())

        #expect(back == persona)
        #expect(persona.toJson().contains("\"skillsOn\": false"))
        #expect(!persona.toJson().contains("disabledSkills"))
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

    // ---- files written when each skill had its own switch ----

    @Test(arguments: [
        #"{ "temperament": "Quiet" }"#,
        #"{ "temperament": "Quiet", "disabledSkills": null }"#,
        #"{ "temperament": "Quiet", "disabledSkills": [] }"#,
        #"{ "temperament": "Quiet", "disabledSkills": ["aiko-copy", "aiko-palette"] }"#,
        #"{ "temperament": "Quiet", "disabledSkills": ["copy", "", null] }"#,
    ])
    func anOldFileWithAnySkillLeftOnHasTheSkillsOn(json: String) {
        let persona = PersonaSettings.fromJson(json)

        #expect(persona.temperament == .quiet)
        #expect(persona.skillsOn)
    }

    @Test
    func anOldFileWithEverySkillOffHasTheSkillsOff() {
        let oldNames = PersonaSettings.fromJson("""
            { "disabledSkills": ["aiko-copy", "aiko-release-gate", "aiko-docs-hygiene", "aiko-blender-to-unity",
                                 "aiko-texturing", "aiko-glb-for-web", "aiko-palette", "aiko-gamedesign-research"] }
            """)
        let newNames = PersonaSettings.fromJson("""
            { "disabledSkills": ["copy", "release-gate", "docs-hygiene", "blender-to-unity",
                                 "texturing", "glb-for-web", "palette", "gamedesign-research"] }
            """)

        #expect(!oldNames.skillsOn)
        #expect(!newNames.skillsOn)
    }

    @Test
    func theNewSwitchWinsOverAnOldListInTheSameFile() {
        let persona = PersonaSettings.fromJson(
            #"{ "skillsOn": true, "disabledSkills": ["copy", "release-gate", "docs-hygiene", "blender-to-unity", "texturing", "glb-for-web", "palette", "gamedesign-research"] }"#
        )

        #expect(persona.skillsOn)
    }
}
