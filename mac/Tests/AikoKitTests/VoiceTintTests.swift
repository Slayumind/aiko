import Testing
@testable import AikoKit

struct VoiceTintTests {
    @Test func everyPieceOfOutputIsTintedTheSame() {
        for row in SpecCases.json("voice-tint")["cases"]?.arrayValue ?? [] {
            let tint = VoiceTint(red: 207, green: 233, blue: 223)
            let output = (row["pieces"]?.arrayValue ?? []).map { tint.apply($0.stringValue ?? "") }.joined()
            #expect(output == row["output"]?.stringValue, "\(row["name"]?.stringValue ?? "")")
        }
    }
}
