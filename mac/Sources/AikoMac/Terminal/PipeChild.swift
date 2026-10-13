import Foundation

/// The other end of --try-pipe: Aiko started as a child of itself, in the place of Claude Code.
///
/// It sends every line back as it came. Three lines mean something else: "spawn" starts a child
/// of its own and answers with both process ids, "variables" answers with two variables of its
/// environment, and the end of the input is ignored, so only being ended from outside stops it.
///
/// The twin of Terminal/PipeChild.cs.
enum PipeChild {
    static let argument = "--pipe-child"

    /// The child of the child. It does nothing and never leaves on its own.
    static let idle = "idle"

    static let setVariable = "AIKO_PIPE_SET"

    static let removedVariable = "AIKO_PIPE_REMOVED"

    static var ownPath: String {
        Bundle.main.executablePath ?? CommandLine.arguments[0]
    }

    static func run(_ arguments: [String]) -> Never {
        if arguments.contains(idle) {
            sleepForever()
        }

        let environment = ProcessInfo.processInfo.environment
        while let line = readLine(strippingNewline: true) {
            let answer: String
            switch line {
            case "spawn":
                answer = "\(ProcessInfo.processInfo.processIdentifier) \(spawn())"
            case "variables":
                answer = "\(environment[setVariable] ?? "")|\(environment[removedVariable] ?? "")"
            default:
                answer = line
            }
            FileHandle.standardOutput.write(Data((answer + "\n").utf8))
        }

        sleepForever()
    }

    private static func spawn() -> Int32 {
        let child = Process()
        child.executableURL = URL(fileURLWithPath: ownPath)
        child.arguments = [argument, idle]
        child.standardInput = FileHandle.nullDevice
        child.standardOutput = FileHandle.nullDevice
        child.standardError = FileHandle.nullDevice

        do {
            try child.run()
            return child.processIdentifier
        } catch {
            return 0
        }
    }

    private static func sleepForever() -> Never {
        while true {
            sleep(3600)
        }
    }
}
