import Foundation

/// Text copied out of a terminal (D-371): see TerminalCopy.cs, the twin. Lengths are UTF-16 units,
/// as in C#; "\r\n" is one Character in Swift, so line breaks are worked on in units.
public enum TerminalCopy {
    public static func unwrap(_ text: String, columns: Int) -> String {
        let lines = splitLines(text).map(trimmedEnd)
        guard lines.count >= 2, columns > 0 else {
            return lines.joined(separator: "\n")
        }

        // The indent of the rows after the first: what Claude Code puts before every following row.
        let indent = lines.dropFirst().filter { !$0.isEmpty }.map(indentOf).min() ?? 0

        var joined = [lines[0]]

        // A selection starts after the mark of a message, and Claude Code puts the following rows
        // right under the start of the text: the first row was as wide as its text and that indent.
        var lastWidth = (indentOf(lines[0]) == 0 ? indent : 0) + lines[0].utf16.count
        for row in lines.dropFirst() {
            let units = Array(row.utf16)
            let line = units.count >= indent
                ? String(decoding: units[indent...], as: UTF16.self)
                : row.trimmingCharacters(in: .whitespaces)
            let previous = joined[joined.count - 1]

            if wrapped(previous, lastWidth, line, row, indent, columns) {
                joined[joined.count - 1] = previous + " " + line
            } else {
                joined.append(line)
            }

            lastWidth = units.count
        }

        return joined.joined(separator: "\n")
    }

    private static func wrapped(_ previous: String, _ width: Int, _ line: String, _ row: String, _ indent: Int, _ columns: Int) -> Bool {
        if previous.trimmingCharacters(in: .whitespaces).isEmpty || line.isEmpty || indentOf(row) > indent {
            return false
        }
        if isListItem(line) || hasFrame(previous) || hasFrame(line) {
            return false
        }

        let firstWord = line.split(separator: " ", maxSplits: 1, omittingEmptySubsequences: false).first.map(String.init) ?? ""
        return width + 1 + firstWord.utf16.count > columns
    }

    private static func indentOf(_ line: String) -> Int {
        Array(line.utf16).prefix { $0 == 0x20 }.count
    }

    private static func isListItem(_ line: String) -> Bool {
        if line.hasPrefix("- ") || line.hasPrefix("* ") || line.hasPrefix("• ") {
            return true
        }

        let units = Array(line.utf16)
        var digits = 0
        while digits < units.count, (0x30...0x39).contains(units[digits]) {
            digits += 1
        }
        return digits > 0 && digits + 1 < units.count && units[digits] == 0x2E && units[digits + 1] == 0x20
    }

    private static func hasFrame(_ line: String) -> Bool {
        line.unicodeScalars.contains { (0x2500...0x257F).contains($0.value) }
    }

    private static func splitLines(_ text: String) -> [String] {
        var lines: [String] = []
        var current: [UInt16] = []
        let units = Array(text.utf16)
        var at = 0
        while at < units.count {
            let unit = units[at]
            if unit == 0x0D || unit == 0x0A {
                lines.append(String(decoding: current, as: UTF16.self))
                current = []
                at += unit == 0x0D && at + 1 < units.count && units[at + 1] == 0x0A ? 2 : 1
                continue
            }
            current.append(unit)
            at += 1
        }
        lines.append(String(decoding: current, as: UTF16.self))
        return lines
    }

    /// TrimEnd of .NET: every white space at the end goes.
    private static func trimmedEnd(_ line: String) -> String {
        var scalars = Array(line.unicodeScalars)
        while let last = scalars.last, last.properties.isWhitespace {
            scalars.removeLast()
        }
        return String(String.UnicodeScalarView(scalars))
    }
}
