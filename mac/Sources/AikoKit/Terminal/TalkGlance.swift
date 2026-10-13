import Foundation

/// What the second line of a session in the list is about.
public enum GlanceLine: Sendable, Equatable {
    /// Nothing to say.
    case none

    /// A permission is asked for: kind, label and text name the step.
    case request

    /// A question with choices waits for an answer. The text is its header.
    case question

    /// A plan waits to be approved.
    case plan

    /// The step that runs now.
    case step

    /// The first line of the last answer.
    case answer

    /// Claude Code ended in the middle of this step.
    case stoppedAt

    /// The environment has no sign-in.
    case signIn
}

/// One conversation as the list of sessions sees it: what it does and one line about it (D-299).
///
/// The words of the line are strings of the interface; here are only its parts.
///
/// The twin of TalkGlance.cs.
public struct TalkGlance: Sendable, Equatable {
    /// The longest text of a line, in UTF-16 units. A row shows one line and cuts the rest itself.
    public static let maxText = 120

    public let activity: TalkActivity
    public let line: GlanceLine
    public let kind: StepKind
    public let label: String
    public let text: String

    public init(activity: TalkActivity, line: GlanceLine, kind: StepKind, label: String, text: String) {
        self.activity = activity
        self.line = line
        self.kind = kind
        self.label = label
        self.text = text
    }

    public static func of(_ talk: Conversation) -> TalkGlance {
        let activity = talk.activity
        switch activity {
        case .signedOut:
            return plain(activity, .signIn)
        case .failed:
            guard let step = lastStep(talk, { _ in true }) else {
                return plain(activity, .none)
            }
            return about(activity, .stoppedAt, step.line)
        case .waiting:
            return asked(talk, activity)
        case .working:
            guard let step = lastStep(talk, { $0.state == .running }) else {
                return plain(activity, .none)
            }
            return about(activity, .step, step.line)
        default:
            return said(talk, activity)
        }
    }

    private static func asked(_ talk: Conversation, _ activity: TalkActivity) -> TalkGlance {
        var open: RequestCard?
        for item in talk.items {
            if case .card(let request, .open) = item.content {
                open = request
            }
        }
        guard let card = open else {
            return plain(activity, .none)
        }

        switch card.kind {
        case .question:
            return TalkGlance(activity: activity, line: .question, kind: .other, label: "", text: cut(card.questions.first?.header ?? ""))
        case .plan:
            return plain(activity, .plan)
        default:
            return about(activity, .request, card.step)
        }
    }

    private static func said(_ talk: Conversation, _ activity: TalkActivity) -> TalkGlance {
        var answer: String?
        for item in talk.items {
            if case .answer(let text, _) = item.content {
                answer = text
            }
        }

        let line = StepLine.lines(answer ?? "")
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .first { !$0.isEmpty } ?? ""
        return line.isEmpty
            ? plain(activity, .none)
            : TalkGlance(activity: activity, line: .answer, kind: .other, label: "", text: cut(line))
    }

    private static func lastStep(_ talk: Conversation, _ wanted: (FeedStep) -> Bool) -> FeedStep? {
        var last: FeedStep?
        for item in talk.items {
            if case .step(let step) = item.content, !step.line.hidden, wanted(step) {
                last = step
            }
        }
        return last
    }

    private static func about(_ activity: TalkActivity, _ line: GlanceLine, _ step: StepLine) -> TalkGlance {
        TalkGlance(activity: activity, line: line, kind: step.kind, label: step.label, text: cut(step.subject))
    }

    private static func plain(_ activity: TalkActivity, _ line: GlanceLine) -> TalkGlance {
        TalkGlance(activity: activity, line: line, kind: .other, label: "", text: "")
    }

    /// Never cuts a letter of two units in half.
    private static func cut(_ text: String) -> String {
        let units = Array(text.utf16)
        if units.count <= maxText {
            return text
        }

        let end = UTF16.isLeadSurrogate(units[maxText - 1]) ? maxText - 1 : maxText
        return String(decoding: units[..<end], as: UTF16.self)
    }
}
