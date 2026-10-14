import AikoKit
import Foundation

/// The only place in the terminal that writes to the log of Aiko (D-298).
///
/// A method here takes numbers, yes or no, and the name of an environment. It takes no other
/// text, so a message, a path, an id or a line of Claude Code cannot reach the log through it. A
/// test reads this file and the rest of the folder to keep it so.
///
/// The twin of Terminal/TerminalLog.cs.
struct AppTerminalLog: TerminalLog {
    func started(_ environment: String, resumed: Bool) {
        Log.write("terminal: Claude Code started for \(environment), \(resumed ? "a resumed" : "a new") conversation")
    }

    func startFailed(_ environment: String) {
        Log.write("terminal: Claude Code did not start for \(environment)")
    }

    func exited(_ environment: String, exitCode: Int, linesRead: Int, linesWritten: Int) {
        Log.write(
            "terminal: Claude Code for \(environment) exited with \(exitCode), "
                + "lines read \(linesRead), written \(linesWritten)")
    }

    func stopped(_ environment: String, linesRead: Int, linesWritten: Int) {
        Log.write(
            "terminal: Claude Code for \(environment) was stopped, lines read \(linesRead), written \(linesWritten)")
    }

    // MARK: - What the doors report

    static func pipeChecked(
        started: Bool, letters: Bool, longLine: Bool, variables: Bool, exitSeen: Bool, treeGone: Bool
    ) {
        Log.write(
            "--try-pipe: started=\(started), letters=\(letters), long line=\(longLine), "
                + "variables=\(variables), exit seen=\(exitSeen), tree gone=\(treeGone)")
    }

    static func conversationChecked(
        read: Bool, markSeen: Bool, logClean: Bool, items: Int, starts: Int, written: Int
    ) {
        Log.write(
            "--try-conversation: script read=\(read), mark in the conversation=\(markSeen), "
                + "log clean=\(logClean), items \(items), starts \(starts), lines written \(written)")
    }

    static func historyChecked(found: Bool, items: Int, messages: Int, answers: Int, steps: Int, notices: Int) {
        Log.write(
            "--try-history: file found=\(found), items \(items): messages \(messages), answers \(answers), "
                + "steps \(steps), notices \(notices)")
    }

    /// The window of --try-terminal: the state by its number in TerminalWindowCheck.states, and
    /// where the window stands. The count of the states is the real terminal, minus one a state
    /// that does not exist.
    static func windowChecked(state: Int, left: Int, top: Int, width: Int, height: Int, window: Int) {
        Log.write("--try-terminal: state \(state) at \(left),\(top) \(width)x\(height), window \(window)")
    }

    static func windowCheckDone(state: Int) {
        Log.write("--try-terminal: state \(state) done")
    }

    static func realChecked(claudeFound: Bool, answered: Bool, signedOut: Bool, failed: Bool, items: Int) {
        Log.write(
            "--try-conversation real: claude found=\(claudeFound), answered=\(answered), "
                + "signed out=\(signedOut), failed=\(failed), items \(items)")
    }
}
