import Testing

@testable import AikoKit

struct LoginShellPathTests {
    @Test
    func thePathIsTakenFromTheMarkedLine() {
        let output = "Welcome back\n__AIKO_PATH__/opt/homebrew/bin:/usr/bin:/bin\n"

        #expect(LoginShellPath.parse(output) == "/opt/homebrew/bin:/usr/bin:/bin")
    }

    @Test
    func whatAProfilePrintsOnTheSameLineIsLeftOut() {
        #expect(LoginShellPath.parse("\u{1B}[0m__AIKO_PATH__/usr/bin:/bin\r\n") == "/usr/bin:/bin")
    }

    @Test
    func theLastMarkedLineWins() {
        #expect(LoginShellPath.parse("__AIKO_PATH__/old\n__AIKO_PATH__/new/bin\n") == "/new/bin")
    }

    @Test
    func anAnswerWithoutAPathIsNoAnswer() {
        #expect(LoginShellPath.parse("") == nil)
        #expect(LoginShellPath.parse("zsh: command not found\n") == nil)
        #expect(LoginShellPath.parse("__AIKO_PATH__\n") == nil)
        #expect(LoginShellPath.parse("__AIKO_PATH__bin:sbin\n") == nil)
    }

    @Test
    func theShellIsAskedAsALoginAndAsAConsole() {
        #expect(LoginShellPath.arguments == ["-lic", "echo __AIKO_PATH__$PATH"])
    }
}
