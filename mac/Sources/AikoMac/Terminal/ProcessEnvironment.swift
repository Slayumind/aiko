import AikoKit
import Foundation

/// The environment a program in the terminal starts with. An app started from Finder has only the
/// short PATH of the system, so the PATH of a login shell is read once and given to every program,
/// the way a Terminal window would have it (LoginShellPath).
enum ProcessEnvironment {
    private static let lock = NSLock()
    nonisolated(unsafe) private static var loginPath: String?

    /// The environment of Aiko with the PATH of a login shell and the changes of a plan; a change
    /// with no value takes the variable out.
    static func with(_ changes: [VariableChange]) -> [String] {
        variables(changes).map { "\($0.key)=\($0.value)" }
    }

    /// The same as a dictionary, for a Process.
    static func variables(_ changes: [VariableChange]) -> [String: String] {
        var variables = ProcessInfo.processInfo.environment
        if let path = path() {
            variables["PATH"] = path
        }
        variables["TERM"] = "xterm-256color"
        for change in changes {
            if let value = change.value {
                variables[change.name] = value
            } else {
                variables.removeValue(forKey: change.name)
            }
        }
        return variables
    }

    /// The names in the environment of Aiko itself, for the plans that take leftovers out.
    static var inheritedNames: [String] { Array(ProcessInfo.processInfo.environment.keys) }

    /// The shell of the person, zsh unless they chose another.
    static var shell: String {
        ProcessInfo.processInfo.environment["SHELL"].flatMap { $0.isEmpty ? nil : $0 } ?? "/bin/zsh"
    }

    private static func path() -> String? {
        lock.lock()
        defer { lock.unlock() }
        if let loginPath {
            return loginPath
        }

        let process = Process()
        process.executableURL = URL(fileURLWithPath: shell)
        process.arguments = LoginShellPath.arguments
        let output = Pipe()
        process.standardOutput = output
        process.standardError = FileHandle.nullDevice
        do {
            try process.run()
            process.waitUntilExit()
        } catch {
            TerminalLog.loginShellNotStarted()
            return nil
        }

        let text = String(decoding: output.fileHandleForReading.readDataToEndOfFile(), as: UTF8.self)
        loginPath = LoginShellPath.parse(text)
        return loginPath
    }
}
