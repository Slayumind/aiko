import Foundation

/// The PATH a person has in their console, asked from a login shell.
///
/// An app started from Finder or at login gets the short PATH of the system. Claude Code started
/// with it does not find node, Homebrew or anything else the person installed, so its tools fail
/// where the same command works in a console. Windows has no twin: there a program gets the PATH
/// of the user from the system.
public enum LoginShellPath {
    /// The line with the answer starts with this. A shell may print anything else before it: a
    /// greeting, a warning from a profile.
    static let marker = "__AIKO_PATH__"

    /// What to pass to the shell. -l reads the profile of a login, -i reads the one of a console:
    /// people put their PATH in either.
    public static let arguments = ["-lic", "echo \(marker)$PATH"]

    /// The PATH in what the shell printed, or nil when it is not there. Nil keeps the PATH of Aiko.
    public static func parse(_ output: String) -> String? {
        let lines = output.split(whereSeparator: \.isNewline)
        guard let line = lines.last(where: { $0.contains(marker) }),
              let range = line.range(of: marker)
        else {
            return nil
        }

        let path = line[range.upperBound...].trimmingCharacters(in: .whitespaces)
        return path.split(separator: ":").contains { $0.hasPrefix("/") } ? path : nil
    }
}
