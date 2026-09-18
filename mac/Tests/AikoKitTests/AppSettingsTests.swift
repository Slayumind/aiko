import Testing

@testable import AikoKit

struct AppSettingsTests {
    @Test
    func byDefaultAikoSitsInTheTrayAndStartsWithTheSystem() {
        let settings = AppSettings.default

        #expect(settings.place == .tray)
        #expect(settings.runAtStartup)
        #expect(settings.language == .system)
        #expect(settings.hideIslandInFullScreen)
    }

    @Test
    func updateChecksAreOffUntilTheUserAgrees() {
        #expect(!AppSettings.default.checkUpdates)
    }

    @Test
    func statisticsAreOffAndUnaskedUntilTheUserAnswers() {
        #expect(!AppSettings.default.sendStats)
        #expect(!AppSettings.default.privacyAsked)
    }

    /// Somebody on 0.2.0 agreed to a switch that sent three things. 0.2.1 sends six, so the old
    /// yes does not carry over: the file reads as unasked and the question is put again.
    @Test
    func aFileFrom020CountsAsNeverAsked() {
        let old = """
            {
              "schemaVersion": 2,
              "place": "Tray",
              "runAtStartup": true,
              "checkUpdates": true,
              "meetAikoShown": true
            }
            """

        let settings = AppSettings.fromJson(old)

        #expect(settings.checkUpdates)
        #expect(!settings.sendStats)
        #expect(!settings.privacyAsked)
    }

    /// Found on a live install: a settings.json made by 0.1 still said schema 1 after 0.2.1 had
    /// written new fields into it. The number is meant to tell a future migration what shape the
    /// file is in, and a number that never moves cannot do that.
    @Test
    func savingStampsTheFileWithTheSchemaThatWroteIt() {
        let old = AppSettings.fromJson(#"{ "schemaVersion": 1, "place": "Tray", "runAtStartup": true }"#)

        #expect(old.schemaVersion == 1)
        #expect(old.toJson().contains("\"schemaVersion\": \(AppSettings.currentSchema)"))
        #expect(AppSettings.fromJson(old.toJson()).schemaVersion == AppSettings.currentSchema)
    }

    @Test
    func settingsSurviveATripThroughTheFile() {
        var settings = AppSettings()
        settings.place = .island
        settings.runAtStartup = false
        settings.checkUpdates = true
        settings.sendStats = true
        settings.privacyAsked = true
        settings.language = .russian
        settings.hideIslandInFullScreen = false
        settings.island = IslandPosition(.right, 0.25)

        #expect(AppSettings.fromJson(settings.toJson()) == settings)
    }

    @Test
    func choicesAreWrittenAsWords() {
        var settings = AppSettings()
        settings.place = .island
        settings.language = .english

        let json = settings.toJson()

        #expect(json.contains("\"Island\""))
        #expect(json.contains("\"English\""))
    }

    @Test(arguments: ["", "   ", "not json at all", #"{ "place": "#])
    func aBrokenFileReadsAsTheDefaults(json: String) {
        #expect(AppSettings.fromJson(json) == .default)
    }

    @Test
    func fieldsWeDoNotKnowAreIgnored() {
        let settings = AppSettings.fromJson(#"{ "place": "Island", "somethingFromALaterVersion": 42 }"#)

        #expect(settings.place == .island)
        #expect(settings.runAtStartup)
    }

    @Test
    func aChoiceWeDoNotKnowReadsAsTheDefaults() {
        // A newer Aiko may write a place this one has never heard of. Better the whole file falls
        // back than the app starts with half of it applied.
        #expect(AppSettings.fromJson(#"{ "place": "Wallpaper" }"#) == .default)
    }

    @Test
    func meetAikoHasNotBeenShownInAFileFrom01() {
        let settings = AppSettings.fromJson(#"{ "schemaVersion": 1, "place": "Island" }"#)

        #expect(!settings.meetAikoShown)
        #expect(settings.place == .island)
    }

    @Test
    func meetAikoShownSurvivesATripThroughTheFile() {
        var shown = AppSettings.default
        shown.meetAikoShown = true

        let back = AppSettings.fromJson(shown.toJson())

        #expect(back.meetAikoShown)
        #expect(back.schemaVersion == AppSettings.currentSchema)
    }
}
