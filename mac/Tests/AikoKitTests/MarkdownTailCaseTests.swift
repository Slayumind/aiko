import Foundation
import Testing

@testable import AikoKit

/// The end of a streaming answer made whole, read from spec/cases, the same table the xUnit tests read.
struct MarkdownTailCaseTests {
    @Test
    func everyTailIsClosedAsTheTableSays() {
        for row in SpecCases.json("markdown-tail")["tails"]?.arrayValue ?? [] {
            let closed = MarkdownTail.closed(row["text"]?.stringValue ?? "")
            #expect(closed == row["closed"]?.stringValue, "\(row["name"]?.stringValue ?? "")")
        }
    }
}
