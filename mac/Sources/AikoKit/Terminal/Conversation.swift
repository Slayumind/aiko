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

/// The size of a conversation before and after it was made shorter, in tokens.
public struct CompactSize: Sendable, Equatable {
    public let before: Int
    public let after: Int

    public init(before: Int, after: Int) {
        self.before = before
        self.after = after
    }

    /// Both numbers are needed: an older Claude Code names only the size before.
    public static func of(before: Int, after: Int) -> CompactSize? {
        before > 0 && after > 0 ? CompactSize(before: before, after: after) : nil
    }

    /// Tokens in thousands, as the notice shows them: at least 1.
    public static func thousands(_ tokens: Int) -> Int {
        max(1, Int((Double(tokens) / 1000).rounded(.toNearestOrAwayFromZero)))
    }
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
        /// The pictures stay with the message after it is sent, so the feed shows what went.
        case ownerMessage(text: String, queued: Bool, attachments: [AttachedImage])

        /// The markdown of an answer. While it is still being written, streaming is set.
        case answer(text: String, streaming: Bool)

        case step(FeedStep)
        case card(request: RequestCard, state: RequestState)
        /// Size is set only for a conversation made shorter, when Claude Code named both sizes.
        case notice(kind: NoticeKind, text: String, size: CompactSize? = nil)
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
        case .compacted(let before, let after):
            next.add(.notice(kind: .compacted, text: "", size: CompactSize.of(before: before, after: after)))
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
        if let at = writing(), case .answer(let written, _) = items[at].content {
            items[at].content = .answer(text: written + text, streaming: true)
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
        if let at = writing() {
            items[at].content = .answer(text: text, streaming: false)
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
            if case .ownerMessage(_, true, _) = item.content { return true }
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
        next.queue(.ownerMessage(text: text, queued: true, attachments: images))

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
        guard case .ownerMessage(let text, _, let attachments) = message.content else {
            return Outcome(state: self, written: [])
        }

        // The message moves to the end of the feed: the answer to it comes right under it, not
        // under whatever was said while it waited.
        var next = self
        next.items.removeAll { $0.id == message.id }
        next.items.append(FeedItem(
            id: message.id,
            content: .ownerMessage(text: text, queued: false, attachments: attachments)))
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

    /// What Claude Code says goes above the messages that wait in the queue, so they stay at the
    /// end of the feed and an answer is not cut in two (D-304).
    private mutating func add(_ content: FeedItem.Content) {
        items.insert(FeedItem(id: nextId, content: content), at: items.count - queuedAtEnd())
        nextId += 1
    }

    private mutating func queue(_ content: FeedItem.Content) {
        items.append(FeedItem(id: nextId, content: content))
        nextId += 1
    }

    /// The place of the answer Claude is writing now: the last item above the queued messages.
    private func writing() -> Int? {
        let at = items.count - queuedAtEnd() - 1
        guard at >= 0, case .answer(_, true) = items[at].content else { return nil }
        return at
    }

    private func queuedAtEnd() -> Int {
        var count = 0
        for item in items.reversed() {
            guard case .ownerMessage(_, true, _) = item.content else { break }
            count += 1
        }
        return count
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
