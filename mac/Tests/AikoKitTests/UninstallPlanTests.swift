import Testing

@testable import AikoKit

/// Removing Aiko is the one place where the app deletes folders. These cases name every folder it
/// may delete and, more important, the ones it may not: the accounts and the history in the Claude
/// Code folders were never Aiko's to remove.
///
/// The twin of UninstallPlanTests.cs.
struct UninstallPlanTests {
    static let windows = AikoFolders.windows(
        #"C:\Users\someone\AppData\Roaming"#, #"C:\Users\someone\AppData\Local"#)

    static let mac = AikoFolders.macOS(
        "/Users/someone/Library/Application Support", "/Users/someone/Library/Caches")

    @Test
    func twoFoldersGoAndNoOthers() {
        #expect(UninstallPlan.foldersToDelete(Self.windows) == [
            #"C:\Users\someone\AppData\Roaming\Aiko"#,
            #"C:\Users\someone\AppData\Local\Aiko"#,
        ])

        #expect(UninstallPlan.foldersToDelete(Self.mac) == [
            "/Users/someone/Library/Application Support/Aiko",
            "/Users/someone/Library/Caches/Aiko",
        ])
    }

    @Test
    func aikosOwnFoldersMayGo() {
        for folder in UninstallPlan.foldersToDelete(Self.windows) {
            #expect(UninstallPlan.mayDelete(Self.windows, folder))
        }

        for folder in UninstallPlan.foldersToDelete(Self.mac) {
            #expect(UninstallPlan.mayDelete(Self.mac, folder))
        }
    }

    @Test(arguments: [
        #"C:\Users\someone\.claude"#,
        #"C:\Users\someone\.claude-work"#,
        #"C:\Users\someone\.claude.json"#,
        #"C:\Users\someone"#,
        #"C:\Users\someone\AppData\Roaming"#,
        #"C:\Users\someone\AppData\Local"#,
        #"C:\Users\someone\AppData\Local\Slayumind.Aiko"#,
        #"C:\"#,
        "",
        "   ",
    ])
    func nothingOfSomebodyElsesMayGoOnWindows(_ path: String) {
        #expect(!UninstallPlan.mayDelete(Self.windows, path))
    }

    @Test(arguments: [
        "/Users/someone/.claude",
        "/Users/someone/.claude-aiko",
        "/Users/someone/.zshrc",
        "/Users/someone/.zshrc.aiko-backup",
        "/Users/someone",
        "/Users/someone/Library",
        "/Users/someone/Library/Caches",
        "/Applications",
        "/Applications/Aiko.app",
        "/",
    ])
    func nothingOfSomebodyElsesMayGoOnMac(_ path: String) {
        #expect(!UninstallPlan.mayDelete(Self.mac, path))
    }

    /// Aiko removes its two folders whole. A folder inside one of them has no delete of its own,
    /// so a caller that names it is asking for something the plan does not do.
    @Test(arguments: [
        #"C:\Users\someone\AppData\Local\Aiko\bin"#,
        #"C:\Users\someone\AppData\Local\Aiko\marketplace"#,
        #"C:\Users\someone\AppData\Roaming\Aiko\settings.json"#,
    ])
    func aFolderInsideOursIsNotDeletedOnItsOwn(_ path: String) {
        #expect(!UninstallPlan.mayDelete(Self.windows, path))
    }

    /// People type paths with a trailing separator, and Windows does not care about case.
    @Test
    func theSameFolderWrittenDifferentlyIsStillOurs() {
        #expect(UninstallPlan.mayDelete(Self.windows, #"C:\Users\someone\AppData\Local\Aiko\"#))
        #expect(UninstallPlan.mayDelete(Self.windows, #"c:\users\someone\appdata\local\aiko"#))
        #expect(UninstallPlan.mayDelete(Self.mac, "/Users/someone/Library/Caches/Aiko/"))
    }
}
