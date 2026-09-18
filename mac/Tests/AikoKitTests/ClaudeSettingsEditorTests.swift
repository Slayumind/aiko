import Foundation
import Testing

@testable import AikoKit

/// Writing to a file that belongs to another program.
///
/// Removal walks every Claude Code folder on the machine and rewrites what it finds, and until now
/// none of it had a test. These cover the paths that end badly: no backup, a backup already there,
/// a file the user changed after installing, and a folder Aiko cannot read.
struct ClaudeSettingsEditorTests {
    static let folder = #"C:\Users\someone\.claude"#
    static let bridge = #"C:\Users\someone\AppData\Local\Aiko\current\Aiko.Bridge.exe"#
    static let moved = #"D:\Aiko\Aiko.Bridge.exe"#

    static var settings: String { ClaudeSettingsEditor.pathIn(folder) }
    static var backup: String { ClaudeSettingsEditor.backupPathIn(folder) }

    private func command(_ path: String = ClaudeSettingsEditorTests.bridge) -> String {
        BridgeCommand.forPath(path)
    }

    private func editor(_ files: FakeFiles) -> ClaudeSettingsEditor {
        ClaudeSettingsEditor(files: files, patch: SettingsJsonPatch(.windows))
    }

    @Test
    func aCopyIsMadeBeforeTheFirstChange() {
        let original = #"{ "statusLine": { "type": "command", "command": "mine.sh" } }"#
        let files = FakeFiles().with(Self.settings, original)

        _ = editor(files).add(Self.folder, command())

        #expect(files.has(Self.backup))
        #expect(files.read(Self.backup) == original)
    }

    @Test
    func aCopyThatAlreadyExistsIsLeftAlone() {
        // A second copy would save our own edit and lose what the user had.
        let files = FakeFiles()
            .with(Self.settings, #"{ "statusLine": { "type": "command", "command": "new.sh" } }"#)
            .with(Self.backup, #"{ "statusLine": { "type": "command", "command": "original.sh" } }"#)

        _ = editor(files).add(Self.folder, command())

        #expect(files.read(Self.backup).contains("original.sh"))
    }

    @Test
    func aFolderWithNoSettingsFileGetsOne() {
        let files = FakeFiles()

        let outcome = editor(files).add(Self.folder, command())

        #expect(outcome.changed)
        #expect(files.has(Self.settings))
        // Nothing existed, so there was nothing worth copying.
        #expect(!files.has(Self.backup))
    }

    @Test
    func theFileIsWrittenThroughATemporaryOne() {
        // A half written settings file breaks Claude Code, not just Aiko.
        let files = FakeFiles()

        _ = editor(files).add(Self.folder, command())

        #expect(files.writes.contains { $0.hasSuffix(".aiko.tmp") })
        #expect(!files.writes.contains { $0 == Self.settings })
    }

    @Test
    func addingOurLineTwiceWritesNothingTheSecondTime() {
        let files = FakeFiles()
        let editor = editor(files)
        _ = editor.add(Self.folder, command())
        files.writes.removeAll()

        let outcome = editor.add(Self.folder, command())

        #expect(!outcome.changed)
        #expect(files.writes.isEmpty)
    }

    @Test
    func removalPutsBackTheLineTheUserHad() {
        let files = FakeFiles().with(Self.settings, #"{ "statusLine": { "type": "command", "command": "mine.sh" } }"#)
        let editor = editor(files)
        _ = editor.add(Self.folder, command())

        let outcome = editor.remove(Self.folder)

        #expect(outcome.changed)
        #expect(files.read(Self.settings).contains("mine.sh"))
        #expect(!files.read(Self.settings).contains(SettingsJsonPatch.wrappedKey))
    }

    @Test
    func removalDropsTheCopyItMade() {
        let files = FakeFiles().with(Self.settings, #"{ "statusLine": { "type": "command", "command": "mine.sh" } }"#)
        let editor = editor(files)
        _ = editor.add(Self.folder, command())

        _ = editor.remove(Self.folder)

        // The copy was insurance while Aiko was in the file, and the readme promises there is
        // nothing left to clean up by hand.
        #expect(!files.has(Self.backup))
    }

    @Test
    func removalKeepsWhatTheUserChangedAfterInstalling() throws {
        let files = FakeFiles()
        let editor = editor(files)
        _ = editor.add(Self.folder, command())

        // They edited the file themselves while Aiko was in it.
        var edited = try #require(JsonNode.parse(files.read(Self.settings))?.objectValue)
        edited["model"] = .string("opus")
        files.with(Self.settings, JsonNode.object(edited).toJsonString())

        _ = editor.remove(Self.folder)

        // Restoring the copy wholesale would have thrown this away.
        #expect(files.read(Self.settings).contains("opus"))
    }

    @Test
    func removalDeletesASettingsFileAikoMadeFromNothing() {
        // Windows Sandbox: no settings.json before Aiko, and "{}" left behind after it.
        let files = FakeFiles()
        let editor = editor(files)
        _ = editor.add(Self.folder, command())

        _ = editor.remove(Self.folder)

        #expect(!files.has(Self.settings))
    }

    @Test
    func removalKeepsAFileAikoMadeOnceSomethingElseWasWrittenToIt() throws {
        let files = FakeFiles()
        let editor = editor(files)
        _ = editor.add(Self.folder, command())

        var edited = try #require(JsonNode.parse(files.read(Self.settings))?.objectValue)
        edited["model"] = .string("opus")
        files.with(Self.settings, JsonNode.object(edited).toJsonString())

        _ = editor.remove(Self.folder)

        #expect(files.has(Self.settings))
        #expect(files.read(Self.settings).contains("opus"))
    }

    @Test
    func removalKeepsAnEmptyFileThatWasThereBeforeAiko() {
        // It was the user's file, even if it said nothing. A copy was made of it, and that copy is
        // what tells the two cases apart.
        let files = FakeFiles().with(Self.settings, "{}")
        let editor = editor(files)
        _ = editor.add(Self.folder, command())

        _ = editor.remove(Self.folder)

        #expect(files.has(Self.settings))
    }

    @Test
    func removalFromAFolderAikoNeverTouchedChangesNothing() {
        let theirs = #"{ "model": "sonnet" }"#
        let files = FakeFiles().with(Self.settings, theirs)

        let outcome = editor(files).remove(Self.folder)

        #expect(!outcome.changed)
        #expect(files.read(Self.settings) == theirs)
        #expect(files.writes.isEmpty)
    }

    @Test
    func removalFromAnEmptyFolderChangesNothing() {
        let files = FakeFiles()

        let outcome = editor(files).remove(Self.folder)

        #expect(!outcome.changed)
        #expect(!files.has(Self.settings))
    }

    @Test
    func aFileAikoMayNotTouchIsReportedAndNotLost() {
        let files = FakeFiles().with(Self.settings, #"{ "model": "opus" }"#)
        files.unreadable.insert(Self.settings)

        let outcome = editor(files).add(Self.folder, command())

        #expect(!outcome.changed)
        #expect(outcome.problem != .none)
        #expect(files.writes.isEmpty)
    }

    @Test
    func withoutABridgePathNothingIsWritten() {
        let files = FakeFiles()

        let outcome = editor(files).add(Self.folder, "")

        #expect(!outcome.changed)
        #expect(outcome.problem != .none)
        #expect(files.writes.isEmpty)
    }

    @Test
    func repairFixesOurLineAfterAReinstallElsewhere() {
        let files = FakeFiles()
        let editor = editor(files)
        _ = editor.add(Self.folder, command())

        let outcome = editor.repairIfOurs(Self.folder, command(Self.moved))

        #expect(outcome.changed)
        #expect(files.read(Self.settings).contains(Self.moved.replacingOccurrences(of: #"\"#, with: #"\\"#)))
    }

    @Test
    func repairNeverInstallsTheLineForSomebodyWhoSaidNo() {
        // The wizard's "not now" is an answer, and startup is not the place to overrule it.
        let files = FakeFiles().with(Self.settings, #"{ "model": "opus" }"#)

        let outcome = editor(files).repairIfOurs(Self.folder, command())

        #expect(!outcome.changed)
        #expect(files.writes.isEmpty)
        #expect(!files.read(Self.settings).contains("statusLine"))
    }

    @Test
    func repairLeavesSomebodyElsesLineAlone() {
        let theirs = #"{ "statusLine": { "type": "command", "command": "mine.sh" } }"#
        let files = FakeFiles().with(Self.settings, theirs)

        let outcome = editor(files).repairIfOurs(Self.folder, command())

        #expect(!outcome.changed)
        #expect(files.read(Self.settings) == theirs)
    }

    @Test
    func repairWithNothingToFixWritesNothing() {
        let files = FakeFiles()
        let editor = editor(files)
        _ = editor.add(Self.folder, command())
        files.writes.removeAll()

        let outcome = editor.repairIfOurs(Self.folder, command())

        #expect(!outcome.changed)
        #expect(files.writes.isEmpty)
    }
}
