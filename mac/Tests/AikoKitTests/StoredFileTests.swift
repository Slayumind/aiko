import Testing

@testable import AikoKit

/// What happens to Aiko's own files after a read, from spec/cases/stored-file. The twin of
/// StoredFileTests.cs.
struct StoredFileTests {
    @Test
    func everyCaseReadsTheSameWay() {
        let rows = SpecCases.json("stored-file")["cases"]?.arrayValue ?? []
        #expect(!rows.isEmpty)
        for row in rows {
            let state = StoredFile.of(exists: row["exists"]?.boolValue ?? false, text: row["text"]?.stringValue)
            #expect(state.rawValue == row["state"]?.stringValue)
            #expect(StoredFile.mayWrite(state) == row["mayWrite"]?.boolValue)
        }
    }
}
