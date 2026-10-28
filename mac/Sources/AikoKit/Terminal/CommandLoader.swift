import Foundation

/// What the short Claude Code of one environment needs to list its commands.
public struct CommandSetup: Sendable, Equatable {
    public let executable: String
    public let workingDirectory: String
    public let configDirectory: String
    public let userProfile: String
    public let inheritedVariables: [String]

    public init(
        executable: String,
        workingDirectory: String,
        configDirectory: String,
        userProfile: String,
        inheritedVariables: [String]
    ) {
        self.executable = executable
        self.workingDirectory = workingDirectory
        self.configDirectory = configDirectory
        self.userProfile = userProfile
        self.inheritedVariables = inheritedVariables
    }
}

/// What Claude Code offers in one environment: the commands of the menu of "/" and the models.
public struct ClaudeOffer: Sendable, Equatable {
    public static let none = ClaudeOffer(commands: [], models: [])

    public let commands: [SlashCommand]
    public let models: [ClaudeModel]

    public init(commands: [SlashCommand], models: [ClaudeModel]) {
        self.commands = commands
        self.models = models
    }
}

/// Asks a short Claude Code for the commands and the models it knows (AIKO-330, AIKO-387).
///
/// The process starts, gets "initialize", names its commands and models and is asked to leave.
/// One answer per environment is enough for the life of the window: skills, plugins and models
/// rarely change while it is open. done is called once, on the thread post brings the work to,
/// with the offer, or with an empty one when Claude Code did not start or left without one.
///
/// Sendable by hand: the one flag it keeps is behind a lock.
public final class CommandLoader: @unchecked Sendable {
    private static let requestId = "commands-1"

    private let platform: PlatformConventions
    private let newProcess: () -> ConversationProcess
    private let post: @Sendable (@escaping @Sendable () -> Void) -> Void

    public init(
        _ platform: PlatformConventions,
        newProcess: @escaping () -> ConversationProcess,
        post: @escaping @Sendable (@escaping @Sendable () -> Void) -> Void
    ) {
        self.platform = platform
        self.newProcess = newProcess
        self.post = post
    }

    public func load(_ setup: CommandSetup, done: @escaping @Sendable (ClaudeOffer) -> Void) {
        let plan = ClaudeLaunchPlan.forCommands(
            platform,
            configDirectory: setup.configDirectory,
            userProfile: setup.userProfile,
            inheritedVariables: setup.inheritedVariables)
        let process = newProcess()
        let once = Once()
        let post = self.post

        let answer: @Sendable (ClaudeOffer) -> Void = { offer in
            guard once.take() else { return }
            process.stop()
            post { done(offer) }
        }

        let started = process.start(
            ProcessStart(
                executable: setup.executable,
                arguments: plan.arguments,
                variables: plan.variables,
                workingDirectory: setup.workingDirectory),
            onLine: { line in
                if case .commands(CommandLoader.requestId, let list, let models) = StreamEvent.fromLine(line) {
                    answer(ClaudeOffer(commands: list, models: models))
                }
            },
            onExit: { _ in answer(.none) })

        if !started || !process.write(HostLine.initialize(CommandLoader.requestId)) {
            answer(.none)
            return
        }

        // Claude Code answers in under a second. One that hangs on its start is stopped after
        // this, so no process waits until Aiko quits.
        DispatchQueue.global().asyncAfter(deadline: .now() + 20) { answer(.none) }
    }

    /// True the first time only.
    private final class Once: @unchecked Sendable {
        private let lock = NSLock()
        private var taken = false

        func take() -> Bool {
            lock.lock()
            defer { lock.unlock() }
            if taken { return false }
            taken = true
            return true
        }
    }
}
