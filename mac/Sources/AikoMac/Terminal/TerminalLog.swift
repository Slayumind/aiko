import Foundation

/// The only place in the terminal that writes to the log of Aiko (D-298). The twin of TerminalLog.cs.
///
/// A function here takes numbers, yes or no, and the name of an environment. It takes no other
/// text, so a command, a path, an id or a line of the terminal cannot reach the log through it. A
/// test reads this file and the rest of the folder to keep it so.
enum TerminalLog {
    static func started(environment: String, shell: Bool, resumed: Bool) {
        Log.write("terminal: \(shell ? "a shell" : "Claude Code") started for \(environment)"
            + (shell ? "" : resumed ? ", a resumed conversation" : ", a new conversation"))
    }

    /// The code is nil when the program ended without one, as a killed program does.
    static func exited(environment: String, shell: Bool, exitCode: Int?) {
        Log.write("terminal: \(shell ? "a shell" : "Claude Code") for \(environment) exited with "
            + (exitCode.map(String.init) ?? "no code"))
    }

    /// The login shell that tells the PATH of the person did not start. The words of the error stay
    /// out: they may name a path.
    static func loginShellNotStarted() {
        Log.write("terminal: the login shell did not start")
    }
}
