import Foundation

/// What "!" at the start of the field means: a command for the shell, run by Aiko in the folder of
/// the session (D-343). Claude Code does not understand "!" through its stream, so Aiko runs the
/// command itself and sends Claude what it printed.
public enum ShellCommand {
    /// The most of an output Claude gets, as Claude Code cuts the output of a command too.
    public static let mostChars = 30000

    private static let inputTag = "bash-input"
    private static let outputTag = "bash-stdout"
    private static let errorTag = "bash-stderr"

    /// The command typed after "!", or nil when the text is not one.
    public static func of(_ text: String) -> String? {
        let trimmed = text.drop(while: { $0.isWhitespace })
        guard trimmed.hasPrefix("!") else { return nil }
        let command = trimmed.dropFirst().trimmingCharacters(in: .whitespacesAndNewlines)
        return command.isEmpty ? nil : command
    }

    /// The message Claude gets. It has the tags Claude Code itself writes for a command run with
    /// "!", so Claude reads it the same way, and the file of the conversation reads back into the
    /// same feed. A command that failed says its exit code in the place of the errors.
    public static func message(_ command: String, output: String, exitCode: Int) -> String {
        // Counted in UTF-16 units, as C# counts the characters of a string.
        let units = Array(output.utf16)
        let shown = units.count > mostChars
            ? (String(utf16CodeUnits: Array(units[0..<mostChars]), count: mostChars) + "\n…")
            : output
        let failed = exitCode == 0 ? "" : "Exit code \(exitCode)"
        return "<\(inputTag)>\(command)</\(inputTag)>\n<\(outputTag)>\(trimEnd(shown))</\(outputTag)><\(errorTag)>\(failed)</\(errorTag)>"
    }

    /// A message of a person in the file of a conversation that is a command of "!": its command,
    /// and its output when the same message has it. Claude Code writes the command and its output
    /// as two messages; Aiko writes them as one.
    public static func read(_ text: String) -> (command: String, output: String?, exitCode: Int)? {
        guard let command = tagged(text, inputTag) else { return nil }
        return (command, output(text), exitCodeIn(tagged(text, errorTag)))
    }

    /// Aiko writes the exit code of a command that failed in the place of the errors.
    private static func exitCodeIn(_ errors: String?) -> Int {
        guard let errors, errors.hasPrefix("Exit code ") else { return 0 }
        return Int(errors.dropFirst("Exit code ".count)) ?? 0
    }

    /// The output alone: the second message Claude Code writes for a command of "!".
    public static func output(_ text: String) -> String? {
        let printed = tagged(text, outputTag)
        var errors = tagged(text, errorTag)
        if printed == nil && errors == nil {
            return nil
        }

        // The exit code Aiko wrote there is not output: the feed shows it on a line of its own.
        if exitCodeIn(errors) != 0 {
            errors = nil
        }
        return [printed, errors].compactMap { $0 }.filter { !$0.isEmpty }.joined(separator: "\n")
    }

    private static func tagged(_ text: String, _ tag: String) -> String? {
        guard let open = text.range(of: "<\(tag)>"), let close = text.range(of: "</\(tag)>"),
              open.upperBound <= close.lowerBound else { return nil }
        return String(text[open.upperBound..<close.lowerBound]).trimmingCharacters(in: CharacterSet(charactersIn: "\r\n"))
    }

    private static func trimEnd(_ text: String) -> String {
        var end = text.endIndex
        while end > text.startIndex, text[text.index(before: end)].isWhitespace {
            end = text.index(before: end)
        }
        return String(text[..<end])
    }
}
