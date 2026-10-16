import Foundation

/// What a conversation is doing, for the list of sessions (D-293).
public enum TalkActivity: Sendable, Equatable {
    /// Nothing runs and nothing is new.
    case idle

    case working

    /// A card waits for the person.
    case waiting

    /// The work ended and the person has not looked yet.
    case done

    /// Claude Code stopped in the middle of the work.
    case failed

    case signedOut
}

public enum TalkFailure: Sendable, Equatable {
    case none
    case signedOut
    case crashed
}

public enum StepState: Sendable, Equatable {
    case running
    case done
    case failed
}

public enum RequestState: Sendable, Equatable {
    case open
    case allowed
    case denied
    case answered

    /// The turn ended before the person chose.
    case closed
}

public enum NoticeKind: Sendable, Equatable {
    /// The conversation was made shorter.
    case compacted

    /// The person stopped the turn.
    case interrupted

    /// The turn or Claude Code itself failed. The text says how, when Claude Code said it.
    case failed

    case signedOut

    /// The output of a command that ran without the model.
    case output
}

/// A step of the work in the feed.
public struct FeedStep: Sendable, Equatable {
    public var toolUseId: String
    public var line: StepLine
    public var state: StepState
    public var result: String
    public var images: [StreamImage]
    public var progress: String

    public init(
        toolUseId: String, line: StepLine, state: StepState, result: String, images: [StreamImage], progress: String
    ) {
        self.toolUseId = toolUseId
        self.line = line
        self.state = state
        self.result = result
        self.images = images
        self.progress = progress
    }
}

/// One thing in the feed of a conversation.
public struct FeedItem: Sendable, Equatable {
    public enum Content: Sendable, Equatable {
        /// The attachments are kept only until the message is sent; images stays as the count to show.
        case ownerMessage(text: String, images: Int, queued: Bool, attachments: [AttachedImage])

        /// The markdown of an answer. While it is still being written, streaming is set.
        case answer(text: String, streaming: Bool)

        case step(FeedStep)
        case card(request: RequestCard, state: RequestState)
        case notice(kind: NoticeKind, text: String)
    }

    public let id: Int
    public var content: Content

    public init(id: Int, content: Content) {
        self.id = id
        self.content = content
    }
}

/// A conversation after one more event or one more action, and the lines to write to Claude Code.
public struct Outcome: Sendable, Equatable {
    public let state: Conversation
    public let written: [String]
}

/// One conversation with Claude Code: its feed and what it is doing (D-294, D-304, D-305).
///
/// Nothing here starts a process or draws anything. Every event of the stream and every action of
/// the person gives a new conversation and the lines to write back; the window does the rest.
///
/// What a subagent says and does inside its own work is left out of the feed. Its permission
/// requests are not: they come as cards like any other.
public struct Conversation: Sendable, Equatable {
    public var items: [FeedItem] = []
    public var inTurn = false
    public var tasks = 0
    public var mode: PermissionMode
    public var model = ""
    public var sessionId = ""
    public var unread = false
    public var failure: TalkFailure = .none
    public var compacting = false
    public var limits: [LimitWindow] = []
    public var nextId = 1
    public var answeredInTurn = false
    public var failedInTurn = false
    public var askedModel = ""

    public static func empty(mode: PermissionMode = .manual) -> Conversation {
        Conversation(mode: mode)
    }

    public var activity: TalkActivity {
        if failure == .signedOut { return .signedOut }
        if failure == .crashed { return .failed }
        if hasOpenCard { return .waiting }
        if isBusy { return .working }
        return unread ? .done : .idle
    }

    /// A turn can end while a subagent still works in the background (D-305).
    public var isBusy: Bool { inTurn || tasks > 0 }

    /// The model asked last, or the one Claude Code named when nothing is asked.
    public var latestModel: String { askedModel.isEmpty ? model : askedModel }

    public var hasOpenCard: Bool {
        items.contains { item in
            if case .card(_, .open) = item.content { return true }
            return false
        }
    }

    // MARK: - What Claude Code says

    public func on(_ event: StreamEvent) -> Outcome {
        var next = self
        switch event {
        case .ready(let session, let model, let mode, _):
            next.sessionId = session
            if !model.isEmpty {
                next.model = model
                next.askedModel = ""
            }
            if let mode { next.mode = mode }
        case .textDelta(nil, let text):
            next.stream(text)
        case .assistant(nil, let blocks, let error):
            next.say(blocks, error)
        case .user(nil, let text, let results, _, _):
            // After a change of model Claude Code names its full id at once, before the next turn.
            if let model = ModelName.fromSwitch(text) {
                next.model = model
                next.askedModel = ""
            }
            for result in results {
                next.changeStep(result.toolUseId) { step in
                    step.state = result.isError ? .failed : .done
                    step.result = result.text
                    step.images = result.images
                }
            }
        case .request(let request):
            next.add(.card(request: RequestCard.from(request), state: .open))
        case .reply(_, _, let mode):
            if let mode { next.mode = mode }
        case .status(let activity, let mode):
            if let mode { next.mode = mode }
            next.compacting = activity == "compacting"
        case .compacted:
            next.add(.notice(kind: .compacted, text: ""))
        case .rateLimit(let windows):
            next.limits = windows
        case .tasks(let count):
            next.tasks = count
        case .taskProgress(let toolUseId, let description):
            next.changeStep(toolUseId) { $0.progress = description }
        case .result(let isError, _, let terminalReason, let text):
            return next.ended(isError: isError, terminalReason: terminalReason, text: text)
        default:
            break
        }
        return Outcome(state: next, written: [])
    }

    private mutating func stream(_ text: String) {
        if let last = items.last, case .answer(let written, true) = last.content {
            items[items.count - 1].content = .answer(text: written + text, streaming: true)
        } else {
            add(.answer(text: text, streaming: true))
        }
    }

    private mutating func say(_ blocks: [AssistantBlock], _ error: String?) {
        if let error {
            // Claude Code answered in the place of Claude. Its words are a notice, not an answer.
            let words = blocks.compactMap { block -> String? in
                if case .text(let value) = block { return value }
                return nil
            }.joined(separator: "\n")

            if error == "authentication_failed" {
                add(.notice(kind: .signedOut, text: ""))
                failure = .signedOut
            } else {
                add(.notice(kind: .failed, text: words))
                failedInTurn = true
            }
            return
        }

        for block in blocks {
            switch block {
            case .text(let value):
                answer(value)
            case .toolUse(let id, let name, let input):
                let line = StepLine.forTool(name, input)
                if !line.hidden {
                    add(.step(FeedStep(toolUseId: id, line: line, state: .running, result: "", images: [], progress: "")))
                }
            case .thinking:
                break
            }
        }
    }

    /// The whole text of an answer takes the place of the pieces that came before it.
    private mutating func answer(_ text: String) {
        if let last = items.last, case .answer(_, true) = last.content {
            items[items.count - 1].content = .answer(text: text, streaming: false)
        } else {
            add(.answer(text: text, streaming: false))
        }
        answeredInTurn = true
    }

    private func ended(isError: Bool, terminalReason: String, text: String) -> Outcome {
        var next = self
        next.settle(isError ? .failed : .done)

        // When Claude Code already said in this turn what went wrong, the feed has that notice.
        let stopped = terminalReason.hasPrefix("aborted")
        if isError && stopped {
            next.add(.notice(kind: .interrupted, text: ""))
        } else if isError && !failedInTurn && failure != .signedOut {
            next.add(.notice(kind: .failed, text: text))
        } else if !isError && !answeredInTurn && !text.isEmpty {
            next.add(.notice(kind: .output, text: text))
        }

        next.inTurn = false
        next.answeredInTurn = false
        next.failedInTurn = false
        next.unread = true
        next.compacting = false

        // The queue is kept here, not in Claude Code: the next message goes when the turn is over (D-304).
        if let queued = next.items.first(where: { item in
            if case .ownerMessage(_, _, true, _) = item.content { return true }
            return false
        }) {
            return next.sent(queued)
        }
        return Outcome(state: next, written: [])
    }

    // MARK: - What the person does

    /// A message sent while Claude works, or while a card waits, goes into the queue.
    public func send(_ text: String, images: [AttachedImage]) -> Outcome {
        if RequestCard.trimmed(text).isEmpty && images.isEmpty {
            return Outcome(state: self, written: [])
        }

        let waits = inTurn || hasOpenCard
        var next = self
        next.failure = .none
        next.unread = false
        next.add(.ownerMessage(text: text, images: images.count, queued: true, attachments: images))

        if waits {
            return Outcome(state: next, written: [])
        }
        return next.sent(next.items[next.items.count - 1])
    }

    public func choose(_ requestId: String, _ choice: CardChoice) -> Outcome {
        for (index, item) in items.enumerated() {
            guard case .card(let request, .open) = item.content, request.requestId == requestId else {
                continue
            }

            let state: RequestState
            switch choice {
            case .deny: state = .denied
            case .answers: state = .answered
            default: state = .allowed
            }

            var next = self
            next.items[index].content = .card(request: request, state: state)
            return Outcome(state: next, written: [request.lineFor(choice)])
        }
        return Outcome(state: self, written: [])
    }

    public func interrupt() -> Outcome {
        guard isBusy else {
            return Outcome(state: self, written: [])
        }
        var next = self
        next.nextId += 1
        return Outcome(state: next, written: [HostLine.interrupt("int-\(nextId)")])
    }

    public func setMode(_ mode: PermissionMode) -> Outcome {
        var next = self
        next.mode = mode
        next.nextId += 1
        return Outcome(state: next, written: [HostLine.setMode("mode-\(nextId)", mode)])
    }

    /// The model stays asked until Claude Code names its full id: in its answer to the change,
    /// or in the init of the next start.
    public func setModel(_ model: String) -> Outcome {
        var next = self
        next.askedModel = model
        next.nextId += 1
        return Outcome(state: next, written: [HostLine.setModel("model-\(nextId)", model)])
    }

    public func markRead() -> Conversation {
        var next = self
        next.unread = false
        return next
    }

    /// Claude Code is gone. If it was in the middle of something, that is a failure to show.
    public func processExited() -> Conversation {
        if !isBusy && !hasOpenCard {
            return self
        }

        var next = self
        next.settle(.failed)
        next.add(.notice(kind: .failed, text: ""))
        next.inTurn = false
        next.tasks = 0
        next.answeredInTurn = false
        next.failedInTurn = false
        next.compacting = false
        if next.failure != .signedOut {
            next.failure = .crashed
        }
        return next
    }

    // MARK: - Small steps

    private func sent(_ message: FeedItem) -> Outcome {
        guard case .ownerMessage(let text, let images, _, let attachments) = message.content else {
            return Outcome(state: self, written: [])
        }

        // The message moves to the end of the feed: the answer to it comes right under it, not
        // under whatever was said while it waited.
        var next = self
        next.items.removeAll { $0.id == message.id }
        next.items.append(FeedItem(
            id: message.id,
            content: .ownerMessage(text: text, images: images, queued: false, attachments: [])))
        next.inTurn = true

        return Outcome(state: next, written: [HostLine.userMessage(text, images: attachments)])
    }

    /// Nothing is left half done: an answer is whole, a step has ended, a card nobody answered is closed.
    private mutating func settle(_ running: StepState) {
        for index in items.indices {
            switch items[index].content {
            case .answer(let text, true):
                items[index].content = .answer(text: text, streaming: false)
            case .step(var step) where step.state == .running:
                step.state = running
                items[index].content = .step(step)
            case .card(let request, .open):
                items[index].content = .card(request: request, state: .closed)
            default:
                break
            }
        }
    }

    private mutating func add(_ content: FeedItem.Content) {
        items.append(FeedItem(id: nextId, content: content))
        nextId += 1
    }

    private mutating func changeStep(_ toolUseId: String, _ change: (inout FeedStep) -> Void) {
        for index in items.indices {
            if case .step(var step) = items[index].content, step.toolUseId == toolUseId {
                change(&step)
                items[index].content = .step(step)
            }
        }
    }
}
