import Testing

@testable import AikoKit

/// The twin of FieldHistoryCaseTests.cs: the arrows in the field, from spec/cases/field-history.
struct FieldHistoryCaseTests {
    @Test
    func everyWalkThroughWhatWasSentShowsTheSame() {
        for row in SpecCases.json("field-history")["walks"]?.arrayValue ?? [] {
            let name = row["name"]?.stringValue ?? ""
            let sent = (row["sent"]?.arrayValue ?? []).compactMap(\.stringValue)
            var field = row["field"]?.stringValue ?? ""
            var walk = FieldWalk.none
            for (at, step) in (row["steps"]?.arrayValue ?? []).enumerated() {
                let next = FieldHistory.step(sent, walk, field: field, by: step["key"]?.stringValue == "up" ? -1 : 1)
                walk = next.walk
                field = next.field ?? field
                #expect((next.field != nil) == (step["taken"]?.boolValue ?? false), "\(name), step \(at + 1): taken")
                #expect(field == step["field"]?.stringValue, "\(name), step \(at + 1)")
            }
        }
    }
}
