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
            #expect(UninstallPlan.mayDelete(Self.windows, folder, keepMemory: false))
        }

        for folder in UninstallPlan.foldersToDelete(Self.mac) {
            #expect(UninstallPlan.mayDelete(Self.mac, folder, keepMemory: false))
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
        #expect(!UninstallPlan.mayDelete(Self.windows, path, keepMemory: false))
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
        #expect(!UninstallPlan.mayDelete(Self.mac, path, keepMemory: false))
    }

    /// Aiko removes its two folders whole. A folder inside one of them has no delete of its own,
    /// so a caller that names it is asking for something the plan does not do.
    @Test(arguments: [
        #"C:\Users\someone\AppData\Local\Aiko\bin"#,
        #"C:\Users\someone\AppData\Local\Aiko\marketplace"#,
        #"C:\Users\someone\AppData\Roaming\Aiko\settings.json"#,
    ])
    func aFolderInsideOursIsNotDeletedOnItsOwn(_ path: String) {
        #expect(!UninstallPlan.mayDelete(Self.windows, path, keepMemory: false))
    }

    /// People type paths with a trailing separator, and Windows does not care about case.
    @Test
    func theSameFolderWrittenDifferentlyIsStillOurs() {
        #expect(UninstallPlan.mayDelete(Self.windows, #"C:\Users\someone\AppData\Local\Aiko\"#, keepMemory: false))
        #expect(UninstallPlan.mayDelete(Self.windows, #"c:\users\someone\appdata\local\aiko"#, keepMemory: false))
        #expect(UninstallPlan.mayDelete(Self.mac, "/Users/someone/Library/Caches/Aiko/", keepMemory: false))
    }

    // ---- keeping the memory ----

    /// The memory is the one thing in Aiko's folders the person cannot make again. These cases say
    /// when there is something to keep. A file Aiko cannot read counts: the facts are still in it.
    @Test(arguments: [
        (nil, false),
        ("", false),
        ("  \n", false),
        ("{}", false),
        (#"{"schema":1,"nextId":1,"facts":[],"setAside":[]}"#, false),
        (#"{"schema":1,"nextId":2,"facts":[{"id":"F1","category":"you","text":"Learns Swift","seen":"2026-10-05","created":"2026-10-05"}],"setAside":[]}"#, true),
        (#"{"schema":1,"nextId":1,"facts":[],"setAside":[{"note":"typed by hand"}]}"#, true),
        (#"{"schema":1,"facts":[{"text":"no id, so it is set aside"}]}"#, true),
        ("not a memory at all", true),
        ("[]", true),
        (#"{"schema":99,"facts":[]}"#, true),
    ] as [(String?, Bool)])
    func aMemoryWithSomethingInItIsWorthKeeping(_ memoryJson: String?, _ worth: Bool) {
        #expect(UninstallPlan.memoryWorthKeeping(memoryJson) == worth)
    }

    /// The answer is nil when nobody could be asked: the installer's hook has no window. Then the
    /// memory stays. An empty memory is never kept, whatever the answer.
    @Test(arguments: [
        (true, true, true),
        (nil, true, true),
        (false, true, false),
        (true, false, false),
        (nil, false, false),
        (false, false, false),
    ] as [(Bool?, Bool, Bool)])
    func theMemoryStaysUnlessThePersonSaysNo(_ answer: Bool?, _ worthKeeping: Bool, _ kept: Bool) {
        #expect(UninstallPlan.keepsMemory(answer, worthKeeping: worthKeeping) == kept)
    }

    @Test
    func onlyTheMemoryFileStaysAndOnlyWhenItIsKept() {
        #expect(UninstallPlan.filesToKeep(Self.windows, keepMemory: true)
            == [#"C:\Users\someone\AppData\Roaming\Aiko\memory.json"#])
        #expect(UninstallPlan.filesToKeep(Self.mac, keepMemory: true)
            == ["/Users/someone/Library/Application Support/Aiko/memory.json"])

        #expect(UninstallPlan.filesToKeep(Self.windows, keepMemory: false).isEmpty)
        #expect(UninstallPlan.filesToKeep(Self.mac, keepMemory: false).isEmpty)
    }

    /// The folder with the memory is emptied around the file, so it does not pass the guard for
    /// whole folders. The other folder goes whole as before.
    @Test
    func theFolderWithTheKeptMemoryDoesNotGoWhole() {
        for folders in [Self.windows, Self.mac] {
            #expect(UninstallPlan.keepsSomethingIn(folders, folders.settingsFolder, keepMemory: true))
            #expect(!UninstallPlan.mayDelete(folders, folders.settingsFolder, keepMemory: true))

            #expect(!UninstallPlan.keepsSomethingIn(folders, folders.localFolder, keepMemory: true))
            #expect(UninstallPlan.mayDelete(folders, folders.localFolder, keepMemory: true))

            #expect(!UninstallPlan.keepsSomethingIn(folders, folders.settingsFolder, keepMemory: false))
            #expect(!UninstallPlan.keepsSomethingIn(folders, folders.localFolder, keepMemory: false))
        }
    }

    /// Everything next to the memory goes, the lock and the ".bad" copy too: no later Aiko reads
    /// them, and the window says that one file stays.
    @Test(arguments: [
        #"C:\Users\someone\AppData\Roaming\Aiko\settings.json"#,
        #"C:\Users\someone\AppData\Roaming\Aiko\environments.json"#,
        #"C:\Users\someone\AppData\Roaming\Aiko\persona.json"#,
        #"C:\Users\someone\AppData\Roaming\Aiko\install-id"#,
        #"C:\Users\someone\AppData\Roaming\Aiko\memory.json.lock"#,
        #"C:\Users\someone\AppData\Roaming\Aiko\memory.json.bad"#,
        #"C:\Users\someone\AppData\Roaming\Aiko\memory.json.4242.tmp"#,
        #"C:\Users\someone\AppData\Roaming\Aiko\a folder"#,
        #"c:\users\someone\appdata\roaming\aiko\Settings.json\"#,
    ])
    func whatLiesNextToTheKeptMemoryGoesOnWindows(_ path: String) {
        #expect(UninstallPlan.mayDeleteInside(Self.windows, path, keepMemory: true))
    }

    /// The memory under every spelling Windows reads as the same file, the folder itself, the
    /// folder above it, anything deeper, and everything outside the one folder that keeps a file.
    @Test(arguments: [
        #"C:\Users\someone\AppData\Roaming\Aiko\memory.json"#,
        #"C:\Users\someone\AppData\Roaming\Aiko\MEMORY.JSON"#,
        #"C:\Users\someone\AppData\Roaming\Aiko\memory.json\"#,
        #"C:\Users\someone\AppData\Roaming\Aiko\memory.json."#,
        #"C:\Users\someone\AppData\Roaming\Aiko/memory.json"#,
        #"C:\Users\someone\AppData\Roaming\Aiko"#,
        #"C:\Users\someone\AppData\Roaming\Aiko\"#,
        #"C:\Users\someone\AppData\Roaming\Aiko\."#,
        #"C:\Users\someone\AppData\Roaming\Aiko\.."#,
        #"C:\Users\someone\AppData\Roaming\Aiko\a folder\.."#,
        #"C:\Users\someone\AppData\Roaming\Aiko\a folder/.."#,
        #"C:\Users\someone\AppData\Roaming\Aiko\a folder\settings.json"#,
        #"C:\Users\someone\AppData\Roaming"#,
        #"C:\Users\someone\AppData\Local\Aiko\log.txt"#,
        #"C:\Users\someone\AppData\Local\Aiko\bin"#,
        #"C:\Users\someone\.claude\settings.json"#,
        #"C:\Users\someone\.claude"#,
        "memory.json",
        "",
        "   ",
    ])
    func theKeptMemoryAndEverythingOutsideStayOnWindows(_ path: String) {
        #expect(!UninstallPlan.mayDeleteInside(Self.windows, path, keepMemory: true))
    }

    @Test(arguments: [
        "/Users/someone/Library/Application Support/Aiko/settings.json",
        "/Users/someone/Library/Application Support/Aiko/memory.json.lock",
        "/Users/someone/Library/Application Support/Aiko/memory.json.bad",
        "/Users/someone/Library/Application Support/Aiko/a folder/",
    ])
    func whatLiesNextToTheKeptMemoryGoesOnMac(_ path: String) {
        #expect(UninstallPlan.mayDeleteInside(Self.mac, path, keepMemory: true))
    }

    @Test(arguments: [
        "/Users/someone/Library/Application Support/Aiko/memory.json",
        "/Users/someone/Library/Application Support/Aiko/Memory.json",
        "/Users/someone/Library/Application Support/Aiko/memory.json/",
        "/Users/someone/Library/Application Support/Aiko",
        "/Users/someone/Library/Application Support/Aiko/..",
        "/Users/someone/Library/Application Support/Aiko/a folder/settings.json",
        "/Users/someone/Library/Application Support",
        "/Users/someone/Library/Caches/Aiko/log.txt",
        "/Users/someone/.claude/settings.json",
        "/",
    ])
    func theKeptMemoryAndEverythingOutsideStayOnMac(_ path: String) {
        #expect(!UninstallPlan.mayDeleteInside(Self.mac, path, keepMemory: true))
    }

    /// With nothing kept, both folders go whole and nothing is deleted in parts, as before.
    @Test
    func withNoMemoryKeptNothingIsDeletedInParts() {
        #expect(!UninstallPlan.mayDeleteInside(
            Self.windows, #"C:\Users\someone\AppData\Roaming\Aiko\settings.json"#, keepMemory: false))
        #expect(!UninstallPlan.mayDeleteInside(
            Self.windows, #"C:\Users\someone\AppData\Roaming\Aiko\memory.json"#, keepMemory: false))
        #expect(!UninstallPlan.mayDeleteInside(
            Self.mac, "/Users/someone/Library/Application Support/Aiko/settings.json", keepMemory: false))

        #expect(UninstallPlan.mayDelete(Self.windows, Self.windows.settingsFolder, keepMemory: false))
        #expect(UninstallPlan.mayDelete(Self.mac, Self.mac.settingsFolder, keepMemory: false))
    }
}
