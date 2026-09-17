import Testing

@testable import AikoKit

struct SessionReminderTests {
    // ---- the message ----
    //
    // ProjectBinding is not ported yet, so the environment a folder belongs to is passed in.
    // The bound environment of C:\projects\aiko is Personal, and C:\temp is bound to nothing.

    @Test
    func aCommandInAFolderBoundElsewhereSaysWhoseLimitsGo() {
        let message = SessionReminder.messageFor(
            launch: "command", runningEnvironment: "Work", boundEnvironment: "Personal", russian: true)

        #expect(message == "Aiko: эта папка относится к Personal, а сессия запущена в Work. Лимиты тратятся из Work.")
    }

    @Test
    func englishSaysTheSame() {
        let message = SessionReminder.messageFor(
            launch: "command", runningEnvironment: "Work", boundEnvironment: "Personal", russian: false)

        #expect(
            message
                == "Aiko: this folder belongs to Personal, but this session runs in Work, so the limits of Work are used.")
    }

    @Test(arguments: [
        ("binding", "Personal", "Personal"),  // started by the binding itself
        ("command", "Personal", "Personal"),  // the command matches the binding
        ("command", "Work", nil),  // no binding here, so nothing to remind about
        (nil, nil, "Personal"),  // not started through the shim at all
    ] as [(String?, String?, String?)])
    func quietWhenThereIsNothingToSay(launch: String?, running: String?, bound: String?) {
        #expect(
            SessionReminder.messageFor(
                launch: launch, runningEnvironment: running, boundEnvironment: bound, russian: false) == nil)
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
