import Testing

@testable import AikoKit

struct SnapshotNameTests {
    @Test(arguments: [
        (#"C:\Users\someone\.claude-personal"#, "claude-personal"),
        (#"C:\Users\someone\.claude"#, "claude"),
        (#"C:\Users\someone\.claude-work\"#, "claude-work"),
        ("/home/someone/.claude", "claude"),
    ] as [(String, String)])
    func theFileIsNamedAfterTheConfigFolder(directory: String, expected: String) {
        #expect(SnapshotName.forConfigDirectory(directory) == expected)
    }

    @Test(arguments: [nil, "", "   "] as [String?])
    func withoutTheVariableTheNameIsTheDefaultOne(directory: String?) {
        #expect(SnapshotName.forConfigDirectory(directory) == SnapshotName.default)
    }

    @Test
    func charactersThatDoNotBelongInAFileNameAreDropped() {
        #expect(SnapshotName.clean("claude:work?") == "claudework")
    }

    @Test
    func aFolderNamedInRussianKeepsItsLetters() {
        // The user's home folder can be in any language, and the file name follows it.
        #expect(SnapshotName.clean(".клод") == "клод")
    }

    @Test
    func aNameLeftWithNothingFallsBack() {
        #expect(SnapshotName.clean("...") == SnapshotName.default)
        #expect(SnapshotName.clean("???") == SnapshotName.default)
    }
}

/// The snapshot name is part of the files already on people's disks. The bridge writes a file under
/// this name and the tray looks for the same name, so these cases must never change.
struct SnapshotNamePinTests {
    @Test(arguments: [
        (#"C:\Users\someone\.claude"#, "claude"),
        (#"C:\Users\someone\.claude-work"#, "claude-work"),
        (#"C:\Users\Александр Иванов\.claude-osnovnaya"#, "claude-osnovnaya"),
        (#"D:\Claude Accounts\.claude-work-2"#, "claude-work-2"),
    ] as [(String, String)])
    func aTypicalWindowsFolderKeepsTheNameItHasToday(directory: String, expected: String) {
        #expect(SnapshotName.forConfigDirectory(directory) == expected)
    }

    @Test(arguments: [
        #"C:\a\.claude-work"#,
        "C:/a/.claude-work",
        #"C:\a\.claude-work\"#,
        "C:/a/.claude-work/",
        #"C:\a/.claude-work\\"#,
    ])
    func theSameFolderGetsTheSameNameWhateverSeparatorsItIsWrittenWith(directory: String) {
        #expect(SnapshotName.forConfigDirectory(directory) == "claude-work")
    }

    @Test(arguments: [#"C:\"#, "C:", #"\"#])
    func aDriveOrARootWithNoFolderNameGetsTheDefaultName(directory: String) {
        #expect(SnapshotName.forConfigDirectory(directory) == SnapshotName.default)
    }

    @Test
    func aFolderOnANetworkShareIsNamedAfterItsLastPart() {
        #expect(SnapshotName.forConfigDirectory(#"\\server\share\users\someone\.claude-team"#) == "claude-team")
    }
}
