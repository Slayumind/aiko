import Foundation

/// What it takes to start one child process: the program, its arguments, what changes in its
/// environment and the folder it works in.
public struct ProcessStart: Sendable, Equatable {
    public let executable: String
    public let arguments: [String]
    public let variables: [VariableChange]
    public let workingDirectory: String

    public init(executable: String, arguments: [String], variables: [VariableChange], workingDirectory: String) {
        self.executable = executable
        self.arguments = arguments
        self.variables = variables
        self.workingDirectory = workingDirectory
    }
}

/// One run of Claude Code as a child process (D-297). The real one starts claude; a scripted one
/// answers from a list, so a conversation can be played without Claude Code.
///
/// A process is used once: after it has exited, a new one is made for the next start.
///
/// The twin of IConversationProcess.
public protocol ConversationProcess: AnyObject {
    /// Starts the process. Its lines and its exit come on a thread of its own, the lines in the
    /// order they were written and the exit after the last of them. False when it did not start;
    /// then nothing is ever called.
    func start(
        _ start: ProcessStart,
        onLine: @escaping @Sendable (String) -> Void,
        onExit: @escaping @Sendable (Int) -> Void
    ) -> Bool

    /// Writes one line to the process. False when the process is gone.
    func write(_ line: String) -> Bool

    /// Closes the input of the process, which asks Claude Code to leave. A process that stays is
    /// ended a little later, with everything it started.
    func stop()
}

/// What the terminal writes to the log of Aiko about a conversation (D-298).
///
/// The only text a method takes is the name of the environment. A message, a path, an id or a
/// line of Claude Code cannot be passed here, so it cannot reach the log.
///
/// The twin of ITerminalLog.
public protocol TerminalLog {
    func started(_ environment: String, resumed: Bool)

    func startFailed(_ environment: String)

    func exited(_ environment: String, exitCode: Int, linesRead: Int, linesWritten: Int)

    /// The terminal ended the process itself: the window closed, or the session gave way to another.
    func stopped(_ environment: String, linesRead: Int, linesWritten: Int)
}
