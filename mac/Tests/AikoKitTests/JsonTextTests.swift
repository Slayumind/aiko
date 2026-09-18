import Testing

@testable import AikoKit

/// Telling an empty settings file from a broken one.
///
/// Every parser reads a broken file as "no data", and the next save then writes defaults over it.
/// That is how a settings file turns into lost settings without anybody noticing, so the store has
/// to be able to tell the two apart before it writes.
struct JsonTextTests {
    @Test(arguments: ["{}", #"{ "place": "Island" }"#, "  { }  "])
    func aSettingsFileIsAnObject(json: String) {
        #expect(JsonText.isObject(json))
    }

    @Test(arguments: [nil, "", "   "] as [String?])
    func nothingIsNotAnObjectAndIsNotBrokenEither(json: String?) {
        // Empty means a first run. The store checks for length before it asks.
        #expect(!JsonText.isObject(json))
    }

    @Test(arguments: [#"{ "place": "#, "not json at all", "[1, 2, 3]", "\"a string\"", "\0\0\0\0"])
    func rubbishIsNotAnObject(json: String) {
        #expect(!JsonText.isObject(json))
    }

    @Test
    func halfAFileFromAPowerCutIsNotAnObject() {
        var whole = AppSettings.default
        whole.place = .island
        let half = String(whole.toJson().prefix(20))

        #expect(!JsonText.isObject(half))
    }

    @Test
    func whatWeWriteIsAlwaysReadableAgain() {
        #expect(JsonText.isObject(AppSettings.default.toJson()))
        #expect(JsonText.isObject(EnvironmentSettings.empty.toJson()))
    }

    @Test
    func bothFilesCarryTheShapeTheyWereWrittenIn() {
        #expect(AppSettings.default.toJson().contains("schemaVersion"))
        #expect(EnvironmentSettings.empty.toJson().contains("schemaVersion"))
    }

    @Test
    func aFileWrittenBeforeTheVersionExistedStillReads() {
        let old = #"{ "place": "Island", "runAtStartup": false }"#

        let settings = AppSettings.fromJson(old)

        #expect(settings.place == .island)
        #expect(!settings.runAtStartup)
    }
}
