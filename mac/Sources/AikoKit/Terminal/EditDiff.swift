import Foundation

public enum DiffSide: Sendable, Equatable {
    case removed
    case added
}

public struct DiffLine: Sendable, Equatable {
    public let side: DiffSide
    public let text: String
}

/// The lines an edit takes out and puts in, for the card that asks to allow it (D-303).
///
/// No line numbers: Claude Code does not send them, and the card is read, not navigated. The
/// equal lines at both ends of an edit are set aside, as in StepLine.changed. A long edit shows
/// its first lines and says how many more there are.
///
/// The twin of EditDiff.cs.
public struct EditDiff: Sendable, Equatable {
    public static let mostLines = 40

    public let lines: [DiffLine]
    public let more: Int

    /// The diff of Edit, MultiEdit and Write. Nil for any other tool, and for an edit that
    /// changes no line.
    public static func forTool(_ toolName: String, _ input: JsonObject) -> EditDiff? {
        var lines: [DiffLine] = []
        switch toolName {
        case "Edit":
            addChange(&lines, text(input, "old_string"), text(input, "new_string"))
        case "MultiEdit":
            for edit in input["edits"]?.arrayValue ?? [] {
                guard let edit = edit.objectValue else { continue }
                addChange(&lines, text(edit, "old_string"), text(edit, "new_string"))
            }
        case "Write":
            lines += StepLine.lines(text(input, "content")).map { DiffLine(side: .added, text: $0) }
        default:
            return nil
        }

        if lines.isEmpty {
            return nil
        }

        return lines.count <= mostLines
            ? EditDiff(lines: lines, more: 0)
            : EditDiff(lines: Array(lines.prefix(mostLines)), more: lines.count - mostLines)
    }

    private static func addChange(_ lines: inout [DiffLine], _ before: String, _ after: String) {
        let old = StepLine.lines(before)
        let now = StepLine.lines(after)
        let ends = StepLine.equalEnds(old, now)

        for at in ends.start..<(old.count - ends.end) {
            lines.append(DiffLine(side: .removed, text: old[at]))
        }
        for at in ends.start..<(now.count - ends.end) {
            lines.append(DiffLine(side: .added, text: now[at]))
        }
    }

    private static func text(_ input: JsonObject, _ key: String) -> String {
        input[key]?.stringValue ?? ""
    }
}
