import Testing

@testable import AikoKit

struct BridgeCommandTests {
    @Test
    func thePathIsQuotedBecauseItHoldsSpaces() {
        let command = BridgeCommand.forPath(#"C:\Users\someone\AppData\Local\Aiko\current\Aiko.Bridge.exe"#)

        #expect(command == #""C:\Users\someone\AppData\Local\Aiko\current\Aiko.Bridge.exe""#)
    }

    @Test
    func aPathThatIsAlreadyQuotedIsLeftAlone() {
        let quoted = #""C:\Program Files\Aiko\Aiko.Bridge.exe""#

        #expect(BridgeCommand.forPath(quoted) == quoted)
    }

    @Test(arguments: ["", "   ", nil] as [String?])
    func withoutAPathThereIsNoCommand(path: String?) {
        #expect(BridgeCommand.forPath(path).isEmpty)
    }

    @Test
    func anEmptyCommandIsRefusedByTheSettingsPatch() {
        // The two belong together: no path means no line in the user's settings file.
        #expect(SettingsJsonPatch(.windows).addBridge("{}", BridgeCommand.forPath("")) == nil)
    }

    @Test
    func powerShellNeedsTheCallOperatorOrNothingRuns() {
        // Without "&" PowerShell reads a quoted path as a plain string and the status line stays
        // empty, with no error to explain it.
        let command = BridgeCommand.forPath(#"C:\Program Files\Aiko\Aiko.Bridge.exe"#, .powerShell)

        #expect(command == #"& "C:\Program Files\Aiko\Aiko.Bridge.exe""#)
    }

    @Test
    func gitBashGetsNoCallOperatorBecauseItWouldBreakTheLine() {
        #expect(BridgeCommand.forPath(#"C:\Aiko\Aiko.Bridge.exe"#, .gitBash).hasPrefix("\""))
    }

    @Test
    func gitBashIsTheDefaultBecauseClaudeCodePrefersIt() {
        #expect(
            BridgeCommand.forPath(#"C:\Aiko\Aiko.Bridge.exe"#)
                == BridgeCommand.forPath(#"C:\Aiko\Aiko.Bridge.exe"#, .gitBash))
    }

    @Test
    func withoutAPathNeitherShellGetsACommand() {
        #expect(BridgeCommand.forPath("", .powerShell).isEmpty)
    }

    @Test
    func theLineWeWriteIsRecognisedAsOurs() {
        let patch = SettingsJsonPatch(.windows)
        let command = BridgeCommand.forPath(#"C:\Aiko\Aiko.Bridge.exe"#)
        let patched = patch.addBridge("{}", command)!

        // Adding it a second time changes nothing: it is already our line.
        #expect(patch.addBridge(patched, command) == nil)
    }
}

/// Recognising our own status line again after the install path or the shell has changed.
///
/// Matching the line by exact text looked right and was not. A reinstall into another folder, or
/// Git appearing on a machine that had none, changes the text. The old line was then taken for the
/// user's own, put aside under our wrapped key, and the bridge was asked to run a path that no
/// longer exists after every model answer.
struct BridgeRecognitionTests {
    static let here = #"C:\Users\someone\AppData\Local\Aiko\current\Aiko.Bridge.exe"#
    static let elsewhere = #"D:\Programs\Aiko\Aiko.Bridge.exe"#

    private let patch = SettingsJsonPatch(.windows)

    @Test
    func ourLineIsOursWhateverFolderItPointsAt() {
        #expect(BridgeCommand.isAiko(.windows, BridgeCommand.forPath(Self.here)))
        #expect(BridgeCommand.isAiko(.windows, BridgeCommand.forPath(Self.elsewhere)))
    }

    @Test
    func ourLineIsOursInEitherShell() {
        #expect(BridgeCommand.isAiko(.windows, BridgeCommand.forPath(Self.here, .gitBash)))
        #expect(BridgeCommand.isAiko(.windows, BridgeCommand.forPath(Self.here, .powerShell)))
    }

    @Test(arguments: [
        nil,
        "",
        "   ",
        "& ",
        "\"",
        "starship prompt",
        #""C:\Users\someone\bin\my-status-line.sh""#,
        #"& "C:\tools\aiko-bridge-helper.exe""#,
    ] as [String?])
    func somebodyElsesLineIsNotOurs(command: String?) {
        #expect(!BridgeCommand.isAiko(.windows, command))
    }

    @Test
    func anUnquotedLineIsReadToo() {
        #expect(BridgeCommand.isAiko(.windows, #"C:\Aiko\Aiko.Bridge.exe"#))
        #expect(BridgeCommand.isAiko(.windows, #"C:\Aiko\aiko.bridge.EXE"#))
    }

    @Test
    func aReinstallElsewhereReplacesOurLineInsteadOfHidingIt() {
        let after = patch.addBridge("{}", BridgeCommand.forPath(Self.here))!

        let patched = patch.addBridge(after, BridgeCommand.forPath(Self.elsewhere))

        #expect(patched != nil)
        #expect(patched!.contains(Self.elsewhere.replacingOccurrences(of: #"\"#, with: #"\\"#)))
        // The old path is gone, and nothing of ours was filed away as if the user had written it.
        #expect(!patched!.contains(SettingsJsonPatch.wrappedKey))
    }

    @Test
    func aShellChangeReplacesOurLineInsteadOfHidingIt() {
        let after = patch.addBridge("{}", BridgeCommand.forPath(Self.here, .powerShell))!

        let patched = patch.addBridge(after, BridgeCommand.forPath(Self.here, .gitBash))

        #expect(patched != nil)
        #expect(!patched!.contains(SettingsJsonPatch.wrappedKey))
    }

    @Test
    func theUsersOwnLineIsStillKeptAside() {
        let mine = #"{ "statusLine": { "type": "command", "command": "my-line.sh" } }"#

        let patched = patch.addBridge(mine, BridgeCommand.forPath(Self.here))!

        #expect(patched.contains(SettingsJsonPatch.wrappedKey))
        #expect(SettingsJsonPatch.readWrappedCommand(patched) == "my-line.sh")
    }

    @Test
    func aLineOfOursLeftAsideByAnOlderAikoIsThrownAway() {
        // What the old exact match produced: our own command filed under the wrapped key. Restoring
        // it on removal would hand the user a status line running a bridge that is gone.
        let damaged = Self.damagedFile()

        let patched = patch.addBridge(damaged, BridgeCommand.forPath(Self.here))

        #expect(patched != nil)
        #expect(!patched!.contains(SettingsJsonPatch.wrappedKey))
        #expect(
            BridgeCommand.isAiko(
                .windows, JsonNode.parse(patched!)?["statusLine"]?["command"]?.stringValue))
    }

    @Test
    func removingAikoDoesNotRestoreALineOfOurs() {
        let restored = patch.removeBridge(Self.damagedFile())

        #expect(restored != nil)
        let root = JsonNode.parse(restored!)?.objectValue
        #expect(root?.contains("statusLine") == false)
        #expect(root?.contains(SettingsJsonPatch.wrappedKey) == false)
    }

    @Test
    func removingAikoStillPutsBackTheLineTheUserHad() {
        let mine = #"{ "statusLine": { "type": "command", "command": "my-line.sh" } }"#
        let patched = patch.addBridge(mine, BridgeCommand.forPath(Self.here))!

        let restored = patch.removeBridge(patched)!

        let root = JsonNode.parse(restored)?.objectValue
        #expect(root?["statusLine"]?["command"]?.stringValue == "my-line.sh")
        #expect(root?.contains(SettingsJsonPatch.wrappedKey) == false)
    }

    @Test
    func aCommandKeyHoldingSomethingOtherThanTextIsNotOurs() {
        let odd = #"{ "statusLine": { "type": "command", "command": 42 } }"#

        let patched = patch.addBridge(odd, BridgeCommand.forPath(Self.here))

        #expect(patched != nil)
        // Not ours, so it is kept aside like anyone else's line rather than thrown away.
        #expect(patched!.contains(SettingsJsonPatch.wrappedKey))
    }

    private static func damagedFile() -> String {
        let ours = JsonNode.string(BridgeCommand.forPath(here)).toJsonString()
        let old = JsonNode.string(BridgeCommand.forPath(elsewhere)).toJsonString()
        return """
            {
              "statusLine": { "type": "command", "command": \(ours) },
              "aikoWrappedStatusLine": { "type": "command", "command": \(old) }
            }
            """
    }
}
