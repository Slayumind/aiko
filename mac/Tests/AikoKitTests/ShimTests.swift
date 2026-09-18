import Testing

@testable import AikoKit

/// The PowerShell profile part of the xUnit file is not ported: PowerShellProfile is Windows only.
struct ShimTests {
    static let home = #"C:\Users\someone"#

    static let settings = EnvironmentSettings([
        AikoEnvironment("Work", [home + #"\.claude"#]),
        AikoEnvironment("Personal", [home + #"\.claude-personal"#], projectFolders: [#"D:\personal"#]),
    ])

    // ---- which environment ----

    @Test
    func plainClaudeInABoundFolderSetsThatFolder() {
        let plan = ShimLaunch.decide(
            .windows, invokedAs: #"C:\aiko\bin\claude.exe"#, workingDirectory: #"D:\personal\app"#,
            settings: Self.settings, userProfile: Self.home)

        #expect(plan.environment?.name == "Personal")
        #expect(plan.variable == .set)
        #expect(plan.configDirectory == Self.home + #"\.claude-personal"#)
        #expect(!plan.isExplicit)
    }

    @Test
    func environment1ClearsTheVariableInsteadOfPointingItAtClaude() {
        let plan = ShimLaunch.decide(
            .windows, invokedAs: "claude", workingDirectory: #"C:\temp"#,
            settings: Self.settings, userProfile: Self.home)

        #expect(plan.environment?.name == "Work")
        #expect(plan.variable == .clear)
        #expect(plan.configDirectory == nil)
    }

    @Test
    func aCommandWinsOverTheBindingAndSaysSo() {
        let plan = ShimLaunch.decide(
            .windows, invokedAs: "aiko-work.exe", workingDirectory: #"D:\personal\app"#,
            settings: Self.settings, userProfile: Self.home)

        #expect(plan.environment?.name == "Work")
        #expect(plan.isExplicit)
    }

    @Test
    func aCustomCommandIsFoundByItsOwnName() {
        var personal = Self.settings.environments[1]
        personal.customCommand = "cc-home"
        let settings = EnvironmentSettings([Self.settings.environments[0], personal])

        let plan = ShimLaunch.decide(
            .windows, invokedAs: "CC-HOME", workingDirectory: #"C:\"#,
            settings: settings, userProfile: Self.home)

        #expect(plan.environment?.name == "Personal")
    }

    @Test
    func aCommandNobodyOwnsStartsClaudeAsItIs() {
        #expect(
            ShimLaunch.decide(
                .windows, invokedAs: "aiko-gone", workingDirectory: #"C:\"#,
                settings: Self.settings, userProfile: Self.home) == .passThrough)
    }

    @Test
    func withNoEnvironmentsNothingChanges() {
        #expect(
            ShimLaunch.decide(
                .windows, invokedAs: "claude", workingDirectory: #"C:\"#,
                settings: .empty, userProfile: Self.home) == .passThrough)
    }

    // ---- the real claude.exe ----

    @Test
    func theRealClaudeIsFoundPastTheShimFolder() {
        let files: Set<String> = [#"C:\aiko\bin\claude.exe"#, #"C:\Users\someone\.local\bin\claude.exe"#]

        let real = RealClaude.find(
            .windows, #"C:\aiko\bin;C:\Windows;C:\Users\someone\.local\bin"#, [#"C:\aiko\bin\"#], files.contains)

        #expect(real == #"C:\Users\someone\.local\bin\claude.exe"#)
    }

    @Test
    func twoShimFoldersNeverStartEachOther() {
        // The spike: an old and a new shim folder in PATH made thousands of processes.
        let files: Set<String> = [#"C:\old\claude.exe"#, #"C:\new\claude.exe"#, #"C:\real\claude.exe"#]
        let path = #"C:\new;C:\old;C:\real"#

        #expect(RealClaude.find(.windows, path, [#"C:\new"#], files.contains) == #"C:\old\claude.exe"#)

        // The old shim was started by the new one, so it inherits the list with both in it.
        let seen = RealClaude.parseSeen(.windows, RealClaude.formatSeen(.windows, [#"C:\new"#, #"C:\old"#]))
        #expect(RealClaude.find(.windows, path, seen, files.contains) == #"C:\real\claude.exe"#)
    }

    @Test
    func aChainThatIsTooLongStops() {
        let seen = (0...RealClaude.maxChain).map { #"C:\s"# + String($0) }

        #expect(RealClaude.find(.windows, #"C:\real"#, seen) { _ in true } == nil)
    }

    @Test
    func quotedAndEmptyPathEntriesAreFine() {
        #expect(
            RealClaude.find(.windows, #";"C:\Program Files\x";;"#, []) { $0 == #"C:\Program Files\x\claude.exe"# }
                == #"C:\Program Files\x\claude.exe"#)
    }

    // ---- the user PATH ----

    @Test
    func theCommandFolderGoesToTheFrontOnce() {
        let value = #"%USERPROFILE%\AppData\Local\Microsoft\WindowsApps;C:\aiko\bin\;C:\Users\someone\.local\bin"#

        #expect(
            UserPathList.addToFront(.windows, value, #"C:\aiko\bin"#)
                == #"C:\aiko\bin;%USERPROFILE%\AppData\Local\Microsoft\WindowsApps;C:\Users\someone\.local\bin"#)
    }

    @Test
    func removingLeavesEverythingElseExactlyAsItWas() {
        let before = #"C:\One;%TWO%\x;c:\THREE"#

        #expect(
            UserPathList.remove(.windows, UserPathList.addToFront(.windows, before, #"C:\aiko\bin"#), #"C:\aiko\bin"#)
                == before)
        #expect(UserPathList.addToFront(.windows, nil, #"C:\aiko\bin"#) == #"C:\aiko\bin"#)
    }
}
