import Foundation

/// A line for the log of Aiko without full paths (D-420). PRIVACY.md promises that the log has no
/// working folders, and the message of a system error often names the file it failed on. A path
/// keeps only its last part, so the line still says which file. Rows: spec/cases/log-paths. The
/// twin of LogText.cs, with the same two patterns.
public enum LogText {
    public static func withoutPaths(_ line: String) -> String {
        let quoted = replace(quotedPath, in: line) { match, text in
            let quote = text.substring(with: match.range(withName: "q"))
            return quote + lastPart(text.substring(with: match.range(withName: "path"))) + quote
        }
        return replace(barePath, in: quoted) { match, text in
            lastPart(text.substring(with: match.range))
        }
    }

    private static func lastPart(_ path: String) -> String {
        var trimmed = Substring(path)
        while let last = trimmed.last, last == "\\" || last == "/" {
            trimmed = trimmed.dropLast()
        }
        guard let cut = trimmed.lastIndex(where: { $0 == "\\" || $0 == "/" }) else { return String(trimmed) }
        return String(trimmed[trimmed.index(after: cut)...])
    }

    /// A path in quotes may have spaces in it: '/Users/me/personal projects/a.txt'.
    private static let quotedPath = try! NSRegularExpression(
        pattern: #"(?<q>['"])(?<path>(?:[A-Za-z]:[\\/]|\\\\|/(?:Users|home|private|var|tmp|Volumes)/)[^'"\r\n]*)\k<q>"#)

    /// A path without quotes ends at the first space.
    private static let barePath = try! NSRegularExpression(
        pattern: #"(?:(?<![\w])[A-Za-z]:[\\/]|\\\\(?=\w)|(?<![\w.~])/(?:Users|home|private|var|tmp|Volumes)/)[^\s'"<>|]*"#)

    private static func replace(
        _ pattern: NSRegularExpression, in line: String, with make: (NSTextCheckingResult, NSString) -> String
    ) -> String {
        let text = line as NSString
        var result = ""
        var from = 0
        for match in pattern.matches(in: line, range: NSRange(location: 0, length: text.length)) {
            result += text.substring(with: NSRange(location: from, length: match.range.location - from))
            result += make(match, text)
            from = match.range.location + match.range.length
        }
        return result + text.substring(from: from)
    }
}
