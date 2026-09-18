import Foundation

public enum CommandProblem: Sendable, Equatable {
    case none
    case empty
    case tooLong
    case badCharacters
    case reserved
    case taken
}

/// The name of the command that starts Claude Code in one environment: "aiko-work".
///
/// Until the person types a name of their own, the command follows the environment name, so
/// renaming Work to Job makes it aiko-job. The command is a file in PATH, which is why the name
/// is plain latin letters, digits and hyphens: it has to be typed in every shell alike.
public enum LaunchCommand {
    public static let prefix = "aiko-"
    public static let maxLength = 40

    public static func fromEnvironmentName(_ environmentName: String) -> String {
        let text = slug(environmentName)
        return prefix + (text.isEmpty ? "env" : text)
    }

    public static func check(_ command: String, _ takenByOthers: [String]) -> CommandProblem {
        if command.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return .empty
        }

        if command.count > maxLength {
            return .tooLong
        }

        if !command.allSatisfy(isAllowed) || command.first == "-" {
            return .badCharacters
        }

        // The shim itself is named claude. A command with that name would start itself forever.
        if command.caseInsensitiveCompare(ShimLaunch.claudeName) == .orderedSame {
            return .reserved
        }

        return takenByOthers.contains(where: { $0.caseInsensitiveCompare(command) == .orderedSame })
            ? .taken
            : .none
    }

    private static func isAllowed(_ c: Character) -> Bool {
        ("a"..."z").contains(c) || ("0"..."9").contains(c) || c == "-" || c == "_"
    }

    /// Latin letters stay, Russian letters are spelled in latin, everything else becomes one hyphen.
    /// Also names the folder of a new environment, so a command and its folder read the same.
    public static func slug(_ name: String) -> String {
        var builder = ""
        for c in name.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() {
            if ("a"..."z").contains(c) || ("0"..."9").contains(c) {
                builder.append(c)
            } else if let latin = cyrillic[c] {
                builder += latin
            } else if !builder.isEmpty && builder.last != "-" {
                builder.append("-")
            }
        }

        var text = builder
        while text.first == "-" { text.removeFirst() }
        while text.last == "-" { text.removeLast() }

        if text.count > maxLength - prefix.count {
            text = String(text.prefix(maxLength - prefix.count))
            while text.last == "-" { text.removeLast() }
        }

        return text
    }

    private static let cyrillic: [Character: String] = [
        "а": "a", "б": "b", "в": "v", "г": "g", "д": "d", "е": "e", "ё": "e", "ж": "zh",
        "з": "z", "и": "i", "й": "y", "к": "k", "л": "l", "м": "m", "н": "n", "о": "o",
        "п": "p", "р": "r", "с": "s", "т": "t", "у": "u", "ф": "f", "х": "kh", "ц": "ts",
        "ч": "ch", "ш": "sh", "щ": "shch", "ъ": "", "ы": "y", "ь": "", "э": "e",
        "ю": "yu", "я": "ya",
    ]
}
