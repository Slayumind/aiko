import Testing

@testable import AikoKit

/// A path in the log keeps only its last part (D-420), from spec/cases/log-paths. The twin of
/// LogTextTests.cs.
struct LogTextTests {
    @Test
    func aPathInTheLogKeepsOnlyItsLastPart() {
        let rows = SpecCases.json("log-paths")["rows"]?.arrayValue ?? []
        #expect(!rows.isEmpty)
        for row in rows {
            #expect(LogText.withoutPaths(row["line"]?.stringValue ?? "") == row["clean"]?.stringValue)
        }
    }
}
