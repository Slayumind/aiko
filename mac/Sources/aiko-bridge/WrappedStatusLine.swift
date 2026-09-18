import AikoKit
import Foundation

/// The status line the user had before Aiko. It gets the same input and its output is printed as
/// is, so nothing the user built is lost.
///
/// It runs through the shell Claude Code itself would have used, which on macOS is always
/// `/bin/zsh -lc`. A line written for zsh and run through another shell prints nothing, and the
/// promise in the readme that an existing status line keeps working would be false.
enum WrappedStatusLine {
    /// Claude Code is waiting for this output, so a command that hangs is dropped, as on Windows.
    static let waitSeconds = 2.0

    static func output(_ command: String, input: String) -> String {
        let call = ClaudeShellLookup.callFor(.macOS, nil, command)

        let process = Process()
        process.executableURL = URL(fileURLWithPath: call.fileName)
        process.arguments = call.arguments

        let fromCommand = Pipe()
        let toCommand = Pipe()
        process.standardOutput = fromCommand
        process.standardInput = toCommand

        guard (try? process.run()) != nil else {
            return ""
        }

        // The input goes over on another thread: a command that never reads it would otherwise fill
        // the pipe and stop the bridge.
        let writer = toCommand.fileHandleForWriting
        let payload = Data(input.utf8)
        DispatchQueue.global().async {
            try? writer.write(contentsOf: payload)
            try? writer.close()
        }

        let text = read(fromCommand.fileHandleForReading, until: Date().addingTimeInterval(waitSeconds))

        if process.isRunning {
            // Only this process, not the tree below it: the shell shares our process group, and
            // killing the group would kill the bridge with it.
            kill(process.processIdentifier, SIGKILL)
        }
        process.waitUntilExit()

        return text
    }

    /// Everything the command printed before the deadline. `readDataToEndOfFile` would wait for the
    /// pipe to close, and a status line that hangs must not hang Claude Code.
    private static func read(_ handle: FileHandle, until deadline: Date) -> String {
        var data = Data()
        var buffer = [UInt8](repeating: 0, count: 8192)

        while true {
            let left = deadline.timeIntervalSinceNow
            if left <= 0 {
                break
            }

            var watched = pollfd(fd: handle.fileDescriptor, events: Int16(POLLIN), revents: 0)
            if poll(&watched, 1, Int32(left * 1000)) <= 0 {
                break
            }

            let count = Foundation.read(handle.fileDescriptor, &buffer, buffer.count)
            if count <= 0 {
                break
            }
            data.append(contentsOf: buffer[0..<count])
        }

        return String(decoding: data, as: UTF8.self)
    }
}
