import Foundation

/// What one conversation needs to start Claude Code. started says that Claude Code already knows
/// the conversation: it was opened before, in this run of Aiko or in an earlier one.
public struct RunnerSetup: Sendable, Equatable {
    public let executable: String
    public let workingDirectory: String
    public let configDirectory: String
    public let userProfile: String
    public let environmentName: String
    public let sessionId: String
    public let started: Bool
    public let mode: PermissionMode
    public let model: String?
    public let inheritedVariables: [String]

    public init(
        executable: String,
        workingDirectory: String,
        configDirectory: String,
        userProfile: String,
        environmentName: String,
        sessionId: String,
        started: Bool,
        mode: PermissionMode,
        model: String?,
        inheritedVariables: [String]
    ) {
        self.executable = executable
        self.workingDirectory = workingDirectory
        self.configDirectory = configDirectory
        self.userProfile = userProfile
        self.environmentName = environmentName
        self.sessionId = sessionId
        self.started = started
        self.mode = mode
        self.model = model
        self.inheritedVariables = inheritedVariables
    }
}

/// Runs a command of "!" in a folder (D-343). ended may be called on a thread of its own, with all
/// the output, errors included, and the exit code.
public protocol Shell: Sendable {
    func run(_ command: String, folder: String, ended: @escaping @Sendable (String, Int) -> Void)
}

/// Runs one conversation: keeps its state, starts Claude Code when the first message is sent and
/// passes lines both ways (D-304).
///
/// Everything here happens on one thread, the one of the window. A process calls back on a thread
/// of its own; those calls only fill a list, and post brings the work home. Lines that came
/// together are taken together, so the window hears about them once.
///
/// Sendable by hand: the list is behind a lock, and the rest is touched on the window thread only.
public final class ConversationRunner: @unchecked Sendable {
    /// A line of a process, or its exit when the line is nil.
    private struct Arrival {
        let run: Int
        let line: String?
        let exitCode: Int
    }

    private let platform: PlatformConventions
    private let setup: RunnerSetup
    private let newProcess: () -> ConversationProcess
    private let post: @Sendable (@escaping @Sendable () -> Void) -> Void
    private let log: TerminalLog
    private let shell: Shell?

    private let gate = NSLock()
    private var arrived: [Arrival] = []
    private var posted = false
    private var taking = false

    private var process: ConversationProcess?

    /// Grows with every start and every stop: what an older process still says is dropped.
    private var run = 0

    private var known: Bool

    /// Whether the process that runs now has said it is ready for a turn.
    private var ready = false
    private var linesRead = 0
    private var linesWritten = 0

    public private(set) var state: Conversation

    /// Called on the thread of the window after the state has changed.
    public var changed: (() -> Void)?

    public var isRunning: Bool { process != nil }

    public init(
        _ platform: PlatformConventions,
        _ setup: RunnerSetup,
        newProcess: @escaping () -> ConversationProcess,
        post: @escaping @Sendable (@escaping @Sendable () -> Void) -> Void,
        log: TerminalLog,
        shell: Shell? = nil
    ) {
        self.platform = platform
        self.setup = setup
        self.newProcess = newProcess
        self.post = post
        self.log = log
        self.shell = shell
        known = setup.started
        state = Conversation.empty(mode: setup.mode)
    }

    public func send(_ text: String, images: [AttachedImage]) {
        apply(state.send(text, images: images), startIfNeeded: true)
    }

    public func choose(_ requestId: String, _ choice: CardChoice) {
        apply(state.choose(requestId, choice), startIfNeeded: false)
    }

    public func interrupt() {
        apply(state.interrupt(), startIfNeeded: false)
    }

    /// Without a process nothing is written: the next start carries the mode in its arguments.
    public func setMode(_ mode: PermissionMode) {
        apply(state.setMode(mode), startIfNeeded: false)
    }

    /// Without a process nothing is written: the next start carries the model in its arguments.
    public func setModel(_ model: String) {
        apply(state.setModel(model), startIfNeeded: false)
    }

    public func markRead() {
        state = state.markRead()
        changed?()
    }

    /// Ends Claude Code. The conversation stays and can go on later with a new process.
    public func close() {
        guard let running = process else {
            return
        }

        run += 1
        running.stop()
        process = nil
        log.stopped(setup.environmentName, linesRead: linesRead, linesWritten: linesWritten)

        state = state.processExited()
        changed?()
    }

    private func apply(_ outcome: Outcome, startIfNeeded: Bool) {
        // The state first: a scripted process answers before write returns.
        state = outcome.state

        if !outcome.written.isEmpty && process == nil && startIfNeeded && !start() {
            state = state.processExited()
        }

        write(outcome.written)
        runShell(outcome.shell)
        changed?()
    }

    /// The command of "!" runs in the folder of the session; its end comes home like a line does.
    private func runShell(_ shellRun: ShellRun?) {
        guard let shellRun else {
            return
        }

        guard let shell else {
            post { [self] in apply(state.shellEnded(shellRun.itemId, output: "", exitCode: -1), startIfNeeded: true) }
            return
        }

        shell.run(shellRun.command, folder: setup.workingDirectory) { [self] output, code in
            post { [self] in apply(state.shellEnded(shellRun.itemId, output: output, exitCode: code), startIfNeeded: true) }
        }
    }

    private func start() -> Bool {
        let plan = ClaudeLaunchPlan.forRequest(
            platform,
            LaunchRequest(
                configDirectory: setup.configDirectory,
                userProfile: setup.userProfile,
                environmentName: setup.environmentName,
                sessionId: setup.sessionId,
                started: known,
                mode: state.mode,
                model: state.latestModel.isEmpty ? setup.model : state.latestModel),
            inheritedVariables: setup.inheritedVariables)

        let started = newProcess()
        run += 1
        let mine = run
        linesRead = 0
        linesWritten = 0
        ready = false

        // A scripted process answers from inside its start. What it says waits until the start
        // is over and logged, as it would with a real process on a thread of its own.
        let resumed = known
        process = started
        taking = true
        let ok = started.start(
            ProcessStart(
                executable: setup.executable,
                arguments: plan.arguments,
                variables: plan.variables,
                workingDirectory: setup.workingDirectory),
            onLine: { [weak self] line in self?.arrive(Arrival(run: mine, line: line, exitCode: 0)) },
            onExit: { [weak self] code in self?.arrive(Arrival(run: mine, line: nil, exitCode: code)) })
        taking = false

        guard ok else {
            process = nil
            log.startFailed(setup.environmentName)
            return false
        }

        log.started(setup.environmentName, resumed: resumed)
        take()
        return true
    }

    private func write(_ lines: [String]) {
        for line in lines {
            guard let running = process else {
                return
            }

            // Counted first: a scripted process answers, and may even exit, before write returns.
            linesWritten += 1
            if !running.write(line) {
                linesWritten -= 1
            }
        }
    }

    /// Called on the thread of the process.
    private func arrive(_ arrival: Arrival) {
        gate.lock()
        arrived.append(arrival)
        let alreadyPosted = posted
        posted = true
        gate.unlock()

        if !alreadyPosted {
            post { [weak self] in self?.take() }
        }
    }

    private func take() {
        // A scripted process answers while its line is being taken. The loop below picks that up.
        if taking {
            return
        }

        taking = true
        var anything = false
        while true {
            gate.lock()
            let batch = arrived
            arrived = []
            if batch.isEmpty {
                posted = false
            }
            gate.unlock()

            if batch.isEmpty {
                break
            }

            for arrival in batch where arrival.run == run {
                take(arrival)
                anything = true
            }
        }
        taking = false

        if anything {
            changed?()
        }
    }

    private func take(_ arrival: Arrival) {
        guard let line = arrival.line else {
            process = nil
            log.exited(
                setup.environmentName, exitCode: arrival.exitCode, linesRead: linesRead, linesWritten: linesWritten)

            // Claude Code refuses a new id it has on disk and a resumed one it has not, and in
            // both cases leaves before it is ready. Which of the two happened is not known when
            // a process died early, so the next start tries the other way.
            if !ready {
                known.toggle()
            }

            state = state.processExited()
            return
        }

        linesRead += 1
        let event = StreamEvent.fromLine(line)

        // From here on Claude Code has the conversation on disk, and the next start resumes it.
        if case .ready = event {
            ready = true
            known = true
        }

        let outcome = state.on(event)
        state = outcome.state
        write(outcome.written)
        runShell(outcome.shell)
    }
}
