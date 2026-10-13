import Foundation

/// One answer of a scripted Claude Code: the lines it writes after something was written to it.
///
/// after names what it waits for: "start" (the process was started), "user" (a message),
/// "response" (an answer to a request), "interrupt" or "mode". With fails the start does not
/// happen. With exit the process ends after its lines.
public struct ScriptedReply: Sendable, Equatable {
    public let after: String
    public let lines: [String]
    public let exit: Int?
    public let fails: Bool

    public init(after: String, lines: [String], exit: Int?, fails: Bool) {
        self.after = after
        self.lines = lines
        self.exit = exit
        self.fails = fails
    }
}

/// Claude Code played from a list, for the checks that must not start the real one. It keeps its
/// place in the list across processes, so a script can end a process and go on in the next.
///
/// The twin of ScriptedClaude.cs.
public final class ScriptedClaude {
    private let script: [ScriptedReply]
    private var next = 0

    /// Every start that was asked for, also the ones that failed.
    public private(set) var starts: [ProcessStart] = []

    /// Every line written to a process that was alive.
    public private(set) var written: [String] = []

    public init(_ script: [ScriptedReply]) {
        self.script = script
    }

    public func newProcess() -> ConversationProcess {
        Process(self)
    }

    private func awaited(_ kind: String) -> ScriptedReply? {
        guard next < script.count, script[next].after == kind else {
            return nil
        }
        next += 1
        return script[next - 1]
    }

    private static func kindOf(_ line: String) -> String {
        guard let root = JsonNode.parse(line)?.objectValue else {
            return "other"
        }

        switch root["type"]?.stringValue {
        case "user": return "user"
        case "control_response": return "response"
        case "control_request":
            guard let request = root["request"]?.objectValue else {
                return "other"
            }
            return request["subtype"]?.stringValue == "interrupt" ? "interrupt" : "mode"
        default: return "other"
        }
    }

    private final class Process: ConversationProcess {
        private let claude: ScriptedClaude
        private var onLine: ((String) -> Void)?
        private var onExit: ((Int) -> Void)?
        private var alive = false

        init(_ claude: ScriptedClaude) {
            self.claude = claude
        }

        func start(
            _ start: ProcessStart,
            onLine: @escaping @Sendable (String) -> Void,
            onExit: @escaping @Sendable (Int) -> Void
        ) -> Bool {
            claude.starts.append(start)

            let reply = claude.awaited("start")
            if reply?.fails == true {
                return false
            }

            self.onLine = onLine
            self.onExit = onExit
            alive = true
            play(reply)
            return true
        }

        func write(_ line: String) -> Bool {
            guard alive else {
                return false
            }

            claude.written.append(line)
            play(claude.awaited(ScriptedClaude.kindOf(line)))
            return true
        }

        func stop() {
            alive = false
        }

        private func play(_ reply: ScriptedReply?) {
            guard let reply else {
                return
            }

            for line in reply.lines {
                onLine?(line)
            }

            if let code = reply.exit {
                alive = false
                onExit?(code)
            }
        }
    }
}
