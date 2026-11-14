import Foundation

/// One command of a command line and what it printed (D-363, D-412). firstLine and endLine are rows
/// of the screen, endLine not included; exitCode is nil while it runs. Times are in milliseconds.
/// timed is false when the shell never said the command started: then nobody knows how long it took.
public struct ShellBlock: Sendable, Equatable {
    public let command: String
    public let firstLine: Int
    public let endLine: Int
    public let exitCode: Int?
    public let startedAt: Int64
    public let endedAt: Int64?
    public let timed: Bool

    public var running: Bool { endedAt == nil }
    public var failed: Bool { exitCode.map { $0 != 0 } ?? false }
    public var lineCount: Int { max(0, endLine - firstLine) }
}

/// Cuts the screen of a command line into commands by the marks of shell integration (OSC 133):
/// B starts what the person typed, C starts the output, D ends it with the exit code. A command that
/// is only Enter is no block. A shell that sends no C gets the output from the line after the
/// command. Rows: spec/cases/shell-screen. The twin of ShellBlocks.cs.
public enum ShellBlocks {
    public static func of(_ screen: ShellScreen) -> [ShellBlock] {
        var blocks: [ShellBlock] = []
        var typed: ShellMark?
        var output: ShellMark?
        let cursor = screen.cursor

        for mark in screen.marks {
            switch mark.kind {
            case "A":
                typed = nil
                output = nil
            case "B":
                typed = mark
                output = nil
            case "C":
                output = mark
            case "D":
                if let start = typed {
                    add(&blocks, screen, start, output, mark)
                    typed = nil
                    output = nil
                }
            default:
                break
            }
        }

        // A command that still runs: from C to the cursor.
        if let start = typed, let begun = output {
            let end = ShellMark(kind: "D", row: cursor.row + screen.dropped, column: cursor.column, code: nil, at: begun.at)
            add(&blocks, screen, start, begun, end, running: true)
        }

        return blocks
    }

    private static func add(
        _ blocks: inout [ShellBlock], _ screen: ShellScreen, _ typed: ShellMark, _ output: ShellMark?,
        _ end: ShellMark, running: Bool = false
    ) {
        let dropped = screen.dropped
        let typedRow = typed.row - dropped

        // Without C the command is the rest of its line and the lines the terminal broke from it.
        var commandEnd = typedRow
        while output == nil && screen.wrapped(commandEnd) {
            commandEnd += 1
        }

        let outputRow = output.map { $0.row - dropped } ?? commandEnd + 1
        let outputColumn = output?.column ?? 0
        let text = output != nil
            ? screen.text(fromRow: typedRow, fromColumn: typed.column, toRow: outputRow, toColumn: outputColumn)
            : screen.text(fromRow: typedRow, fromColumn: typed.column, toRow: commandEnd, toColumn: Int.max)
        let command = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !command.isEmpty else { return }

        // The output starts on the line after the command when C came at the end of it.
        var first = outputColumn > 0 && output != nil && outputRow == typedRow ? outputRow + 1 : outputRow
        let endRow = end.row - dropped
        var last = end.column > 0 ? endRow + 1 : endRow
        guard first >= 0 else { return }

        // Blank lines at either end belong to no one: a spinner leaves one, the next prompt starts with one.
        last = max(first, last)
        while first < last && screen.lineText(first).isEmpty {
            first += 1
        }
        while last > first && screen.lineText(last - 1).isEmpty {
            last -= 1
        }

        // Without C the command never started, so it took no time; the moment of B would count the
        // typing as well.
        let startedAt = output?.at ?? end.at
        blocks.append(ShellBlock(
            command: command,
            firstLine: first,
            endLine: last,
            exitCode: running ? nil : end.code ?? 0,
            startedAt: startedAt,
            endedAt: running ? nil : end.at,
            timed: output != nil))
    }
}
