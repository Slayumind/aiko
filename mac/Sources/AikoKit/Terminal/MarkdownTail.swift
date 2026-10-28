import Foundation

/// The end of an answer that is still being written, made whole for drawing (D-332). The twin of
/// Terminal/MarkdownTail.cs: the words after the last blank line or code block get the marks they
/// still wait for, an opening mark with nothing after it is dropped, and an open code block is
/// left alone.
public enum MarkdownTail {
    public static func closed(_ markdown: String) -> String {
        let all = Array(markdown)
        let (inFence, tailStart) = scan(all)
        if all.isEmpty || inFence { return markdown }

        // Inline marks never cross a blank line or a code block, so only the words after the
        // last of them can wait for one.
        let head = String(all[..<tailStart])
        let tail = Array(all[tailStart...])

        var open = openMarks(tail)
        if open.isEmpty { return markdown }

        var trimmed = trimEnd(tail)
        while let last = open.last, trimmed.count - last.mark.count == last.at, String(trimmed.suffix(last.mark.count)) == last.mark {
            trimmed = trimEnd(Array(trimmed[..<last.at]))
            open.removeLast()
        }

        return head + String(trimmed) + open.reversed().map(\.mark).joined()
    }

    private struct OpenMark {
        let mark: String
        let at: Int
    }

    /// The marks opened and not closed, the last opened last. Inside inline code nothing counts.
    private static func openMarks(_ text: [Character]) -> [OpenMark] {
        var open: [OpenMark] = []
        var at = 0
        while at < text.count {
            // Only spaces before it on its line: a star there may be a list marker.
            var lineBegin = at
            while lineBegin > 0 && text[lineBegin - 1] != "\n" { lineBegin -= 1 }
            let lineStart = text[lineBegin..<at].allSatisfy(\.isWhitespace)

            if text[at] == "`" {
                guard let end = text[(at + 1)...].firstIndex(of: "`") else {
                    open.append(OpenMark(mark: "`", at: at))
                    return open
                }
                at = end + 1
                continue
            }

            let next: Character? = at + 1 < text.count ? text[at + 1] : nil

            // A star with spaces on both sides, as in "2 * 3", is not emphasis.
            let spaced = (at == 0 || text[at - 1].isWhitespace) && (next == nil || next!.isWhitespace)
            let mark: String?
            if text[at] == "*" && next == "*" {
                mark = "**"
            } else if text[at] == "~" && next == "~" {
                mark = "~~"
            } else if text[at] == "*" && !(lineStart && next == " ") && !spaced {
                mark = "*"
            } else {
                mark = nil
            }
            guard let mark else {
                at += 1
                continue
            }

            if let last = open.lastIndex(where: { $0.mark == mark }) {
                open.remove(at: last)
            } else {
                open.append(OpenMark(mark: mark, at: at))
            }
            at += mark.count
        }
        return open
    }

    private static func trimEnd(_ text: [Character]) -> [Character] {
        var end = text.count
        while end > 0 && text[end - 1].isWhitespace { end -= 1 }
        return Array(text[..<end])
    }

    /// Whether the text ends inside a code block (``` or ~~~), and where the words after the last
    /// blank line or the last closed code block start. A blank line inside a block is not one.
    private static func scan(_ all: [Character]) -> (inFence: Bool, tailStart: Int) {
        var fence: String?
        var tailStart = 0
        var start = 0
        while true {
            let end = all[start...].firstIndex(of: "\n")
            let line = all[start..<(end ?? all.count)]
            let next = end.map { $0 + 1 } ?? all.count
            let trimmed = String(line.drop(while: { $0 == " " || $0 == "\t" }))
            if fence == nil && (trimmed.hasPrefix("```") || trimmed.hasPrefix("~~~")) {
                fence = String(trimmed.prefix(3))
            } else if let open = fence, trimmed.hasPrefix(open) {
                fence = nil
                tailStart = next
            } else if fence == nil && trimmed.isEmpty && end != nil {
                tailStart = next
            }

            guard end != nil else {
                return (fence != nil, tailStart)
            }
            start = next
        }
    }
}
