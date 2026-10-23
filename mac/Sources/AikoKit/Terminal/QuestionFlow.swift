import Foundation

/// A key the card of questions takes while it has the keyboard (D-330). The twin of
/// Terminal/QuestionFlow.cs.
public enum QuestionKey: String, Sendable, Equatable {
    /// The arrows move over the options as they stand on the card: up and down between the lines
    /// of the grid, left and right one option in reading order.
    case up
    case down
    case left
    case right
    case space
    case enter
    case escape
}

/// What the card does after a key or a click.
public enum QuestionOutcome: String, Sendable, Equatable {
    case none
    /// Every question has its answer: they go to Claude Code.
    case answer
    /// The person said no to the questions.
    case deny
    /// The row of the person's own words was chosen: the field for them takes the keyboard.
    case writeOwn
}

/// How a step of the strip at the top of the card looks.
public enum QuestionMark: String, Sendable, Equatable {
    case ahead
    case open
    case answered
}

/// One state of the card and what it asks the window to do.
public struct QuestionStep: Sendable, Equatable {
    public let flow: QuestionFlow
    public let outcome: QuestionOutcome

    public init(_ flow: QuestionFlow, _ outcome: QuestionOutcome = .none) {
        self.flow = flow
        self.outcome = outcome
    }
}

/// A card of questions while the person answers it (D-330, AIKO-327): one question at a time, the
/// strip of steps above it, Back and Next below. Every question has one more row after its options,
/// the person's own words ("Other"), as in the console of Claude Code.
public struct QuestionFlow: Sendable, Equatable {
    public let questions: [CardQuestion]
    /// The open question.
    public private(set) var at: Int
    /// The row the keyboard stands on in the open question. Rows are the options, then Other.
    public private(set) var focus: Int
    /// The rows picked in each question.
    public private(set) var picked: [[Int]]
    /// The person's own words in each question.
    public private(set) var own: [String]

    public static func start(_ questions: [CardQuestion]) -> QuestionFlow {
        QuestionFlow(
            questions: questions, at: 0, focus: 0,
            picked: questions.map { _ in [] }, own: questions.map { _ in "" })
    }

    public var open: CardQuestion { questions[at] }

    /// The row of the person's own words in the open question.
    public var ownRow: Int { open.options.count }

    public var isLast: Bool { at == questions.count - 1 }

    /// The preview of the focused option, or of the picked one when the keyboard is on Other.
    public var preview: String? {
        let options = open.options
        let row = focus < options.count ? focus : (picked[at].first { $0 < options.count } ?? -1)
        return row >= 0 && !options[row].preview.isEmpty ? options[row].preview : nil
    }

    public func isPicked(_ row: Int) -> Bool { picked[at].contains(row) }

    /// A question is answered when a row is picked, and Other has words when it is picked.
    public func isAnswered(_ question: Int) -> Bool {
        !picked[question].isEmpty
            && (!picked[question].contains(questions[question].options.count)
                || !own[question].trimmingCharacters(in: .whitespaces).isEmpty)
    }

    public func mark(_ question: Int) -> QuestionMark {
        question == at ? .open : isAnswered(question) ? .answered : .ahead
    }

    /// The labels picked in each question, with the person's own words in the place of Other.
    public func answers() -> [[String]] {
        questions.enumerated().map { index, question in
            picked[index].sorted()
                .map { $0 < question.options.count ? question.options[$0].label : own[index].trimmingCharacters(in: .whitespaces) }
                .filter { !$0.isEmpty }
        }
    }

    /// columns: how many options stand side by side on the card now, two or one in a narrow window.
    public func press(_ key: QuestionKey, columns: Int = 1) -> QuestionStep {
        switch key {
        case .up: return QuestionStep(with { $0.focus = focus - columns >= 0 ? focus - columns : focus })
        case .down: return QuestionStep(with { $0.focus = below(columns) })
        case .left: return QuestionStep(with { $0.focus = max(0, focus - 1) })
        case .right: return QuestionStep(with { $0.focus = min(ownRow, focus + 1) })
        case .space: return choose(focus)
        case .enter: return enter()
        case .escape: return QuestionStep(self, .deny)
        }
    }

    /// A digit from 1: the row with that number, as a click on it.
    public func number(_ number: Int) -> QuestionStep {
        number >= 1 && number <= ownRow + 1 ? click(number - 1) : QuestionStep(self)
    }

    /// A click on a row. One answer goes on to the next question at once, as in the console; with
    /// several answers a click only ticks the row.
    public func click(_ row: Int) -> QuestionStep {
        let chosen = choose(row)
        if chosen.outcome != .none || open.multiSelect {
            return chosen
        }
        return chosen.flow.forward()
    }

    /// The person's own words in the open question. Words pick Other; no words leave it as it was.
    public func write(_ words: String) -> QuestionFlow {
        var flow = with { $0.own[at] = words }
        if words.trimmingCharacters(in: .whitespaces).isEmpty || isPicked(ownRow) {
            return flow
        }
        flow.picked[at] = open.multiSelect ? picked[at] + [ownRow] : [ownRow]
        return flow
    }

    /// Enter in the field of the person's own words: the words stand, and the card goes on.
    public func confirm() -> QuestionStep {
        isAnswered(at) ? forward() : QuestionStep(self)
    }

    /// Back: the question before, with the keyboard on its first picked row.
    public func back() -> QuestionFlow {
        at == 0 ? self : go(at - 1)
    }

    /// Next, or Answer on the last question. A question without an answer keeps the card on it.
    public func forward() -> QuestionStep {
        guard isAnswered(at) else { return QuestionStep(self) }
        if !isLast {
            return QuestionStep(go(at + 1))
        }
        if let open = questions.indices.first(where: { !isAnswered($0) }) {
            return QuestionStep(go(open))
        }
        return QuestionStep(self, .answer)
    }

    /// A click on a step of the strip: that question.
    public func show(_ question: Int) -> QuestionFlow {
        questions.indices.contains(question) ? go(question) : self
    }

    /// The option under the focused one. On the line before the last, short by one, down goes to
    /// the last option, so Other is never out of reach.
    private func below(_ columns: Int) -> Int {
        let columns = max(1, columns)
        if focus / columns >= ownRow / columns { return focus }
        return min(ownRow, focus + columns)
    }

    private func enter() -> QuestionStep {
        // With one answer Enter takes the row the keyboard is on, as in the console.
        if !open.multiSelect && !isPicked(focus) {
            return click(focus)
        }
        return forward()
    }

    private func choose(_ row: Int) -> QuestionStep {
        guard row >= 0 && row <= ownRow else { return QuestionStep(self) }

        var focused = with { $0.focus = row }
        if row == ownRow && own[at].trimmingCharacters(in: .whitespaces).isEmpty {
            return QuestionStep(focused, .writeOwn)
        }
        focused.picked[at] = open.multiSelect
            ? (isPicked(row) ? picked[at].filter { $0 != row } : picked[at] + [row])
            : [row]
        return QuestionStep(focused)
    }

    private func go(_ question: Int) -> QuestionFlow {
        with {
            $0.at = question
            $0.focus = picked[question].min() ?? 0
        }
    }

    private func with(_ change: (inout QuestionFlow) -> Void) -> QuestionFlow {
        var copy = self
        change(&copy)
        return copy
    }
}
