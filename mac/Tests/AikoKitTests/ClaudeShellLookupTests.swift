import Testing

@testable import AikoKit

/// Calling somebody else's status line with the shell it was written for.
///
/// The bridge used to run every wrapped command through cmd.exe while Aiko wrote its own line for
/// Git Bash. A bash line put through cmd.exe prints nothing and reports nothing, so the promise
/// that an existing status line keeps working was quietly false.
///
/// Finding Git Bash is in WindowsGitBash, which is Windows only and is not ported. Here the path
/// comes in ready, the way the app hands it over.
struct ClaudeShellLookupTests {
    static let gitBash = #"C:\Program Files\Git\bin\bash.exe"#

    @Test
    func theShellFollowsWhetherBashWasFound() {
        #expect(ClaudeShellLookup.shellFor(.windows, Self.gitBash) == .gitBash)
        #expect(ClaudeShellLookup.shellFor(.windows, nil) == .powerShell)
    }

    @Test
    func withGitTheWrappedCommandRunsInBash() {
        let call = ClaudeShellLookup.callFor(.windows, Self.gitBash, "~/bin/my-line.sh")

        #expect(call.fileName == Self.gitBash)
        #expect(call.arguments == ["-c", "~/bin/my-line.sh"])
    }

    @Test
    func withoutGitTheWrappedCommandRunsInPowerShell() {
        let call = ClaudeShellLookup.callFor(.windows, nil, "& my-line.ps1")

        #expect(call.fileName == "powershell.exe")
        #expect(call.arguments.contains("-NoProfile"))
        #expect(call.arguments.contains("-NonInteractive"))
        #expect(call.arguments.last == "& my-line.ps1")
    }

    @Test
    func theCommandIsPassedWholeAndNeverTakenApart() {
        // Quotes and spaces inside somebody else's command are theirs. The runtime quotes each
        // argument on its own, so nothing here has to escape anything.
        let awkward = #"jq -r '.model.display_name + " ok"' "#

        let call = ClaudeShellLookup.callFor(.windows, Self.gitBash, awkward)

        #expect(call.arguments.last == awkward)
    }
}
