import Foundation

public enum StepKind: Sendable, Equatable {
    case read
    case search
    case edit
    case run
    case agent
    case web

    /// Any other tool. The label names it.
    case other
}

/// One step of the work as one line of the feed: what kind it is and what it is about (D-294).
///
/// The label is set for a tool the terminal has no word for: the name of the tool, or the name of
/// the MCP server for a tool that comes from one. Added and removed count the lines an edit
/// changes. A hidden step is not drawn: a question and a plan come as cards, and two tools are
/// only noise.
public struct StepLine: Sendable, Equatable {
    public var kind: StepKind
    public var label: String
    public var subject: String
    public var added: Int
    public var removed: Int
    public var hidden: Bool

    public init(kind: StepKind, label: String, subject: String, added: Int, removed: Int, hidden: Bool) {
        self.kind = kind
        self.label = label
        self.subject = subject
        self.added = added
        self.removed = removed
        self.hidden = hidden
    }

    public static func forTool(_ toolName: String, _ input: JsonObject) -> StepLine {
        switch toolName {
        case "Read":
            let file = text(input, "file_path")
            var step = line(.read, fileName(file))
            step.hidden = isAttachment(file)
            return step
        case "Glob", "Grep":
            return line(.search, text(input, "pattern"))
        case "Write":
            var step = line(.edit, fileName(text(input, "file_path")))
            step.added = lines(text(input, "content")).count
            return step
        case "Edit":
            let change = changed(text(input, "old_string"), text(input, "new_string"))
            var step = line(.edit, fileName(text(input, "file_path")))
            step.added = change.added
            step.removed = change.removed
            return step
        case "NotebookEdit":
            return line(.edit, fileName(text(input, "notebook_path")))
        case "Bash", "PowerShell":
            return line(.run, firstLine(text(input, "command")))
        case "Agent", "Task":
            return line(.agent, text(input, "description"))
        case "WebFetch":
            return line(.web, text(input, "url"))
        case "WebSearch":
            return line(.web, text(input, "query"))
        case "AskUserQuestion", "ExitPlanMode", "ToolSearch":
            return StepLine(kind: .other, label: toolName, subject: "", added: 0, removed: 0, hidden: true)
        default:
            break
        }

        // mcp__<server>__<tool>: the server may have underscores of its own, the tool comes last.
        let units = Array(toolName.utf16)
        if toolName.hasPrefix("mcp__"), let cut = lastDoubleUnderscore(units), cut > 5, cut + 2 < units.count {
            return StepLine(
                kind: .other,
                label: String(decoding: units[5..<cut], as: UTF16.self),
                subject: String(decoding: units[(cut + 2)...], as: UTF16.self),
                added: 0, removed: 0, hidden: false)
        }

        return StepLine(kind: .other, label: toolName, subject: "", added: 0, removed: 0, hidden: false)
    }

    /// The lines of an edit that differ, after the equal lines at both ends are set aside.
    public static func changed(_ before: String, _ after: String) -> (added: Int, removed: Int) {
        let old = lines(before)
        let now = lines(after)
        let ends = equalEnds(old, now)
        return (now.count - ends.start - ends.end, old.count - ends.start - ends.end)
    }

    /// How many lines are equal at the start of two texts, and how many at the end after those.
    /// Compared unit by unit, as the Windows core compares strings: two spellings of one letter
    /// are two different lines.
    public static func equalEnds(_ before: [String], _ after: [String]) -> (start: Int, end: Int) {
        let old = before.map { Array($0.utf16) }
        let now = after.map { Array($0.utf16) }

        var start = 0
        while start < old.count && start < now.count && old[start] == now[start] {
            start += 1
        }

        var end = 0
        while end < old.count - start && end < now.count - start
            && old[old.count - 1 - end] == now[now.count - 1 - end] {
            end += 1
        }

        return (start, end)
    }

    /// The lines of a text. One line break at the very end does not start another line.
    ///
    /// The text is cut in UTF-16 units: Swift counts "\r\n" as one character, and a split by
    /// characters would not see the line break inside it.
    public static func lines(_ text: String) -> [String] {
        if text.isEmpty {
            return []
        }

        var units: [UInt16] = []
        let source = Array(text.utf16)
        var at = 0
        while at < source.count {
            if source[at] == 13 && at + 1 < source.count && source[at + 1] == 10 {
                at += 1
            }
            units.append(source[at])
            at += 1
        }

        var result = units.split(separator: 10, omittingEmptySubsequences: false)
            .map { String(decoding: $0, as: UTF16.self) }
        if result.count > 1 && result[result.count - 1].isEmpty {
            result.removeLast()
        }
        return result
    }

    private static func line(_ kind: StepKind, _ subject: String) -> StepLine {
        StepLine(kind: kind, label: "", subject: subject, added: 0, removed: 0, hidden: false)
    }

    private static func text(_ input: JsonObject, _ key: String) -> String {
        input[key]?.stringValue ?? ""
    }

    private static func lastDoubleUnderscore(_ units: [UInt16]) -> Int? {
        var at = units.count - 2
        while at >= 0 {
            if units[at] == 95 && units[at + 1] == 95 {
                return at
            }
            at -= 1
        }
        return nil
    }

    /// The name after the last slash of either kind: the path may come from another system.
    private static func fileName(_ path: String) -> String {
        var units = Array(path.utf16)
        while let last = units.last, last == 47 || last == 92 {
            units.removeLast()
        }
        let start = (units.lastIndex { $0 == 47 || $0 == 92 } ?? -1) + 1
        return String(decoding: units[start...], as: UTF16.self)
    }

    private static func firstLine(_ text: String) -> String {
        var units = Array(text.utf16)
        func isEdge(_ unit: UInt16) -> Bool { unit == 32 || unit == 9 || unit == 13 || unit == 10 }
        while let first = units.first, isEdge(first) {
            units.removeFirst()
        }
        while let last = units.last, isEdge(last) {
            units.removeLast()
        }
        if let end = units.firstIndex(where: { $0 == 13 || $0 == 10 }) {
            units = Array(units[..<end])
            while let last = units.last, last == 32 || last == 9 {
                units.removeLast()
            }
        }
        return String(decoding: units, as: UTF16.self)
    }

    /// Older versions of Claude Code save a pasted picture into a temporary folder and read it
    /// back with Read. That step is the picture the person already sees in their own message.
    private static func isAttachment(_ path: String) -> Bool {
        let normal = path.replacingOccurrences(of: "\\", with: "/")
        return normal.contains("/claude-") && normal.contains("/images/")
    }
}
