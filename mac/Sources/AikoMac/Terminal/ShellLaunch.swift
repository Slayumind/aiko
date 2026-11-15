import AikoKit
import Foundation

/// The shell of a command line session and how it starts, the twin of ShellLaunch.cs: the shell of
/// the person, with Aiko's marks of shell integration after their own files (D-412). zsh reads the
/// files of Shell/zsh in the bundle, which read the person's first; bash reads aiko.bash, the same
/// file Windows gives Git Bash. Another shell starts as it is, and its session has no blocks: the
/// real terminal shows it.
struct ShellLaunch {
    let executable: String
    let arguments: [String]
    let variables: [VariableChange]

    /// The short name the questions to Claude Code tell shells apart by: zsh, bash or another.
    let name: String

    /// Whether the shell leaves the marks the blocks are cut by.
    let marks: Bool

    static func find() -> ShellLaunch {
        let shell = ProcessEnvironment.shell
        let name = (shell as NSString).lastPathComponent
        let scripts = (Bundle.main.resourcePath.map { ($0 as NSString).appendingPathComponent("Shell") }) ?? ""

        let zsh = (scripts as NSString).appendingPathComponent("zsh")
        if name == "zsh", FileManager.default.fileExists(atPath: (zsh as NSString).appendingPathComponent(".zshrc")) {
            // The person's own ZDOTDIR, when they set one, is where Aiko's files read theirs from.
            let own = ProcessInfo.processInfo.environment["ZDOTDIR"] ?? ""
            return ShellLaunch(
                executable: shell, arguments: ["-l"],
                variables: [VariableChange("ZDOTDIR", zsh), VariableChange("AIKO_USER_ZDOTDIR", own.isEmpty ? nil : own)],
                name: name, marks: true)
        }

        let bash = (scripts as NSString).appendingPathComponent("aiko.bash")
        if name == "bash", FileManager.default.fileExists(atPath: bash) {
            return ShellLaunch(executable: shell, arguments: ["--rcfile", bash, "-i"], variables: [], name: name, marks: true)
        }

        return ShellLaunch(executable: shell, arguments: ["-l"], variables: [], name: name, marks: false)
    }
}
