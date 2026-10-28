import Testing

@testable import AikoKit

/// The twin of ModelChoiceCaseTests.cs: the models a new session offers, from spec/cases/model-choice.
struct ModelChoiceCaseTests {
    private static let cases = SpecCases.json("model-choice")

    private static let named: [ClaudeModel] = (cases["named"]?.arrayValue ?? []).map { model in
        ClaudeModel(value: model["value"]?.stringValue ?? "", displayName: model["displayName"]?.stringValue ?? "")
    }

    private static func offered(_ row: JsonNode) -> [ClaudeModel] {
        NewSessionChoices.offered(row["fromNamed"]?.boolValue == true ? named : [])
    }

    private static func words(_ row: JsonNode, _ key: String) -> [String] {
        (row[key]?.arrayValue ?? []).compactMap(\.stringValue)
    }

    @Test
    func everyListOfModelsIsOfferedTheSame() {
        for row in Self.cases["offered"]?.arrayValue ?? [] {
            let list = Self.offered(row)
            let name = row["name"]?.stringValue ?? ""
            #expect(list.map(\.value) == Self.words(row, "values"), "\(name): values")
            #expect(list.map(\.displayName) == Self.words(row, "names"), "\(name): names")
        }
    }

    @Test
    func everyClickOnTheModelTakesTheSameNextOne() {
        for row in Self.cases["next"]?.arrayValue ?? [] {
            let model = row["model"]?.stringValue
            let list = Self.offered(row).map(\.value)
            #expect(NewSessionChoices.nextModel(model, list) == row["next"]?.stringValue, "\(model ?? "nil")")
        }
    }

    @Test
    func everyNewSessionStartsWithTheSameModel() {
        for row in Self.cases["default"]?.arrayValue ?? [] {
            var sessions = TerminalSessions.empty
            for (at, session) in (row["sessions"]?.arrayValue ?? []).enumerated() {
                sessions = sessions.add(
                    id: "s\(at + 1)", environment: session["environment"]?.stringValue ?? "", folder: "/work",
                    name: "s\(at + 1)", model: session["model"]?.stringValue, mode: .manual,
                    now: session["lastUsed"]?.int64Value ?? 0)
            }
            let list = Self.offered(row).map(\.value)
            #expect(
                NewSessionChoices.defaultModel(sessions, row["environment"]?.stringValue ?? "", list) == row["model"]?.stringValue,
                "\(row["name"]?.stringValue ?? "")")
        }
    }
}
