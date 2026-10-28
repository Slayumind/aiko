import Foundation
import Testing

@testable import AikoKit

/// The card of questions read from spec/cases, the same table the xUnit tests read.
struct QuestionFlowCaseTests {
    @Test
    func everyFlowEndsWhereTheTableSays() {
        let cases = SpecCases.json("question-card")
        for row in cases["flows"]?.arrayValue ?? [] {
            let name = row["name"]?.stringValue ?? ""
            var step = QuestionStep(QuestionFlow.start(Self.questions(cases, row["questions"]?.stringValue ?? "")))
            for action in row["actions"]?.arrayValue ?? [] {
                step = Self.act(step.flow, action)
            }

            let flow = step.flow
            if let at = row["at"]?.int64Value { #expect(Int(at) == flow.at, "\(name)") }
            if let focus = row["focus"]?.int64Value { #expect(Int(focus) == flow.focus, "\(name)") }
            if let outcome = row["outcome"]?.stringValue { #expect(outcome == step.outcome.rawValue, "\(name)") }
            if let marks = row["marks"]?.arrayValue {
                #expect(marks.compactMap(\.stringValue) == flow.questions.indices.map { flow.mark($0).rawValue }, "\(name)")
            }
            if let answers = row["answers"]?.arrayValue {
                #expect(answers.map { ($0.arrayValue ?? []).compactMap(\.stringValue) } == flow.answers(), "\(name)")
            }
            if let preview = row["preview"] {
                #expect(preview.stringValue == flow.preview, "\(name)")
            }
        }
    }

    private static func act(_ flow: QuestionFlow, _ action: JsonNode) -> QuestionStep {
        if let key = action["key"]?.stringValue.flatMap(QuestionKey.init(rawValue:)) {
            return flow.press(key, columns: Int(action["columns"]?.int64Value ?? 1))
        }
        if let number = action["number"]?.int64Value { return flow.number(Int(number)) }
        if let row = action["click"]?.int64Value { return flow.click(Int(row)) }
        if let words = action["write"]?.stringValue { return QuestionStep(flow.write(words)) }
        if action["confirm"] != nil { return flow.confirm() }
        if action["back"] != nil { return QuestionStep(flow.back()) }
        if action["forward"] != nil { return flow.forward() }
        return QuestionStep(flow.show(Int(action["show"]?.int64Value ?? -1)))
    }

    private static func questions(_ cases: JsonObject, _ set: String) -> [CardQuestion] {
        (cases["questions"]?[set]?.arrayValue ?? []).map { question in
            CardQuestion(
                text: question["text"]?.stringValue ?? "",
                header: question["header"]?.stringValue ?? "",
                multiSelect: question["multi"]?.boolValue ?? false,
                options: (question["options"]?.arrayValue ?? []).map {
                    QuestionOption(
                        label: $0["label"]?.stringValue ?? "",
                        description: $0["description"]?.stringValue ?? "",
                        preview: $0["preview"]?.stringValue ?? "")
                })
        }
    }
}
