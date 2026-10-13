import Foundation

/// A part of an answer that is drawn as one piece and selected as one piece.
public enum AnswerSegment: Sendable, Equatable {
    /// Paragraphs, headings, lists, quotes and rules that follow each other.
    case prose([MarkdownBlock])

    /// A block of code, which has its own copy button.
    case code(language: String, text: String)

    case table(columns: [ColumnAlign], header: [[MarkdownInline]], rows: [[[MarkdownInline]]])
}

/// Cuts an answer into the pieces the window draws (D-304).
///
/// Text is selected inside one piece. Code and tables are pieces of their own, because each is
/// drawn by its own view; everything between them is one piece of prose.
public enum AnswerSegments {
    public static func of(_ blocks: [MarkdownBlock]) -> [AnswerSegment] {
        var segments: [AnswerSegment] = []
        var prose: [MarkdownBlock] = []

        func endProse() {
            if !prose.isEmpty {
                segments.append(.prose(prose))
                prose = []
            }
        }

        for block in blocks {
            switch block {
            case .code(let language, let text):
                endProse()
                segments.append(.code(language: language, text: text))
            case .table(let columns, let header, let rows):
                endProse()
                segments.append(.table(columns: columns, header: header, rows: rows))
            default:
                prose.append(block)
            }
        }

        endProse()
        return segments
    }
}
