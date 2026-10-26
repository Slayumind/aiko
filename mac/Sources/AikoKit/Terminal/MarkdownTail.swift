import Foundation

/// The end of an answer that is still being written, made whole for drawing (D-332). The twin of
/// Terminal/MarkdownTail.cs: the last paragraph gets the marks it still waits for, an opening mark
/// with nothing after it is dropped, and an open code block is left alone.
public enum MarkdownTail {
    public static func closed(_ markdown: String) -> String {
        if markdown.isEmpty || inOpenFence(markdown) { return markdown }

        let all = Array(markdown)
        var cut = -1
        if all.count >= 2 {
            for index in stride(from: all.count - 2, through: 0, by: -1) where all[index] == "\n" && all[index + 1] == "\n" {
                cut = index
                break
            }
        }
        let head = cut < 0 ? "" : String(all[..<(cut + 2)])
        let tail = cut < 0 ? all : Array(all[(cut + 2)...])

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
            let lineStart = at == 0 || text[at - 1] == "\n"
            if text[at] == "`" {
                guard let end = text[(at + 1)...].firstIndex(of: "`") else {
                    open.append(OpenMark(mark: "`", at: at))
                    return open
                }
                at = end + 1
                continue
            }

            let next: Character? = at + 1 < text.count ? text[at + 1] : nil
            let mark: String?
            if text[at] == "*" && next == "*" {
                mark = "**"
            } else if text[at] == "~" && next == "~" {
                mark = "~~"
            } else if text[at] == "*" && !(lineStart && next == " ") {
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

    private static func inOpenFence(_ markdown: String) -> Bool {
        markdown.split(separator: "\n", omittingEmptySubsequences: false)
            .filter { $0.drop(while: { $0 == " " || $0 == "\t" }).hasPrefix("```") }
            .count % 2 == 1
    }
}
