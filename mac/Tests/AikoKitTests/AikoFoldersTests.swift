import Testing

@testable import AikoKit

/// The folders the app, the bridge and the shim share. Every path here is on people's disks
/// already, so each one is written out in full: a changed name would lose their settings or split
/// the bridge from the tray.
struct AikoFoldersTests {
    static let folders = AikoFolders.windows(
        #"C:\Users\someone\AppData\Roaming"#, #"C:\Users\someone\AppData\Local"#)

    @Test
    func thePersonsChoicesLiveUnderAppDataAiko() {
        let roaming = #"C:\Users\someone\AppData\Roaming\Aiko"#

        #expect(Self.folders.settingsFolder == roaming)
        #expect(Self.folders.settingsFile == roaming + #"\settings.json"#)
        #expect(Self.folders.environmentsFile == roaming + #"\environments.json"#)
        #expect(Self.folders.personaFile == roaming + #"\persona.json"#)
        #expect(Self.folders.installIdFile == roaming + #"\install-id"#)
        #expect(Self.folders.reportedPeriodsFile == roaming + #"\reported.json"#)
    }

    @Test
    func whatAikoMakesForItselfLivesUnderLocalAppDataAiko() {
        let local = #"C:\Users\someone\AppData\Local\Aiko"#

        #expect(Self.folders.localFolder == local)
        #expect(Self.folders.snapshotsFolder == local + #"\environments"#)
        #expect(Self.folders.activityFolder == local + #"\activity"#)
        #expect(Self.folders.directCacheFolder == local + #"\direct"#)
        #expect(Self.folders.commandsFolder == local + #"\bin"#)
        #expect(Self.folders.marketplaceFolder == local + #"\marketplace"#)
        #expect(Self.folders.marketplaceFile == local + #"\marketplace\.claude-plugin\marketplace.json"#)
        #expect(Self.folders.personaPluginsFolder == local + #"\plugins"#)
        #expect(Self.folders.logFile == local + #"\log.txt"#)
    }

    @Test
    func theInstalledAppIsWhereVelopackPutsIt() {
        #expect(Self.folders.installedAppFolder == #"C:\Users\someone\AppData\Local\Slayumind.Aiko\current"#)
    }

    @Test
    func aSnapshotFileIsNamedAfterTheSnapshot() {
        #expect(
            Self.folders.snapshotFile(SnapshotName.forConfigDirectory(#"C:\Users\someone\.claude-work\"#))
                == #"C:\Users\someone\AppData\Local\Aiko\environments\claude-work.json"#)
    }

    @Test
    func aSecondEnvironmentFolderSitsBesideClaude() {
        #expect(ClaudeConfigFolder.namedFolderName("work") == ".claude-work")
        #expect(ClaudeConfigFolder.searchPattern == ".claude*")
    }
}
