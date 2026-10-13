import AikoKit
import Darwin
import Foundation

/// Claude Code as a child process with pipes on both ends (D-297).
///
/// The output is read on a queue of its own, so a long answer never waits for the window and the
/// window never waits for an answer. The exit is reported after the last line: when the process
/// has ended and its output has closed, whichever comes later.
///
/// The twin of Terminal/ClaudeProcess.cs. Windows puts the process into a job; macOS has no such
/// thing, so stopping walks the children of the process and ends each of them.
final class ClaudeProcess: ConversationProcess, @unchecked Sendable {
    /// How long Claude Code gets to leave on its own after its input was closed.
    private static let grace: TimeInterval = 3

    /// Writing to a process that is gone would end Aiko with SIGPIPE. Ignored, the write fails
    /// with an error instead, and that error is handled.
    private static let ignoresBrokenPipes: Void = {
        signal(SIGPIPE, SIG_IGN)
    }()

    private let gate = NSLock()
    private var process: Process?
    private var input: FileHandle?
    private var gone = false

    func start(
        _ start: ProcessStart,
        onLine: @escaping @Sendable (String) -> Void,
        onExit: @escaping @Sendable (Int) -> Void
    ) -> Bool {
        Self.ignoresBrokenPipes

        let process = Process()
        process.executableURL = URL(fileURLWithPath: start.executable)
        process.arguments = start.arguments
        process.currentDirectoryURL = URL(fileURLWithPath: start.workingDirectory)

        var environment = ProcessInfo.processInfo.environment
        for variable in start.variables {
            environment[variable.name] = variable.value
        }
        process.environment = environment

        let toProcess = Pipe()
        let fromProcess = Pipe()
        process.standardInput = toProcess
        process.standardOutput = fromProcess

        // What Claude Code writes to its error stream is for a person at a console. A pipe nobody
        // drains would fill up and stop the process, so it goes nowhere.
        process.standardError = FileHandle.nullDevice

        // Two things must have happened before the exit is reported.
        let ended = DispatchGroup()
        ended.enter()
        ended.enter()

        let lines = Lines(onLine)
        fromProcess.fileHandleForReading.readabilityHandler = { handle in
            let bytes = handle.availableData
            if bytes.isEmpty {
                handle.readabilityHandler = nil
                lines.close()
                ended.leave()
            } else {
                lines.take(bytes)
            }
        }
        process.terminationHandler = { _ in ended.leave() }

        do {
            try process.run()
        } catch {
            fromProcess.fileHandleForReading.readabilityHandler = nil
            return false
        }

        gate.lock()
        self.process = process
        input = toProcess.fileHandleForWriting
        gate.unlock()

        ended.notify(queue: .global()) { [self] in
            gate.lock()
            gone = true
            try? input?.close()
            gate.unlock()

            onExit(Int(process.terminationStatus))
        }
        return true
    }

    func write(_ line: String) -> Bool {
        gate.lock()
        defer { gate.unlock() }

        guard !gone, let input else {
            return false
        }

        do {
            try input.write(contentsOf: Data((line + "\n").utf8))
            return true
        } catch {
            return false
        }
    }

    func stop() {
        gate.lock()
        let running = gone ? nil : process
        try? input?.close()
        input = nil
        gate.unlock()

        guard let running else {
            return
        }

        // One wait, once, for a process that was asked to leave. Nothing ticks after it.
        DispatchQueue.global().asyncAfter(deadline: .now() + Self.grace) {
            if running.isRunning {
                ProcessFamily.end(running.processIdentifier)
            }
        }
    }

    /// The lines of the output, cut on the queue the pipe delivers on.
    private final class Lines: @unchecked Sendable {
        private var buffer = LineBuffer()
        private let onLine: @Sendable (String) -> Void

        init(_ onLine: @escaping @Sendable (String) -> Void) {
            self.onLine = onLine
        }

        func take(_ bytes: Data) {
            for line in buffer.take(bytes) {
                onLine(line)
            }
        }

        func close() {
            if let last = buffer.rest() {
                onLine(last)
            }
        }
    }
}

/// A process with everything it started.
enum ProcessFamily {
    /// Ends the process and every process below it. The children are found first: once their
    /// parent is gone they belong to nobody and cannot be found this way.
    static func end(_ root: pid_t) {
        for one in below(root) + [root] {
            kill(one, SIGKILL)
        }
    }

    /// Every process below this one, the deepest first.
    static func below(_ parent: pid_t) -> [pid_t] {
        var found: [pid_t] = []
        for child in children(of: parent) {
            found += below(child) + [child]
        }
        return found
    }

    private static func children(of parent: pid_t) -> [pid_t] {
        var ids = [pid_t](repeating: 0, count: 512)
        let size = Int32(ids.count * MemoryLayout<pid_t>.stride)
        guard proc_listchildpids(parent, &ids, size) > 0 else {
            return []
        }

        // The call has answered with a count on some systems and with a size on others. The
        // list starts as zeros, so what was filled is simply what is not zero.
        return ids.filter { $0 > 0 && $0 != parent }
    }
}
