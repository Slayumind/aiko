import Foundation

/// The one line Aiko writes into the Claude Code settings file.
///
/// The bridge takes no arguments: it reads CLAUDE_CONFIG_DIR itself and names its file after that
/// folder. So the command is only the path to the bridge, in quotes because the install path
/// holds spaces.
///
/// Our line has to be recognised again later, and not only when it is character for character the
/// same. The install path changes on a reinstall, and the shell changes the moment Git appears on
/// a machine that had none. A line matched by exact text stops being ours after either, gets put
/// aside as if the user had written it, and the bridge is then asked to run a path that no longer
/// exists on every model answer. So the line is recognised by the program it points at.
public enum BridgeCommand {
    /// The bridge program without the suffix the system adds to program files.
    public static let programName = "Aiko.Bridge"

    /// `BridgeCommand.For` on Windows; `for` is a keyword in Swift.
    public static func forPath(_ bridgePath: String?, _ shell: ClaudeShell = .gitBash) -> String {
        let path = bridgePath?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if path.isEmpty {
            // An empty command is refused by the patch, which is what we want: better no line at
            // all than a status line that runs nothing.
            return ""
        }

        // A path that is already quoted stays as it is; quoting it twice would break the command.
        let quoted = path.hasPrefix("\"") && path.hasSuffix("\"") ? path : "\"\(path)\""

        // In PowerShell a quoted path on its own is just a string and nothing runs. The call
        // operator makes it a command. In bash and zsh the same "&" would break the line, so the
        // shells get different text (spike 2026-09-11 left this open).
        return shell == .powerShell ? "& \(quoted)" : quoted
    }

    /// The session start hook: the same program with one argument.
    public static func hookFor(_ bridgePath: String?, _ shell: ClaudeShell = .gitBash) -> String {
        let command = forPath(bridgePath, shell)
        return command.isEmpty ? command : command + " " + SessionReminder.argument
    }

    /// Whether this status line command runs our bridge, whatever path and shell it was written
    /// for. Only the program name is compared: everything else about the line is allowed to change.
    public static func isAiko(_ platform: PlatformConventions, _ command: String?) -> Bool {
        guard let path = executablePathIn(command) else {
            return false
        }

        return PathText.fileName(path)
            .caseInsensitiveCompare(platform.executableName(programName)) == .orderedSame
    }

    /// The program a status line command runs, with the PowerShell call operator and the quotes
    /// taken off. Nil when the command is empty or names no program.
    private static func executablePathIn(_ command: String?) -> String? {
        var text = command?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if text.isEmpty {
            return nil
        }

        if text.hasPrefix("& ") {
            text = String(text.dropFirst(2)).trimmingCharacters(in: .whitespaces)
        }

        if text.hasPrefix("\"") {
            let rest = text.dropFirst()
            guard let closing = rest.firstIndex(of: "\"") else { return nil }
            let inside = String(rest[rest.startIndex..<closing])
            return inside.isEmpty ? nil : inside
        }

        // An unquoted command ends at the first space. Our own line is always quoted, so this is
        // only here to read a line somebody wrote by hand.
        let head = text.components(separatedBy: " ").first ?? text
        return head.isEmpty ? nil : head
    }
}
