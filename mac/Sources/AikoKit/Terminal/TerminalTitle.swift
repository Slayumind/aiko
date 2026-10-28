import Foundation

/// Finds the title a program sets for its terminal, OSC 0 or OSC 2, in the output of the terminal
/// (D-360). Output comes in pieces, and a title may be cut between two of them.
///
/// The twin of TitleReader in TerminalTitle.cs.
public final class TitleReader {
    /// A title longer than this is not a title. Nothing past it is kept in memory.
    public static let maxLength = 1024

    private static let escape: Unicode.Scalar = "\u{1B}"
    private static let bell: Unicode.Scalar = "\u{07}"

    private enum State {
        case text
        /// An escape came; a "]" starts a command of the operating system.
        case afterEscape
        /// Inside the command, until a bell or an escape and a backslash.
        case command
        /// An escape came inside the command.
        case commandEscape
    }

    private var command = String.UnicodeScalarView()
    private var state = State.text

    public init() {}

    /// The last title that ended in this piece of output, or nil when none did.
    public func read(_ output: String) -> String? {
        var title: String?
        for c in output.unicodeScalars {
            switch state {
            case .text:
                if c == Self.escape {
                    state = .afterEscape
                }

            case .afterEscape:
                state = c == "]" ? .command : c == Self.escape ? .afterEscape : .text
                command.removeAll()

            case .command:
                if c == Self.bell {
                    title = finished() ?? title
                } else if c == Self.escape {
                    state = .commandEscape
                } else if command.count <= Self.maxLength + 2 {
                    // Past the limit nothing more is kept, and the command is no title.
                    command.append(c)
                }

            case .commandEscape:
                if c == "\\" {
                    title = finished() ?? title
                } else {
                    // A new escape sequence cut the command short.
                    command.removeAll()
                    state = c == "]" ? .command : .text
                }
            }
        }

        return title
    }

    private func finished() -> String? {
        state = .text
        let text = String(command)
        let length = command.count
        command.removeAll()
        guard length <= Self.maxLength + 2, text.hasPrefix("0;") || text.hasPrefix("2;") else {
            return nil
        }

        return String(text.unicodeScalars.dropFirst(2))
    }
}

/// Whether output of a terminal draws something a person can see. A program starts with sequences
/// that only clear the screen, move the cursor and set the title; until a letter comes the terminal
/// shows a loader instead (D-368). Places are UTF-16 units, as in C#.
///
/// The twin of TerminalScreen in TerminalTitle.cs.
public enum TerminalScreen {
    public static func hasText(_ output: String) -> Bool {
        let units = Array(output.utf16)
        var at = 0
        while at < units.count {
            let unit = units[at]
            if unit == 0x1B, at + 1 < units.count {
                at = sequenceEnd(units, at) + 1
                continue
            }

            if let scalar = Unicode.Scalar(unit), !scalar.properties.isWhitespace,
               scalar.properties.generalCategory != .control {
                return true
            }
            if Unicode.Scalar(unit) == nil {
                // Half of a pair: a letter outside the first plane.
                return true
            }
            at += 1
        }
        return false
    }

    private static func sequenceEnd(_ units: [UInt16], _ escape: Int) -> Int {
        let kind = units[escape + 1]
        var at = escape + 2
        // A choice of a character set, ESC ( B: two characters after the escape.
        if [0x28, 0x29, 0x2A, 0x2B].contains(kind) {
            return min(escape + 2, units.count - 1)
        }
        if kind == 0x5B {
            while at < units.count, !(0x40...0x7E).contains(units[at]) {
                at += 1
            }
            return at
        }

        if kind == 0x5D {
            while at < units.count, units[at] != 0x07, !(units[at] == 0x1B && at + 1 < units.count && units[at + 1] == 0x5C) {
                at += 1
            }
            return at < units.count && units[at] == 0x1B ? at + 1 : at
        }

        return escape + 1
    }
}

/// The title of Claude Code (spike 2026-10): "✳ name" waits for input, a turning half circle before
/// the name means a turn is going on. The name is "Claude Code" until the first turn, then the
/// subject of the conversation.
///
/// The twin of ClaudeTitle in TerminalTitle.cs.
public struct ClaudeTitle: Sendable, Equatable {
    /// The name Claude Code shows before it knows the subject.
    public static let defaultName = "Claude Code"

    public let working: Bool
    public let name: String

    public init(working: Bool, name: String) {
        self.working = working
        self.name = name
    }

    /// Nil for a title that is not one of Claude Code.
    public static func parse(_ title: String?) -> ClaudeTitle? {
        let text = (title ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        guard let mark = text.unicodeScalars.first else {
            return nil
        }

        let working: Bool
        if mark == "\u{2733}" {
            working = false
        } else if isSpinner(mark) {
            working = true
        } else {
            return nil
        }

        let rest = text.unicodeScalars.dropFirst().drop { $0 == "\u{FE0F}" }
        return ClaudeTitle(working: working, name: String(rest).trimmingCharacters(in: .whitespacesAndNewlines))
    }

    /// The name a session takes from the title: none before Claude Code knows the subject.
    public var sessionName: String? {
        name.isEmpty || name == Self.defaultName ? nil : name
    }

    /// The half circles of the spike, the quarter ones next to them, and the braille dots that
    /// other versions of Claude Code turn.
    private static func isSpinner(_ c: Unicode.Scalar) -> Bool {
        (0x25D0...0x25D3).contains(c.value) || (0x2801...0x28FF).contains(c.value)
    }
}
