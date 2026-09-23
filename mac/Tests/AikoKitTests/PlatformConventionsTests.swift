import Foundation
import Testing

@testable import AikoKit

/// The core takes the platform as a description, so its rules must follow whatever description
/// it gets. The system here is made up on purpose: it is neither Windows nor macOS, so a rule
/// that only works for the two we ship fails these cases.
///
/// What each platform names and where it puts things is a table, and tables live in
/// spec/cases/platform-paths; both cores read it. What stays here needs a settings object or asks
/// this computer itself, which a shared case cannot do.
///
/// The twin of PlatformConventionsTests.cs.
struct PlatformConventionsTests {
    static let other = PlatformConventions(
        executableSuffix: "",
        pathListSeparator: ":",
        directorySeparator: "/",
        localDataVariable: nil,
        fallbackShell: ShellProgram(.gitBash, "/bin/sh", ["-c"]))

    @Test
    func commandsAreLinksNamedLikeThePlatformNamesPrograms() {
        let plan = CommandLinks.plan(
            Self.other, EnvironmentSettings([AikoEnvironment("Work", ["/w"])]), ["/bin/claude"])

        #expect(plan.toAdd == ["aiko-work"])
    }

    @Test
    func theMacFoldersOfThisComputerSitUnderTheUsersLibrary() {
        // The one place the Mac app asks the system where to write.
        let folders = AikoFolders.forThisMac()

        #expect(folders.settingsFolder.hasSuffix("/Library/Application Support/Aiko"))
        #expect(folders.localFolder.hasSuffix("/Library/Caches/Aiko"))
    }
}
