import Foundation
import Testing

@testable import AikoKit

/// What the bridge does with one run. The executable only reads stdin, the files and the
/// environment; every choice made here is one it cannot make on its own.
struct BridgeWorkTests {
    static let folders = AikoFolders.macOS(
        "/Users/someone/Library/Application Support", "/Users/someone/Library/Caches")

    static let configDirectory = "/Users/someone/.claude-work"

    static let payload = """
        {
          "session_id": "00000000-0000-0000-0000-000000000000",
          "cwd": "/Users/someone/work",
          "rate_limits": {
            "five_hour": { "used_percentage": 28, "resets_at": 1789170600 },
            "seven_day": { "used_percentage": 15, "resets_at": 1789506000 }
          }
        }
        """

    // ---- which job this run is ----

    @Test
    func thePluginRequestIsRecognisedBeforeAnythingIsRead() {
        #expect(BridgeMode.forArguments(["plugin", "aiko-persona"]) == .personaPlugin)
    }

    @Test(arguments: [
        ["plugin"],
        ["plugin", "something-else"],
        ["plugin", "aiko-persona", "extra"],
        [],
        ["--what-is-this"],
    ])
    func anythingElseIsTheStatusLine(arguments: [String]) {
        #expect(BridgeMode.forArguments(arguments) == .statusLine)
    }

    @Test
    func theHooksAreToldApartByTheirFirstArgument() {
        #expect(BridgeMode.forArguments(["--session-start"]) == .sessionStart)
        #expect(BridgeMode.forArguments(["--session-start", "ignored"]) == .sessionStart)
        #expect(BridgeMode.forArguments(["hook"]) == .hook)
        #expect(BridgeMode.forArguments(["hook", "ignored"]) == .hook)
    }

    // ---- the status line ----

    @Test
    func aStatusLineWithLimitsWritesTheSnapshotOfItsEnvironment() {
        let plan = BridgeWork.statusLine(
            folders: Self.folders,
            configDirectory: Self.configDirectory,
            settingsJson: nil,
            input: Self.payload,
            now: utc(2026, 9, 12, 12))

        #expect(plan.snapshot?.path == "/Users/someone/Library/Caches/Aiko/environments/claude-work.json")

        let written = SnapshotFile.fromJson(plan.snapshot?.text)
        #expect(written.environment == "claude-work")
        #expect(written.receivedAt == utc(2026, 9, 12, 12))
        #expect(written.windows.count == 2)
        #expect(written.windows.first { $0.kind == .fiveHour }?.percent == 28)
    }

    @Test(arguments: ["", "not json at all", "{}", #"{ "rate_limits": {} }"#])
    func inputWithNoLimitsWritesNothing(input: String) {
        let plan = BridgeWork.statusLine(
            folders: Self.folders,
            configDirectory: Self.configDirectory,
            settingsJson: nil,
            input: input,
            now: utc(2026, 9, 12, 12))

        #expect(plan.snapshot == nil)
        #expect(plan.wrappedCommand == nil)
    }

    @Test
    func theLineTheUserAlreadyHadIsHandedBackToBeRun() {
        let settings = #"""
            {
              "statusLine": { "type": "command", "command": "/Applications/Aiko.app/Contents/MacOS/Aiko.Bridge" },
              "aikoWrappedStatusLine": { "type": "command", "command": "~/bin/my-line.sh" }
            }
            """#

        let plan = BridgeWork.statusLine(
            folders: Self.folders,
            configDirectory: Self.configDirectory,
            settingsJson: settings,
            input: Self.payload,
            now: utc(2026, 9, 12, 12))

        #expect(plan.wrappedCommand == "~/bin/my-line.sh")
    }

    @Test(arguments: ["", "{}", #"{ "aikoWrappedStatusLine": { "type": "command", "command": "  " } }"#])
    func nothingKeptAsideMeansNothingToRun(settings: String) {
        let plan = BridgeWork.statusLine(
            folders: Self.folders,
            configDirectory: Self.configDirectory,
            settingsJson: settings,
            input: Self.payload,
            now: utc(2026, 9, 12, 12))

        #expect(plan.wrappedCommand == nil)
    }

    // ---- the session events behind the face ----

    static func activity(_ input: String, existing: String? = nil, now: Date = utc(2026, 9, 12, 12)) -> ActivityStep {
        BridgeWork.activity(
            folders: folders,
            configDirectory: configDirectory,
            input: input,
            readFile: { _ in existing },
            now: now,
            offsetSeconds: 0)
    }

    static func hook(_ event: String, session: String = "session-1") -> String {
        #"{ "hook_event_name": "\#(event)", "session_id": "\#(session)" }"#
    }

    @Test
    func aPromptWritesTheActivityOfItsSession() {
        guard case .write(let file) = Self.activity(Self.hook("UserPromptSubmit")) else {
            Issue.record("expected a file to be written")
            return
        }

        let expected = "/Users/someone/Library/Caches/Aiko/activity/"
            + ActivityRecord.fileName("claude-work", "session-1")
        #expect(file.path == expected)

        let record = ActivityRecord.fromJson(file.text)
        #expect(record?.environment == "claude-work")
        #expect(record?.activity == .working)
        #expect(record?.at == utc(2026, 9, 12, 12))
    }

    @Test
    func theEndOfASessionTakesItsFileAway() {
        let expected = "/Users/someone/Library/Caches/Aiko/activity/"
            + ActivityRecord.fileName("claude-work", "session-1")

        #expect(Self.activity(Self.hook("SessionEnd")) == .remove(path: expected))
    }

    @Test
    func theSameActivityAgainDoesNotTouchTheDisk() {
        let written = ActivityRecord(
            environment: "claude-work", activity: .working, at: utc(2026, 9, 12, 12)).toJson()

        let step = Self.activity(
            Self.hook("PostToolUse"), existing: written, now: utc(2026, 9, 12, 12, 0, 5))

        #expect(step == .nothing)
    }

    @Test(arguments: ["PreToolUse", "SessionStart", ""])
    func anEventTheFaceDoesNotCareAboutWritesNothing(event: String) {
        #expect(Self.activity(Self.hook(event)) == .nothing)
    }

    @Test
    func inputThatIsNotAHookCallWritesNothing() {
        #expect(Self.activity("") == .nothing)
        #expect(Self.activity("not json") == .nothing)
        #expect(Self.activity(Self.payload) == .nothing)
    }

    // ---- the session start hook ----

    static let environments = EnvironmentSettings([
        AikoEnvironment("Work", ["/Users/someone/.claude"]),
        AikoEnvironment(
            "Personal", ["/Users/someone/.claude-personal"], projectFolders: ["/Users/someone/personal"]),
    ])

    static func sessionStart(
        launch: String?, running: String?, cwd: String, language: AikoLanguage = .english,
        tag: String? = nil
    ) -> String? {
        var settings = AppSettings()
        settings.language = language

        return BridgeWork.sessionStart(
            folders: folders,
            input: #"{ "cwd": "\#(cwd)" }"#,
            launch: launch,
            runningEnvironment: running,
            environmentsJson: environments.toJson(),
            settingsJson: settings.toJson(),
            systemLanguageTag: tag)
    }

    @Test
    func aCommandInSomebodyElsesFolderSaysWhoseLimitsAreSpent() {
        let answer = Self.sessionStart(
            launch: "command", running: "Work", cwd: "/Users/someone/personal/app")

        #expect(answer != nil)
        #expect(answer?.contains("systemMessage") == true)
        #expect(answer?.contains("this folder belongs to Personal") == true)
    }

    @Test
    func theSameSessionInRussian() {
        let answer = Self.sessionStart(
            launch: "command", running: "Work", cwd: "/Users/someone/personal/app",
            language: .system, tag: "ru-RU")

        // Russian letters are escaped in the hook answer, as they are in every file Aiko writes
        // for Claude Code, so the message is read back out of the JSON.
        let message = JsonNode.parse(answer ?? "")?.objectValue?["systemMessage"]?.stringValue
        #expect(message?.contains("эта папка относится к Personal") == true)
        #expect(message?.contains("Лимиты тратятся из Work") == true)
    }

    @Test
    func aSessionThatMatchesItsBindingSaysNothing() {
        #expect(Self.sessionStart(launch: "command", running: "Personal", cwd: "/Users/someone/personal/app") == nil)
        #expect(Self.sessionStart(launch: "binding", running: "Work", cwd: "/Users/someone/personal/app") == nil)
        #expect(Self.sessionStart(launch: "command", running: nil, cwd: "/Users/someone/personal/app") == nil)
    }

    @Test
    func inputWithoutAFolderSaysNothing() {
        let answer = BridgeWork.sessionStart(
            folders: Self.folders, input: "{}", launch: "command", runningEnvironment: "Work",
            environmentsJson: Self.environments.toJson(), settingsJson: nil, systemLanguageTag: "ru")

        #expect(answer == nil)
    }

    // ---- the persona plugin ----

    @Test
    func thePluginGoesIntoAFolderNamedAfterItsContent() {
        let bridge = "/Applications/Aiko.app/Contents/MacOS/Aiko.Bridge"
        let build = BridgeWork.personaPlugin(
            folders: Self.folders,
            configDirectory: Self.configDirectory,
            personaJson: "",
            bridgePath: bridge)

        let hash = PersonaPlugin.contentHash(PersonaPlugin.files(.normal, bridge))
        #expect(build.folder == "/Users/someone/Library/Caches/Aiko/plugins/claude-work/\(hash)")
        #expect(build.files.map(\.path) == PersonaPlugin.files(.normal, bridge).map(\.path))
    }

    @Test
    func anotherTemperamentGetsAnotherFolder() {
        let bridge = "/Applications/Aiko.app/Contents/MacOS/Aiko.Bridge"
        var persona = PersonaSettings.default
        persona.temperament = .bright

        let bright = BridgeWork.personaPlugin(
            folders: Self.folders, configDirectory: Self.configDirectory,
            personaJson: persona.toJson(), bridgePath: bridge)
        let normal = BridgeWork.personaPlugin(
            folders: Self.folders, configDirectory: Self.configDirectory,
            personaJson: "", bridgePath: bridge)

        #expect(bright.folder != normal.folder)
    }
}

/// The language the reminder speaks, as macOS says it: a tag such as "ru-RU" instead of the
/// number Windows hands the bridge.
struct MacLanguageTests {
    @Test(arguments: [
        ("ru", true), ("ru-RU", true), ("RU-ru", true), ("ru_RU", true),
        ("en-US", false), ("", false), ("russian", false),
    ])
    func theSystemLanguageIsReadFromItsTag(tag: String, russian: Bool) {
        #expect(SessionReminder.isRussian(.system, systemLanguageTag: tag) == russian)
    }

    @Test
    func aChosenLanguageBeatsTheSystemOne() {
        #expect(SessionReminder.isRussian(.russian, systemLanguageTag: nil))
        #expect(SessionReminder.isRussian(.russian, systemLanguageTag: "en-US"))
        #expect(!SessionReminder.isRussian(.english, systemLanguageTag: "ru-RU"))
        #expect(!SessionReminder.isRussian(.system, systemLanguageTag: nil))
    }
}
