import Testing

@testable import AikoKit

struct ClaudeInstallTests {
    static let home = #"C:\Users\someone"#

    @Test
    func freshPathJoinsMachineAndUserAndExpandsThem() {
        let path = ClaudeInstall.freshPath(
            .windows,
            machinePath: #"C:\Windows"#,
            userPath: #"%USERPROFILE%\.local\bin"#,
            expand: { $0.replacingOccurrences(of: "%USERPROFILE%", with: Self.home) })

        #expect(path == #"C:\Windows;C:\Users\someone\.local\bin"#)
        #expect(
            ClaudeInstall.freshPath(.windows, machinePath: #"C:\Windows"#, userPath: nil, expand: { $0 })
                == #"C:\Windows"#)
    }

    @Test
    func findsClaudePastTheCommandFolder() {
        let files: Set<String> = [#"C:\aiko\bin\claude.exe"#, #"C:\tools\claude.exe"#]

        #expect(
            ClaudeInstall.find(
                .windows, freshPath: #"C:\aiko\bin;C:\tools"#, commandFolder: #"C:\aiko\bin"#,
                userProfile: Self.home, exists: files.contains) == #"C:\tools\claude.exe"#)
    }

    @Test
    func fallsBackToTheNativeInstallerFolder() {
        let native = Self.home + #"\.local\bin\claude.exe"#

        #expect(
            ClaudeInstall.find(
                .windows, freshPath: #"C:\Windows"#, commandFolder: #"C:\aiko\bin"#,
                userProfile: Self.home, exists: { $0 == native }) == native)
        #expect(
            ClaudeInstall.find(
                .windows, freshPath: #"C:\Windows"#, commandFolder: #"C:\aiko\bin"#,
                userProfile: Self.home, exists: { _ in false }) == nil)
    }

    @Test(arguments: [
        ("Work", #"C:\Users\someone\.claude-work"#),
        ("Основная", #"C:\Users\someone\.claude-osnovnaya"#),
        ("!!", #"C:\Users\someone\.claude-env"#),
    ] as [(String, String)])
    func aNewFolderIsNamedAfterTheEnvironment(name: String, folder: String) {
        #expect(
            ClaudeInstall.newConfigFolder(
                .windows, environmentName: name, userProfile: Self.home, folderExists: { _ in false })
                == folder)
    }

    @Test
    func aTakenFolderNameGetsANumber() {
        let taken: Set<String> = [Self.home + #"\.claude-work"#, Self.home + #"\.claude-work-2"#]

        #expect(
            ClaudeInstall.newConfigFolder(
                .windows, environmentName: "Work", userProfile: Self.home,
                folderExists: { path in taken.contains(where: { $0.caseInsensitiveCompare(path) == .orderedSame }) })
                == Self.home + #"\.claude-work-3"#)
    }
}
