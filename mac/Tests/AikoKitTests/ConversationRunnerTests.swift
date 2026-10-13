import Foundation
import Testing

@testable import AikoKit

/// When Claude Code is started and what is written to it are tables, and they live in
/// spec/cases/conversation-runner; both cores read them. What is left here is the order of things
/// between the thread of a process and the thread of the window.
struct ConversationRunnerTests {
    static let ready = #"{"type":"system","subtype":"init","session_id":"s-1","model":"claude-opus-5-5","permissionMode":"default"}"#
    static let hello = #"{"type":"assistant","message":{"content":[{"type":"text","text":"Hello."}]},"parent_tool_use_id":null}"#
    static let done = #"{"type":"result","subtype":"success","is_error":false,"terminal_reason":"completed","result":"Hello."}"#

    /// A process the test speaks for, line by line.
    private final class HandProcess: ConversationProcess {
        var say: (String) -> Void = { _ in }
        var exit: (Int) -> Void = { _ in }
        var stopped = false

        func start(
            _ start: ProcessStart,
            onLine: @escaping @Sendable (String) -> Void,
            onExit: @escaping @Sendable (Int) -> Void
        ) -> Bool {
            say = onLine
            exit = onExit
            return true
        }

        func write(_ line: String) -> Bool {
            !stopped
        }

        func stop() {
            stopped = true
        }
    }

    private struct NoLog: TerminalLog {
        func started(_ environment: String, resumed: Bool) {}
        func startFailed(_ environment: String) {}
        func exited(_ environment: String, exitCode: Int, linesRead: Int, linesWritten: Int) {}
        func stopped(_ environment: String, linesRead: Int, linesWritten: Int) {}
    }

    /// The work for the window thread, kept in a list to be done by hand. A test has one thread.
    private final class Posted: @unchecked Sendable {
        var work: [@Sendable () -> Void] = []

        func doAll() {
            for one in work {
                one()
            }
        }
    }

    private final class Processes {
        var made: [HandProcess] = []

        func make() -> ConversationProcess {
            made.append(HandProcess())
            return made[made.count - 1]
        }
    }

    private static func made() -> (ConversationRunner, Processes, Posted) {
        let processes = Processes()
        let posted = Posted()
        let runner = ConversationRunner(
            .macOS,
            RunnerSetup(
                executable: "claude", workingDirectory: "/work", configDirectory: "/accounts/.claude-work",
                userProfile: "/home/someone", environmentName: "Work", sessionId: "id-1", started: false,
                mode: .manual, model: nil, inheritedVariables: []),
            newProcess: processes.make,
            post: { posted.work.append($0) },
            log: NoLog())

        return (runner, processes, posted)
    }

    private static func hasAnswer(_ runner: ConversationRunner) -> Bool {
        runner.state.items.contains { item in
            if case .answer = item.content { return true }
            return false
        }
    }

    @Test
    func linesThatCameTogetherReachTheWindowAsOneChange() {
        let (runner, processes, posted) = Self.made()
        runner.send("hi", images: [])
        var changes = 0
        runner.changed = { changes += 1 }

        processes.made[0].say(Self.ready)
        processes.made[0].say(Self.hello)
        processes.made[0].say(Self.done)

        #expect(posted.work.count == 1)
        #expect(runner.state.activity == .working)

        posted.work[0]()

        #expect(changes == 1)
        #expect(runner.state.activity == .done)
    }

    @Test
    func aLineAfterTheLastChangeAsksForTheWindowThreadAgain() {
        let (runner, processes, posted) = Self.made()
        runner.send("hi", images: [])

        processes.made[0].say(Self.ready)
        posted.work[0]()
        processes.made[0].say(Self.hello)

        #expect(posted.work.count == 2)
    }

    @Test
    func whatAStoppedProcessStillSaysIsDropped() {
        let (runner, processes, posted) = Self.made()
        runner.send("hi", images: [])
        processes.made[0].say(Self.ready)

        runner.close()
        processes.made[0].say(Self.hello)
        processes.made[0].exit(0)
        posted.doAll()

        #expect(processes.made[0].stopped)
        #expect(!runner.isRunning)
        #expect(!Self.hasAnswer(runner))
    }

    @Test
    func theExitOfAnOlderProcessDoesNotEndTheNewOne() {
        let (runner, processes, posted) = Self.made()
        runner.send("one", images: [])
        runner.close()
        runner.send("two", images: [])

        processes.made[0].exit(1)
        posted.doAll()

        #expect(processes.made.count == 2)
        #expect(runner.isRunning)
        #expect(runner.state.activity == .working)
    }
}
