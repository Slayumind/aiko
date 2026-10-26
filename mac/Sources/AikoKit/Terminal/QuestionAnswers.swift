import Foundation

/// One answered question as the feed shows it after the card: the header of the question, or its
/// text when it has none, and what the person chose. The twin of QuestionAnswer in QuestionAnswers.cs.
public struct QuestionAnswer: Sendable, Equatable {
    public let header: String
    public let answer: String

    public init(header: String, answer: String) {
        self.header = header
        self.answer = answer
    }
}

/// The answers to a card of questions, read from what Claude Code keeps beside the result of the
/// tool: tool_use_result in the stream, toolUseResult in the file of the conversation.
public enum QuestionAnswers {
    /// The tool behind a card of questions; its step is hidden until it has answers.
    public static let tool = "AskUserQuestion"

    /// In the order of the questions; an answer to a question not in the list comes after them.
    public static func from(_ result: JsonObject?) -> [QuestionAnswer] {
        guard let result, let answers = result["answers"]?.objectValue else { return [] }

        var given: [(question: String, answer: String)] = answers.members.compactMap { member in
            member.value.stringValue.map { (member.key, $0) }
        }
        var list: [QuestionAnswer] = []
        for question in result["questions"]?.arrayValue ?? [] {
            guard let text = question["question"]?.stringValue,
                  let at = given.firstIndex(where: { $0.question == text }) else { continue }
            let header = question["header"]?.stringValue ?? ""
            list.append(QuestionAnswer(header: header.isEmpty ? text : header, answer: given[at].answer))
            given.remove(at: at)
        }
        list += given.map { QuestionAnswer(header: $0.question, answer: $0.answer) }
        return list
    }
}
