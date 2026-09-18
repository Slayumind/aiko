import Testing

@testable import AikoKit

struct SessionReminderTests {
    static let home = #"C:\Users\someone"#

    static let settings = EnvironmentSettings([
        AikoEnvironment("Work", [home + #"\.claude"#]),
        AikoEnvironment(
            "Personal", [home + #"\.claude-personal"#], projectFolders: [#"C:\projects\aiko"#]),
    ])

    // ---- the message ----

    @Test
    func aCommandInAFolderBoundElsewhereSaysWhoseLimitsGo() {
        let message = SessionReminder.messageFor(
            .windows, launch: "command", runningEnvironment: "Work",
            workingDirectory: #"C:\projects\aiko\src"#, settings: Self.settings, russian: true)

        #expect(message == "Aiko: эта папка относится к Personal, а сессия запущена в Work. Лимиты тратятся из Work.")
    }

    @Test
    func englishSaysTheSame() {
        let message = SessionReminder.messageFor(
            .windows, launch: "command", runningEnvironment: "Work",
            workingDirectory: #"C:\projects\aiko"#, settings: Self.settings, russian: false)

        #expect(
            message
                == "Aiko: this folder belongs to Personal, but this session runs in Work, so the limits of Work are used.")
    }

    @Test(arguments: [
        ("binding", "Personal", #"C:\projects\aiko"#),  // started by the binding itself
        ("command", "Personal", #"C:\projects\aiko"#),  // the command matches the binding
        ("command", "Work", #"C:\temp"#),  // no binding here, so nothing to remind about
        (nil, nil, #"C:\projects\aiko"#),  // not started through the shim at all
    ] as [(String?, String?, String)])
    func quietWhenThereIsNothingToSay(launch: String?, running: String?, folder: String) {
        #expect(
            SessionReminder.messageFor(
                .windows, launch: launch, runningEnvironment: running,
                workingDirectory: folder, settings: Self.settings, russian: false) == nil)
    }

    @Test
    func theHookAnswerIsASystemMessage() {
        let output = JsonNode.parse(SessionReminder.hookOutput("Aiko: «test»"))?.objectValue

        #expect(output?["systemMessage"]?.stringValue == "Aiko: «test»")
    }

    @Test(arguments: [
        (AikoLanguage.russian, 0x0409, true),
        (AikoLanguage.english, 0x0419, false),
        (AikoLanguage.system, 0x0419, true),
        (AikoLanguage.system, 0x0409, false),
    ])
    func language(setting: AikoLanguage, windows: Int, russian: Bool) {
        #expect(SessionReminder.isRussian(setting, windows) == russian)
    }

    @Test
    func readsTheWorkingFolderFromHookInput() {
        #expect(SessionReminder.workingDirectoryIn(#"{ "session_id": "s", "cwd": "C:\\x" }"#) == #"C:\x"#)
        #expect(SessionReminder.workingDirectoryIn("not json") == nil)
    }
}
