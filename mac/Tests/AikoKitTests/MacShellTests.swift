import Testing

@testable import AikoKit

struct ZshProfileTests {
    static let folder = "/Users/someone/Library/Caches/Aiko/bin"

    static var block: String {
        """
        # Aiko: launch commands
        export PATH='/Users/someone/Library/Caches/Aiko/bin':$PATH
        # end Aiko
        """
    }

    @Test
    func anEmptyProfileGetsTheBlockAndNothingElse() {
        #expect(ZshProfile.add("", Self.folder) == Self.block + "\n")
    }

    @Test
    func theBlockGoesAfterOneBlankLineAtTheEnd() {
        let after = ZshProfile.add("export EDITOR=nano\n", Self.folder)

        #expect(after == "export EDITOR=nano\n\n" + Self.block + "\n")
    }

    @Test
    func aProfileWithNoLastNewlineGetsOne() {
        let after = ZshProfile.add("export EDITOR=nano", Self.folder)

        #expect(after == "export EDITOR=nano\n\n" + Self.block + "\n")
    }

    @Test
    func theSameBlockTwiceChangesNothing() {
        let once = ZshProfile.add("export EDITOR=nano\n", Self.folder)!

        #expect(ZshProfile.add(once, Self.folder) == nil)
        #expect(ZshProfile.has(once))
        #expect(ZshProfile.holds(once, Self.folder))
    }

    @Test
    func aBlockPointingSomewhereElseIsRewrittenInPlace() {
        let old = ZshProfile.add("export EDITOR=nano\n", "/old/bin")!
        let new = ZshProfile.add(old, Self.folder)

        #expect(new == "export EDITOR=nano\n\n" + Self.block + "\n")
        #expect(!ZshProfile.holds(old, Self.folder))
    }

    @Test
    func removingPutsTheProfileBackAsItWas() {
        for before in ["", "export EDITOR=nano\n", "export EDITOR=nano\n\n", "a\nb\nc\n"] {
            let after = ZshProfile.add(before, Self.folder)!
            #expect(ZshProfile.remove(after) == before)
        }
    }

    @Test
    func removingKeepsWhatThePersonWroteAfterTheBlock() {
        let after = ZshProfile.add("export EDITOR=nano\n", Self.folder)! + "\nalias ll='ls -l'\n"

        #expect(ZshProfile.remove(after) == "export EDITOR=nano\n\nalias ll='ls -l'\n")
    }

    @Test
    func aProfileWithoutOurBlockIsLeftAlone() {
        #expect(ZshProfile.remove("export EDITOR=nano\n") == nil)
        #expect(!ZshProfile.has("export EDITOR=nano\n"))
        #expect(!ZshProfile.has("# Aiko: launch commands\n"))
    }

    @Test
    func aQuoteInThePathIsClosedEscapedAndOpenedAgain() {
        #expect(ZshProfile.exportLine("/Users/o'brien/bin")
            == #"export PATH='/Users/o'\''brien/bin':$PATH"#)
    }
}

struct MacTerminalTests {
    @Test
    func environmentOneIsOpenedWithTheVariableUnsetTheWayAPlainClaudeRuns() {
        let script = MacTerminal.script(
            claude: "/Users/someone/.local/bin/claude",
            configFolder: "/Users/someone/.claude",
            workingDirectory: "/Users/someone/code",
            isDefaultFolder: true)

        #expect(script.contains("unset CLAUDE_CONFIG_DIR"))
        #expect(!script.contains("export CLAUDE_CONFIG_DIR"))
        #expect(script.contains("cd '/Users/someone/code' || cd \"$HOME\""))
        #expect(script.contains("exec '/Users/someone/.local/bin/claude'"))
        #expect(script.hasPrefix("#!/bin/zsh\n"))
    }

    @Test
    func anotherEnvironmentIsOpenedWithItsFolderInTheVariable() {
        let script = MacTerminal.script(
            claude: "/opt/claude",
            configFolder: "/Users/someone/.claude-work",
            workingDirectory: "/Users/someone",
            isDefaultFolder: false)

        #expect(script.contains("export CLAUDE_CONFIG_DIR='/Users/someone/.claude-work'"))
    }
}

struct ClaudeCodeInstallTests {
    @Test
    func eachSystemShowsItsOwnInstallCommand() {
        #expect(ClaudeCodeInstall.command(.windows) == "irm https://claude.ai/install.ps1 | iex")
        #expect(ClaudeCodeInstall.command(.macOS) == "curl -fsSL https://claude.ai/install.sh | bash")
    }
}

struct DiagnosticsTextTests {
    @Test
    func theTextNamesEverySettingAndNoSecret() {
        var facts = DiagnosticsFacts()
        facts.version = "0.3.0+1a2b3c4"
        facts.system = "macOS 15.7.9"
        facts.place = .island
        facts.language = .russian
        facts.startsAtLogin = true
        facts.environments = 2
        facts.directMode = 1
        facts.boundFolders = 3
        facts.commandsSetUp = true
        facts.shell = "zsh"
        facts.logPath = "/Users/someone/Library/Caches/Aiko/log.txt"

        #expect(DiagnosticsText.build(facts) == """
            Aiko 0.3.0+1a2b3c4
            macOS 15.7.9
            shown in: the island
            language: Русский
            starts at login: yes
            checks for updates: no
            sends statistics: no
            environments: 2, direct mode on for 1, bound folders 3
            launch commands set up: yes
            status line shell: zsh
            log: /Users/someone/Library/Caches/Aiko/log.txt
            """)
    }

    @Test
    func theMenuBarAndTheSystemLanguageAreSpelledForAPerson() {
        var facts = DiagnosticsFacts()
        facts.place = .tray

        let lines = DiagnosticsText.build(facts).components(separatedBy: "\n")
        #expect(lines[2] == "shown in: the menu bar")
        #expect(lines[3] == "language: the one macOS uses")
    }
}
