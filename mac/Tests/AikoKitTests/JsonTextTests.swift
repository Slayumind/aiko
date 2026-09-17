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
}
