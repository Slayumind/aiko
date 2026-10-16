import Foundation

/// A command Claude Code knows: a built-in one, a skill or a command of a plugin. Claude Code gives
/// them with descriptions in its answer to "initialize" (spike of 2026-10-16, RESEARCH).
public struct SlashCommand: Sendable, Equatable {
    public let name: String
    public let description: String
    public let argumentHint: String
    public let aliases: [String]
    public let builtin: Bool

    public init(name: String, description: String, argumentHint: String, aliases: [String], builtin: Bool) {
        self.name = name
        self.description = description
        self.argumentHint = argumentHint
        self.aliases = aliases
        self.builtin = builtin
    }
}

/// One line of the menu: the command and the part of its name that matches what was typed.
/// matchLength is 0 when nothing was typed after "/", or when only an alias matched.
public struct SlashMatch: Sendable, Equatable {
    public let command: SlashCommand
    public let matchStart: Int
    public let matchLength: Int
}

/// The menu of commands that "/" opens at the start of the field (AIKO-330). The twin of
/// SlashMenu.cs; positions are in UTF-16 units, as in C#.
public enum SlashMenu {
    /// Commands for the screen of Claude Code itself, which do nothing in a conversation of Aiko:
    /// terminal_slash_commands of its init (2.1.301).
    private static let terminalOnly: Set<String> = ["doctor", "color", "focus", "reload-plugins"]

    /// What was typed after "/", while the caret is still in the first word; nil when the menu is
    /// closed: no "/" at the very start, a space before the caret, or the caret before "/".
    public static func query(_ text: String, caret: Int) -> String? {
        let units = Array(text.utf16)
        guard units.first == 47, caret >= 1, caret <= units.count else { return nil }
        let typed = String(decoding: units[1..<caret], as: UTF16.self)
        return typed.contains(where: \.isWhitespace) ? nil : typed
    }

    /// The commands to show for what was typed, best first. Hidden ones never show. A name that
    /// starts with the query goes first, then a name with a part that starts with it ("copy" in
    /// "aiko:copy"), then an alias, then a name that only has it inside. Built-in commands go
    /// before skills inside each group, and names go in the order of the alphabet.
    public static func matches(_ commands: [SlashCommand], query: String) -> [SlashMatch] {
        commands
            .filter { !isHidden($0.name) }
            .compactMap { command in rank(command, query).map { (match: $0.match, order: $0.order) } }
            .sorted { left, right in
                if left.order != right.order { return left.order < right.order }
                if left.match.command.builtin != right.match.command.builtin { return left.match.command.builtin }
                return left.match.command.name.lowercased().utf16.lexicographicallyPrecedes(right.match.command.name.lowercased().utf16)
            }
            .map(\.match)
    }

    /// The text the field gets when a command is chosen: the command and a space for its
    /// arguments.
    public static func chosen(_ command: SlashCommand) -> String {
        "/" + command.name + " "
    }

    private static func isHidden(_ name: String) -> Bool {
        name.hasPrefix("__") || terminalOnly.contains(name)
    }

    private static func rank(_ command: SlashCommand, _ query: String) -> (match: SlashMatch, order: Int)? {
        let name = Array(command.name.lowercased().utf16)
        let wanted = Array(query.lowercased().utf16)
        if wanted.isEmpty {
            return (SlashMatch(command: command, matchStart: 0, matchLength: 0), 0)
        }

        func starts(at: Int) -> Bool {
            at + wanted.count <= name.count && Array(name[at..<(at + wanted.count)]) == wanted
        }

        if starts(at: 0) {
            return (SlashMatch(command: command, matchStart: 0, matchLength: wanted.count), 0)
        }

        // ":" "-" "_" in UTF-16.
        for at in 1..<max(1, name.count) where [58, 45, 95].contains(name[at - 1]) && starts(at: at) {
            return (SlashMatch(command: command, matchStart: at, matchLength: wanted.count), 1)
        }

        if command.aliases.contains(where: { $0.lowercased().hasPrefix(query.lowercased()) }) {
            return (SlashMatch(command: command, matchStart: 0, matchLength: 0), 2)
        }

        for at in 0..<name.count where starts(at: at) {
            return (SlashMatch(command: command, matchStart: at, matchLength: wanted.count), 3)
        }
        return nil
    }
}
