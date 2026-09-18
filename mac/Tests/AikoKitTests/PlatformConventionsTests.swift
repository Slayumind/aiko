import Foundation
import Testing

@testable import AikoKit

/// The core takes the platform as a description, so its rules must follow whatever description
/// it gets. The system here is made up on purpose: it is neither Windows nor macOS, so a rule
/// that only works for the two we ship fails these cases.
struct PlatformConventionsTests {
    static let other = PlatformConventions(
        executableSuffix: "",
        pathListSeparator: ":",
        directorySeparator: "/",
        localDataVariable: nil,
        fallbackShell: ShellProgram(.gitBash, "/bin/sh", ["-c"]))

    @Test
    func windowsNamesProgramsWithExeAndListsFoldersWithASemicolon() {
        #expect(PlatformConventions.windows.executableName("claude") == "claude.exe")
        #expect(PlatformConventions.windows.pathListSeparator == ";")
        #expect(PlatformConventions.windows.localDataVariable == "%LOCALAPPDATA%")
    }

    @Test
    func withoutGitBashWindowsFallsBackToPowerShell() {
        let call = ClaudeShellLookup.callFor(.windows, nil, "line")

        #expect(ClaudeShellLookup.shellFor(.windows, nil) == .powerShell)
        #expect(call.fileName == "powershell.exe")
        #expect(call.arguments == ["-NoProfile", "-NonInteractive", "-Command", "line"])
    }

    @Test
    func theRealClaudeIsFoundWithTheNamesAndSeparatorOfThePlatform() {
        let found = RealClaude.find(Self.other, "/opt/aiko/bin:/usr/local/bin", ["/opt/aiko/bin"]) {
            $0.hasSuffix("claude")
        }

        #expect(found == "/usr/local/bin/claude")
        #expect(RealClaude.formatSeen(Self.other, ["/a", "/b"]) == "/a:/b")
    }

    @Test
    func ourBridgeIsRecognisedByTheProgramNameOfThePlatform() {
        #expect(BridgeCommand.isAiko(Self.other, #""/Applications/Aiko.app/Aiko.Bridge""#))
        #expect(!BridgeCommand.isAiko(Self.other, #""C:\Aiko\Aiko.Bridge.exe""#))
    }

    @Test
    func commandsAreLinksNamedLikeThePlatformNamesPrograms() {
        let plan = CommandLinks.plan(
            Self.other, EnvironmentSettings([AikoEnvironment("Work", ["/w"])]), ["/bin/claude"])

        #expect(plan.toAdd == ["aiko-work"])
    }

    @Test
    func withoutAVariableForTheLocalFolderThePersonaCommandUsesTheFullPath() {
        let folders = AikoFolders(Self.other, "/home/someone/settings", "/home/someone/local")
        let bridge = Self.other.join(folders.installedAppFolder, "Aiko.Bridge")

        #expect(
            AikoMarketplace.personaCommand(Self.other, bridge, folders)
                == "\"\(bridge)\" plugin aiko-persona")
    }

    @Test
    func theFallbackShellOfThePlatformRunsTheWrappedLine() {
        let call = ClaudeShellLookup.callFor(Self.other, nil, "line")

        #expect(call.fileName == "/bin/sh")
        #expect(call.arguments == ["-c", "line"])
    }
}

/// What the Windows build writes and looks for today. These names are on people's disks and in
/// their PATH, so moving the platform details out of the core must keep every one of them.
struct PlatformPinTests {
    @Test
    func theShimAndTheCommandsAreExeFiles() {
        #expect(CommandLinks.shimFileName(.windows) == "claude.exe")
        #expect(CommandLinks.fileNameFor(.windows, "aiko-work") == "aiko-work.exe")
    }

    @Test
    func theListOfSeenShimFoldersIsJoinedWithASemicolon() {
        #expect(RealClaude.formatSeen(.windows, [#"C:\new"#, #"C:\old"#]) == #"C:\new;C:\old"#)
        #expect(RealClaude.parseSeen(.windows, #" C:\new ;;C:\old;"#) == [#"C:\new"#, #"C:\old"#])
    }

    @Test
    func theRealClaudeIsAnExeFile() {
        #expect(RealClaude.find(.windows, #"C:\tools"#, []) { _ in true } == #"C:\tools\claude.exe"#)
    }

    @Test
    func aBridgeWithoutTheExeSuffixIsNotOurs() {
        #expect(!BridgeCommand.isAiko(.windows, #""C:\Aiko\Aiko.Bridge""#))
        #expect(!BridgeCommand.isAiko(.windows, #""C:\Aiko\Aiko.Bridge.cmd""#))
    }

    @Test
    func theUserPathKeepsEntriesWithSpacesAndJoinsWithASemicolon() {
        #expect(
            UserPathList.addToFront(.windows, #" C:\x ;;C:\y"#, #"C:\aiko\bin"#)
                == #"C:\aiko\bin; C:\x ;C:\y"#)
        #expect(UserPathList.contains(.windows, #"C:\x;C:\aiko\bin\"#, #"C:\aiko\bin"#))
    }

    @Test
    func thePersonaCommandOutsideTheInstallUsesTheFullPath() {
        #expect(
            AikoMarketplace.personaCommand(
                .windows,
                #"C:\Users\someone\AppData\Local\Other\Aiko.Bridge.exe"#,
                AikoFolders.windows(#"C:\Users\someone\AppData\Roaming"#, #"C:\Users\someone\AppData\Local"#))
                == #""C:\Users\someone\AppData\Local\Other\Aiko.Bridge.exe" plugin aiko-persona"#)
    }
}

/// What the macOS build writes and looks for (D-243, D-244, D-250). The Windows core pins the same
/// cases in MacPlatformTests, so a change here has to be made twice on purpose.
struct MacPlatformTests {
    static let home = "/Users/someone"

    static let folders = AikoFolders.macOS(
        home + "/Library/Application Support", home + "/Library/Caches")

    @Test
    func aProgramHasNoSuffixAndFoldersAreListedWithAColon() {
        #expect(PlatformConventions.macOS.executableName("claude") == "claude")
        #expect(PlatformConventions.macOS.pathListSeparator == ":")
        #expect(PlatformConventions.macOS.directorySeparator == "/")
    }

    @Test
    func aCommandSpellsTheRealPathBecauseThereIsNoVariableForIt() {
        // %LOCALAPPDATA% is a cmd.exe idea. A shell on macOS would pass "%LOCALAPPDATA%" through
        // as four plain characters, and Claude Code would run a command that points nowhere.
        #expect(PlatformConventions.macOS.localDataVariable == nil)

        let bridge = "/Applications/Aiko.app/Contents/MacOS/Aiko.Bridge"

        #expect(
            AikoMarketplace.personaCommand(.macOS, bridge, Self.folders)
                == "\"\(bridge)\" plugin aiko-persona")
    }

    @Test
    func theStatusLineRunsInALoginZsh() {
        // -l so the line sees the PATH the person's own tools are on; there is no Git Bash to
        // prefer, so this is the only shell the Mac build ever uses.
        let call = ClaudeShellLookup.callFor(.macOS, nil, "line")

        #expect(ClaudeShellLookup.shellFor(.macOS, nil) == .zsh)
        #expect(call.fileName == "/bin/zsh")
        #expect(call.arguments == ["-lc", "line"])
    }

    @Test
    func zshGetsNoCallOperatorBecauseOnlyPowerShellNeedsOne() {
        let bridge = "/Applications/Aiko.app/Contents/MacOS/Aiko.Bridge"

        #expect(BridgeCommand.forPath(bridge, .zsh) == "\"\(bridge)\"")
        #expect(BridgeCommand.isAiko(.macOS, BridgeCommand.forPath(bridge, .zsh)))
        #expect(!BridgeCommand.isAiko(.macOS, #""/Applications/Aiko.app/Aiko.Bridge.exe""#))
    }

    @Test
    func theShimAndTheCommandsArePlainFilesWithNoSuffix() {
        #expect(CommandLinks.shimFileName(.macOS) == "claude")
        #expect(CommandLinks.fileNameFor(.macOS, "aiko-work") == "aiko-work")
    }

    @Test
    func theListOfSeenShimFoldersIsJoinedWithAColon() {
        #expect(RealClaude.formatSeen(.macOS, ["/a/bin", "/b/bin"]) == "/a/bin:/b/bin")
        #expect(RealClaude.parseSeen(.macOS, " /a/bin ::/b/bin:") == ["/a/bin", "/b/bin"])
    }

    @Test
    func theRealClaudeIsFoundPastTheCommandFolder() {
        let files: Set<String> = ["/Users/someone/Library/Caches/Aiko/bin/claude", "/opt/homebrew/bin/claude"]

        #expect(
            RealClaude.find(
                .macOS,
                "/Users/someone/Library/Caches/Aiko/bin:/opt/homebrew/bin",
                ["/Users/someone/Library/Caches/Aiko/bin"],
                files.contains) == "/opt/homebrew/bin/claude")
    }

    @Test
    func claudeCodeFallsBackToTheFolderItsOwnInstallerUses() {
        let native = Self.home + "/.local/bin/claude"

        #expect(
            ClaudeInstall.find(
                .macOS, freshPath: "/usr/bin", commandFolder: "/tmp/bin", userProfile: Self.home,
                exists: { $0 == native }) == native)
    }

    @Test
    func aNewEnvironmentFolderSitsBesideClaudeInTheHomeFolder() {
        #expect(
            ClaudeInstall.newConfigFolder(
                .macOS, environmentName: "Work", userProfile: Self.home, folderExists: { _ in false })
                == Self.home + "/.claude-work")
        #expect(
            ClaudeInstall.credentialsPathIn(.macOS, Self.home + "/.claude")
                == Self.home + "/.claude/.credentials.json")
    }

    @Test
    func thePersonsChoicesLiveInApplicationSupport() {
        let support = Self.home + "/Library/Application Support/Aiko"

        #expect(Self.folders.settingsFolder == support)
        #expect(Self.folders.settingsFile == support + "/settings.json")
        #expect(Self.folders.environmentsFile == support + "/environments.json")
        #expect(Self.folders.personaFile == support + "/persona.json")
        #expect(Self.folders.installIdFile == support + "/install-id")
        #expect(Self.folders.reportedPeriodsFile == support + "/reported.json")
    }

    @Test
    func whatAikoMakesForItselfLivesInCaches() {
        let caches = Self.home + "/Library/Caches/Aiko"

        #expect(Self.folders.localFolder == caches)
        #expect(Self.folders.snapshotsFolder == caches + "/environments")
        #expect(Self.folders.snapshotFile("claude-work") == caches + "/environments/claude-work.json")
        #expect(Self.folders.activityFolder == caches + "/activity")
        #expect(Self.folders.directCacheFolder == caches + "/direct")
        #expect(Self.folders.commandsFolder == caches + "/bin")
        #expect(Self.folders.marketplaceFolder == caches + "/marketplace")
        #expect(Self.folders.marketplaceFile == caches + "/marketplace/.claude-plugin/marketplace.json")
        #expect(Self.folders.personaPluginsFolder == caches + "/plugins")
        #expect(Self.folders.logFile == caches + "/log.txt")
    }

    @Test
    func bothSystemsNameTheFilesUnderTheBasesAlike() {
        // Only the two base folders differ, so a rule written about "the snapshots folder" holds
        // on either system and the two cores keep one layout between them.
        let windows = AikoFolders.windows(#"C:\Roaming"#, #"C:\Local"#)

        #expect(windows.snapshotFile("claude") == #"C:\Local\Aiko\environments\claude.json"#)
        #expect(Self.folders.snapshotFile("claude") == "/Users/someone/Library/Caches/Aiko/environments/claude.json")
    }

    @Test
    func aBaseFolderWrittenWithATrailingSlashDoesNotDoubleIt() {
        #expect(
            AikoFolders.macOS("/x", "/Users/someone/Library/Caches/").localFolder
                == "/Users/someone/Library/Caches/Aiko")
    }

    @Test
    func theMacFoldersOfThisComputerSitUnderTheUsersLibrary() {
        // The one place the Mac app asks the system where to write.
        let folders = AikoFolders.forThisMac()

        #expect(folders.settingsFolder.hasSuffix("/Library/Application Support/Aiko"))
        #expect(folders.localFolder.hasSuffix("/Library/Caches/Aiko"))
    }
}
