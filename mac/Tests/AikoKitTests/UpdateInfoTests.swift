import Testing

@testable import AikoKit

struct UpdateInfoTests {
    @Test
    func aNewerVersionOnTheSiteMeansAnUpdateIsAvailable() {
        let info = UpdateInfo.fromJson("""
            { "latest": "0.1.1", "downloadUrl": "https://github.com/Slayumind/aiko/releases/latest" }
            """)

        #expect(info.compareWith("0.1.0") == .available)
        #expect(info.downloadUrl == "https://github.com/Slayumind/aiko/releases/latest")
    }

    @Test
    func theSameVersionMeansNothingToDo() {
        let info = UpdateInfo.fromJson(#"{ "latest": "0.1.0" }"#)

        #expect(info.compareWith("0.1.0") == .upToDate)
    }

    @Test
    func aNewerCopyThanTheSiteKnowsAboutIsNotOutOfDate() {
        let info = UpdateInfo.fromJson(#"{ "latest": "0.1.0" }"#)

        #expect(info.compareWith("0.2.0") == .upToDate)
    }

    @Test
    func versionsAreComparedAsNumbersNotAsText() {
        let info = UpdateInfo.fromJson(#"{ "latest": "0.10.0" }"#)

        // As text "0.10.0" sorts before "0.9.0", which would hide the update.
        #expect(info.compareWith("0.9.0") == .available)
    }

    @Test
    func withoutALatestVersionAikoSaysItDoesNotKnow() {
        // The lesson from Sparks: the site leaves the field out when it cannot tell, and a client
        // that invents a fallback would declare every copy up to date.
        let info = UpdateInfo.fromJson(#"{ "downloadUrl": "https://example.org" }"#)

        #expect(info.latest == nil)
        #expect(info.compareWith("0.1.0") == .unknown)
    }

    @Test(arguments: [
        "", "   ", "<html>not json</html>", "[1, 2, 3]", #"{ "latest": 42 }"#, #"{ "latest": "tomorrow" }"#,
    ])
    func anAnswerWeCannotReadMeansWeDoNotKnow(json: String) {
        #expect(UpdateInfo.fromJson(json).compareWith("0.1.0") == .unknown)
    }

    @Test
    func fieldsFromALaterSiteVersionAreIgnored() {
        let info = UpdateInfo.fromJson("""
            { "latest": "0.2.0", "minSupported": "0.1.0", "installers": { "win-x64": "https://example.org/a.exe" } }
            """)

        #expect(info.compareWith("0.1.0") == .available)
    }
}
