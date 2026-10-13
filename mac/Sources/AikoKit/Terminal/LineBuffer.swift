import Foundation

/// Cuts what a pipe delivers into lines.
///
/// A pipe hands over bytes in pieces of any size, and a piece can end in the middle of a line or
/// in the middle of one letter. Only whole lines are turned into text, so a letter is never cut.
/// A carriage return before the line break is dropped.
///
/// Windows needs no twin: there the reader of the process stream does this itself.
public struct LineBuffer: Sendable {
    private var pending = Data()

    /// How much of pending is known to hold no line break, so a long line is not searched again
    /// with every piece that arrives.
    private var searched = 0

    public init() {}

    /// The lines that are now whole, in order.
    public mutating func take(_ bytes: Data) -> [String] {
        pending.append(bytes)

        var lines: [String] = []
        var start = pending.startIndex
        var from = start + searched
        while let end = pending[from...].firstIndex(of: 0x0A) {
            lines.append(Self.text(pending[start..<end]))
            start = pending.index(after: end)
            from = start
        }

        if start != pending.startIndex {
            pending = Data(pending[start...])
        }
        searched = pending.count
        return lines
    }

    /// What is left when the pipe has closed: a last line without a line break, or nothing.
    public mutating func rest() -> String? {
        defer {
            pending = Data()
            searched = 0
        }
        return pending.isEmpty ? nil : Self.text(pending[...])
    }

    private static func text(_ line: Data) -> String {
        var line = line
        if line.last == 0x0D {
            line = line.dropLast()
        }
        return String(decoding: line, as: UTF8.self)
    }
}
