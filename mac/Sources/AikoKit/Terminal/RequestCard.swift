import Foundation

public enum CardKind: Sendable, Equatable {
    /// May this tool run?
    case permission

    /// A question with options.
    case question

    /// A plan to approve.
    case plan
}

/// What the second button of a permission card does.
public enum SecondChoice: Sendable, Equatable {
    case none

    /// Allow, and stop asking about edits in this session.
    case editsInSession

    /// Allow, and never ask about this command in this project again (D-306).
    case alwaysAllow
}

public struct QuestionOption: Sendable, Equatable {
    public let label: String
    public let description: String

    public init(label: String, description: String) {
        self.label = label
        self.description = description
    }
}

public struct CardQuestion: Sendable, Equatable {
    public let text: String
    public let header: String
    public let multiSelect: Bool
    public let options: [QuestionOption]

    public init(text: String, header: String, multiSelect: Bool, options: [QuestionOption]) {
        self.text = text
        self.header = header
        self.multiSelect = multiSelect
        self.options = options
    }
}

/// What the person chose on a card.
public enum CardChoice: Sendable, Equatable {
    /// The first button: yes to this one call, or yes to the plan.
    case allow

    /// The second button of a permission card.
    case second

    /// No. The message goes to Claude; an empty one becomes a plain refusal.
    case deny(String)

    /// The answers to the questions, in their order: the labels picked, or the person's own words.
    case answers([[String]])
}

/// A request of Claude Code as a card of the feed: a permission, a question or a plan (D-294).
public struct RequestCard: Sendable, Equatable {
    public let kind: CardKind
    public let requestId: String
    public let toolUseId: String
    public let fromSubagent: Bool
    public let step: StepLine
    public let second: SecondChoice
    public let ruleText: String
    public let questions: [CardQuestion]
    public let plan: String
    public let source: ToolRequest

    /// What Claude reads when the person says no without a word.
    public static let plainRefusal = "The user said no."

    public static func from(_ request: ToolRequest) -> RequestCard {
        let step = StepLine.forTool(request.toolName, request.input)
        let kind: CardKind
        switch request.toolName {
        case "AskUserQuestion": kind = .question
        case "ExitPlanMode": kind = .plan
        default: kind = .permission
        }

        return RequestCard(
            kind: kind,
            requestId: request.requestId,
            toolUseId: request.toolUseId,
            fromSubagent: !(request.agentId ?? "").isEmpty,
            step: step,
            second: kind == .permission ? secondFor(step, request) : .none,
            ruleText: request.ruleText,
            questions: kind == .question ? questionsIn(request.input) : [],
            plan: kind == .plan ? request.input["plan"]?.stringValue ?? "" : "",
            source: request)
    }

    /// The line that answers the request.
    public func lineFor(_ choice: CardChoice) -> String {
        switch choice {
        case .second where second == .editsInSession:
            return HostLine.allowEdits(requestId, input: source.input)
        case .second where second == .alwaysAllow && source.rule != nil:
            return HostLine.allowAlways(requestId, input: source.input, rule: source.rule ?? JsonObject())
        case .deny(let words):
            let message = Self.trimmed(words)
            return HostLine.deny(requestId, message: message.isEmpty ? Self.plainRefusal : message)
        case .answers(let picked):
            return HostLine.answer(requestId, input: source.input, answers: paired(picked))
        default:
            return HostLine.allow(requestId, input: source.input)
        }
    }

    /// An edit offers "edits in this session"; anything else offers the rule, when there is one.
    /// A command is never answered with the edit mode: that would allow more than the button says.
    private static func secondFor(_ step: StepLine, _ request: ToolRequest) -> SecondChoice {
        if step.kind == .edit && request.suggestsAcceptEdits {
            return .editsInSession
        }
        return request.rule != nil ? .alwaysAllow : .none
    }

    /// Each answer under the text of its question. Several picks are joined with a comma, and a
    /// question nobody answered is left out.
    private func paired(_ picked: [[String]]) -> [(question: String, answer: String)] {
        var pairs: [(question: String, answer: String)] = []
        for index in 0..<min(questions.count, picked.count) {
            let answer = picked[index].filter { !$0.isEmpty }.joined(separator: ", ")
            if !answer.isEmpty {
                pairs.append((question: questions[index].text, answer: answer))
            }
        }
        return pairs
    }

    private static func questionsIn(_ input: JsonObject) -> [CardQuestion] {
        var questions: [CardQuestion] = []
        for question in objects(input["questions"]) {
            let options = objects(question["options"])
                .map { QuestionOption(label: $0["label"]?.stringValue ?? "", description: $0["description"]?.stringValue ?? "") }
                .filter { !$0.label.isEmpty }

            if let text = question["question"]?.stringValue, !text.isEmpty {
                questions.append(CardQuestion(
                    text: text,
                    header: question["header"]?.stringValue ?? "",
                    multiSelect: question["multiSelect"]?.boolValue ?? false,
                    options: options))
            }
        }
        return questions
    }

    private static func objects(_ node: JsonNode?) -> [JsonObject] {
        (node?.arrayValue ?? []).compactMap(\.objectValue)
    }

    /// Without the white space at both ends, the way String.Trim cuts it on Windows.
    static func trimmed(_ text: String) -> String {
        text.trimmingCharacters(in: .whitespacesAndNewlines)
    }
}
