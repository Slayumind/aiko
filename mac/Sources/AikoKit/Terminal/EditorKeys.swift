import Foundation

/// A key of the editor under the terminal that may matter to the program (D-354).
///
/// The twin of EditorKey in EditorKeys.cs.
public enum EditorKey: String, Sendable, CaseIterable {
    case up, down, left, right, enter, escape, tab, backspace
    /// A character typed: a digit, "/" or any other.
    case character
    /// Ctrl+C.
    case interrupt
}

/// What a key does.
public enum EditorAction: String, Sendable {
    case keep, pass, send, newLine, historyBack, historyForward, complete
    /// The text goes into the line of Claude Code without Enter (D-370).
    case insert
    /// The editor of a session of Claude Code closes.
    case hide
}

/// What a key did, and whether the editor now passes every key to the program.
public struct EditorPress: Sendable, Equatable {
    public let action: EditorAction
    public let sequence: String
    public let passing: Bool

    public init(_ action: EditorAction, _ sequence: String, _ passing: Bool) {
        self.action = action
        self.sequence = sequence
        self.passing = passing
    }
}

/// Where the editor stands when a key comes.
public struct EditorState: Sendable, Equatable {
    public let kind: SessionKind
    public let empty: Bool
    public let firstLine: Bool
    public let lastLine: Bool
    public let passing: Bool
    /// The editor of a session of Claude Code was opened with Ctrl+G (D-370).
    public let composing: Bool

    public init(kind: SessionKind, empty: Bool, firstLine: Bool, lastLine: Bool, passing: Bool, composing: Bool = false) {
        self.kind = kind
        self.empty = empty
        self.firstLine = firstLine
        self.lastLine = lastLine
        self.passing = passing
        self.composing = composing
    }
}

/// The keys of the editor (D-354, D-369): see EditorKeys.cs, the twin.
public enum EditorKeys {
    public static let escape = "\u{1B}"

    public static func press(_ state: EditorState, _ key: EditorKey, shift: Bool, character: Character? = nil) -> EditorPress {
        // A long text for Claude Code: nothing passes; Enter puts the text into its line, Esc closes.
        if state.composing {
            switch key {
            case .escape: return EditorPress(.hide, "", false)
            case .enter where shift: return EditorPress(.newLine, "", false)
            case .enter: return EditorPress(state.empty ? .hide : .insert, "", false)
            default: return EditorPress(.keep, "", false)
            }
        }

        if key == .escape {
            return EditorPress(.pass, escape, false)
        }

        if key == .interrupt {
            return state.empty || state.passing ? EditorPress(.pass, "\u{03}", false) : keep(state)
        }

        if state.passing && state.empty {
            return key == .enter ? EditorPress(.pass, "\r", false) : EditorPress(.pass, sequence(key, shift, character), true)
        }

        if state.empty {
            switch key {
            case .up where state.kind == .shell:
                return EditorPress(.historyBack, "", false)
            case .down where state.kind == .shell:
                return EditorPress(.historyForward, "", false)
            case .enter where shift:
                return EditorPress(.newLine, "", false)
            case .up, .down, .left, .right, .enter, .backspace:
                return EditorPress(.pass, sequence(key, shift, character), false)
            case .tab:
                return EditorPress(.pass, shift ? "\u{1B}[Z" : "\t", false)
            case .character where character.map { $0.isASCII && $0.isNumber } == true:
                return EditorPress(.pass, String(character!), false)
            case .character where character == "/":
                return EditorPress(.pass, "/", true)
            default:
                return keep(state)
            }
        }

        switch key {
        case .enter:
            return EditorPress(shift ? .newLine : .send, "", false)
        case .up where state.kind == .shell && state.firstLine:
            return EditorPress(.historyBack, "", false)
        case .down where state.kind == .shell && state.lastLine:
            return EditorPress(.historyForward, "", false)
        case .tab where !shift && state.kind == .shell:
            return EditorPress(.complete, "", false)
        default:
            return keep(state)
        }
    }

    private static func keep(_ state: EditorState) -> EditorPress {
        EditorPress(.keep, "", state.passing && state.empty)
    }

    private static func sequence(_ key: EditorKey, _ shift: Bool, _ character: Character?) -> String {
        switch key {
        case .up: return "\u{1B}[A"
        case .down: return "\u{1B}[B"
        case .right: return "\u{1B}[C"
        case .left: return "\u{1B}[D"
        case .enter: return "\r"
        case .backspace: return "\u{7F}"
        case .tab: return shift ? "\u{1B}[Z" : "\t"
        case .character: return character.map { String($0) } ?? ""
        default: return ""
        }
    }
}

/// What goes to the program when the editor sends (D-360): see EditorSend in EditorKeys.cs.
public enum EditorSend {
    public static let pasteStart = "\u{1B}[200~"
    public static let pasteEnd = "\u{1B}[201~"

    public static func writes(_ text: String, pictures: [String]) -> [String] {
        var writes = inserts(text, pictures: pictures)
        if !writes.isEmpty {
            writes.append("\r")
        }
        return writes
    }

    /// The same writes without the Enter: the text waits in the line of the program (D-370).
    public static func inserts(_ text: String, pictures: [String]) -> [String] {
        var writes = pictures.map { pasteStart + $0 + pasteEnd }

        // A terminal pastes a line break as a carriage return, as Windows Terminal does. "\r\n" is
        // one Character in Swift, so the units are worked on: every break becomes one "\r".
        let source = Array(text.utf16)
        var lines: [UInt16] = []
        var at = 0
        while at < source.count {
            let unit = source[at]
            if unit == 0x0D || unit == 0x0A {
                lines.append(0x0D)
                at += unit == 0x0D && at + 1 < source.count && source[at + 1] == 0x0A ? 2 : 1
                continue
            }
            lines.append(unit)
            at += 1
        }
        while lines.last == 0x0D {
            lines.removeLast()
        }
        if !lines.isEmpty {
            writes.append(pasteStart + String(decoding: lines, as: UTF16.self) + pasteEnd)
        }
        return writes
    }
}

/// The commands a command line got from the editor in this session: see CommandHistory in
/// EditorKeys.cs.
public struct CommandHistory: Sendable, Equatable {
    public let commands: [String]
    public let at: Int
    public let draft: String

    public static let empty = CommandHistory(commands: [], at: 0, draft: "")

    public init(commands: [String], at: Int, draft: String) {
        self.commands = commands
        self.at = at
        self.draft = draft
    }

    public func sent(_ command: String) -> CommandHistory {
        let line = command.trimmingCharacters(in: .whitespacesAndNewlines)
        let next = line.isEmpty || commands.last == line ? commands : commands + [line]
        return CommandHistory(commands: next, at: next.count, draft: "")
    }

    public func back(_ text: String) -> (CommandHistory, String?) {
        guard at > 0 else { return (self, nil) }
        let kept = at == commands.count ? text : draft
        return (CommandHistory(commands: commands, at: at - 1, draft: kept), commands[at - 1])
    }

    public func forward() -> (CommandHistory, String?) {
        guard at < commands.count else { return (self, nil) }
        let next = at + 1
        return (CommandHistory(commands: commands, at: next, draft: draft), next == commands.count ? draft : commands[next])
    }
}

/// One entry of a folder: its name and whether it is a folder.
public struct FolderEntry: Sendable, Equatable {
    public let name: String
    public let folder: Bool

    public init(_ name: String, _ folder: Bool) {
        self.name = name
        self.folder = folder
    }
}

/// What Tab did.
public struct Completion: Sendable, Equatable {
    public let text: String
    public let caret: Int
    public let choices: [String]
}

/// Tab in a command line (D-369): see PathCompletion in EditorKeys.cs. Places are UTF-16 units.
public enum PathCompletion {
    public static func complete(
        _ text: String, caret: Int, folder: String, separator: Character, list: (String) -> [FolderEntry]
    ) -> Completion? {
        let units = Array(text.utf16)
        let end = min(max(caret, 0), units.count)
        var start = end
        while start > 0, let scalar = Unicode.Scalar(units[start - 1]), !scalar.properties.isWhitespace, units[start - 1] != 0x22 {
            start -= 1
        }

        let word = String(decoding: units[start..<end], as: UTF16.self)
        let cut = word.lastIndex { $0 == "/" || $0 == "\\" }
        let typedFolder = cut.map { String(word[...$0]) } ?? ""
        let typedName = cut.map { String(word[word.index(after: $0)...]) } ?? word

        let tidySeparator: Character = folder.contains("\\") || typedFolder.contains("\\") ? "\\" : "/"
        let raw = typedFolder.isEmpty ? folder
            : isRooted(typedFolder) ? typedFolder
            : trimmedEnd(folder) + "/" + typedFolder
        let matches = list(tidy(raw, tidySeparator))
            .filter { $0.name.lowercased().hasPrefix(typedName.lowercased()) }
            .sorted { $0.name.lowercased() < $1.name.lowercased() }
        guard !matches.isEmpty else { return nil }

        let one = matches.count == 1
        let name = one ? matches[0].name : commonStart(matches.map(\.name), typedName.utf16.count)
        let path = typedFolder + name + (one && matches[0].folder ? String(separator) : "")
        var written = path.contains(" ") ? "\"" + path + "\"" : path
        if one && !matches[0].folder {
            written += " "
        }

        let choices = one ? [] : matches.map { $0.folder ? $0.name + String(separator) : $0.name }
        let before = String(decoding: units[..<start], as: UTF16.self)
        let after = String(decoding: units[end...], as: UTF16.self)
        return Completion(text: before + written + after, caret: start + written.utf16.count, choices: choices)
    }

    private static func isRooted(_ path: String) -> Bool {
        let units = Array(path.utf16)
        return path.hasPrefix("/") || path.hasPrefix("\\") || path.hasPrefix("~") || (units.count >= 2 && units[1] == 0x3A)
    }

    private static func trimmedEnd(_ path: String) -> String {
        var units = Array(path.utf16)
        while let last = units.last, last == 0x2F || last == 0x5C {
            units.removeLast()
        }
        return String(decoding: units, as: UTF16.self)
    }

    /// One separator all through, and none at the end, except after the root of a drive or "/".
    private static func tidy(_ path: String, _ separator: Character) -> String {
        let other: Character = separator == "\\" ? "/" : "\\"
        let same = String(path.map { $0 == other ? separator : $0 })
        var trimmed = same
        while trimmed.last == separator {
            trimmed.removeLast()
        }
        if trimmed.isEmpty {
            return String(same.prefix(1))
        }
        return trimmed.hasSuffix(":") ? trimmed + String(separator) : trimmed
    }

    /// The longest start the names share, keeping the case of the first one; never shorter than
    /// what was typed. UTF-16 units, as in C#.
    private static func commonStart(_ names: [String], _ typed: Int) -> String {
        let all = names.map { Array($0.utf16) }
        let lowered = names.map { Array($0.lowercased().utf16) }
        let length = all.map(\.count).min() ?? 0
        var common = 0
        while common < length, lowered.allSatisfy({ $0.count > common && $0[common] == lowered[0][common] }) {
            common += 1
        }
        let take = max(common, min(typed, all[0].count))
        return String(decoding: all[0][..<take], as: UTF16.self)
    }
}
