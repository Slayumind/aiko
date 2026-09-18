import Foundation

/// The shell Claude Code runs the status line with. On Windows it uses Git Bash, and falls back to
/// PowerShell only when Git Bash is not installed. On macOS it is always zsh.
public enum ClaudeShell: Sendable, Equatable {
    case gitBash
    case powerShell
    case zsh
}

/// A shell program and the arguments that go before the command it should run.
public struct ShellProgram: Sendable, Equatable {
    public let kind: ClaudeShell
    public let fileName: String
    public let argumentsBeforeCommand: [String]

    public init(_ kind: ClaudeShell, _ fileName: String, _ argumentsBeforeCommand: [String]) {
        self.kind = kind
        self.fileName = fileName
        self.argumentsBeforeCommand = argumentsBeforeCommand
    }
}

/// How the operating system names programs, spells a path, lists folders in PATH and runs a shell
/// command.
///
/// The core decides what to do; these small facts decide how it is spelled on one system. Each
/// program passes the description of the system it runs on, so the rules read the same in both
/// cores. The Mac app passes `.macOS`; the Windows cases of these tests pass `.windows`.
public struct PlatformConventions: Sendable, Equatable {
    /// Added to a program name to get its file name: ".exe" on Windows, nothing on macOS.
    public let executableSuffix: String

    /// Between the folders in PATH and in other folder lists: ';' on Windows, ':' on macOS.
    public let pathListSeparator: Character

    /// Between the parts of a path.
    public let directorySeparator: Character

    /// How a command line names the user's local data folder, so the command does not have to
    /// spell the path itself. Nil when the system has no such variable.
    public let localDataVariable: String?

    /// The shell Claude Code runs a status line with when Git Bash is not there.
    public let fallbackShell: ShellProgram

    public init(
        executableSuffix: String,
        pathListSeparator: Character,
        directorySeparator: Character,
        localDataVariable: String?,
        fallbackShell: ShellProgram
    ) {
        self.executableSuffix = executableSuffix
        self.pathListSeparator = pathListSeparator
        self.directorySeparator = directorySeparator
        self.localDataVariable = localDataVariable
        self.fallbackShell = fallbackShell
    }

    public static let windows = PlatformConventions(
        executableSuffix: ".exe",
        pathListSeparator: ";",
        directorySeparator: "\\",
        // cmd.exe expands it when Claude Code runs the command.
        localDataVariable: "%LOCALAPPDATA%",
        fallbackShell: ShellProgram(
            .powerShell, "powershell.exe", ["-NoProfile", "-NonInteractive", "-Command"]))

    /// macOS has no Git Bash and no %VARIABLE% in a command, so a command spells the real path.
    /// zsh is the login shell since Catalina, and -l makes it read the profile the person's own
    /// tools are on.
    public static let macOS = PlatformConventions(
        executableSuffix: "",
        pathListSeparator: ":",
        directorySeparator: "/",
        localDataVariable: nil,
        fallbackShell: ShellProgram(.zsh, "/bin/zsh", ["-lc"]))

    /// "claude" becomes "claude.exe" on Windows and stays "claude" on macOS.
    public func executableName(_ programName: String) -> String { programName + executableSuffix }

    /// Joins the parts of a path with the separator of this system. A part that already ends with
    /// a separator does not get a second one.
    public func join(_ first: String, _ rest: String...) -> String {
        join(first, rest)
    }

    public func join(_ first: String, _ rest: [String]) -> String {
        var path = first
        for part in rest {
            while path.count > 0, path.last == directorySeparator {
                path.removeLast()
            }
            path.append(directorySeparator)
            path += part
        }
        return path
    }

}

/// Reading a path without asking the machine the code runs on.
///
/// The Windows core gets these from Path, whose answers follow the running system: Path.GetFileName
/// splits on both separators on Windows and on one on macOS. Aiko reads paths written for the other
/// system all the time, so these split on both everywhere.
enum PathText {
    static let separators: Set<Character> = ["/", "\\"]

    /// Every trailing separator, the way `TrimEnd(Separators)` cuts one in the Windows core.
    static func trimEndSeparators(_ path: String) -> String {
        var text = path
        while let last = text.last, separators.contains(last) {
            text.removeLast()
        }
        return text
    }

    /// One trailing separator, and never the one a root needs, like Path.TrimEndingDirectorySeparator.
    /// "C:\\" and "/" are not the same folder as "C:" and "".
    static func trimOneEndSeparator(_ path: String) -> String {
        guard let last = path.last, separators.contains(last), path.count > 1 else {
            return path
        }
        if path.count == 3, Array(path)[1] == ":" {
            return path
        }
        return String(path.dropLast())
    }

    /// The last part of a path, like Path.GetFileName on Windows.
    static func fileName(_ path: String) -> String {
        guard let index = path.lastIndex(where: { separators.contains($0) }) else {
            return path
        }
        return String(path[path.index(after: index)...])
    }

    /// The last part with its extension cut off, like Path.GetFileNameWithoutExtension.
    static func fileNameWithoutExtension(_ path: String) -> String {
        let name = fileName(path)
        guard let dot = name.lastIndex(of: "."), dot != name.startIndex else {
            return name
        }
        return String(name[name.startIndex..<dot])
    }
}

/// How to call a status line command with the shell Claude Code uses.
///
/// Both the app and the bridge need this. The app needs it when it writes our line, because the
/// shells need different text. The bridge needs it when it runs the line the user already had:
/// that line was written for Claude Code's shell, and running it through another one makes a
/// working line print nothing at all.
///
/// Git Bash is a Windows idea, so on macOS the path is always nil and the shell is always the
/// fallback one.
public enum ClaudeShellLookup {
    public static func shellFor(_ platform: PlatformConventions, _ gitBashPath: String?) -> ClaudeShell {
        gitBashPath == nil ? platform.fallbackShell.kind : .gitBash
    }

    /// How to run a command that was written for Claude Code's shell.
    ///
    /// Arguments are handed over as a list rather than glued into one string: the command is
    /// somebody else's text and may hold quotes, and escaping it by hand is a bug waiting to be
    /// written.
    public static func callFor(
        _ platform: PlatformConventions, _ gitBashPath: String?, _ command: String
    ) -> (fileName: String, arguments: [String]) {
        guard let gitBashPath else {
            return (platform.fallbackShell.fileName, platform.fallbackShell.argumentsBeforeCommand + [command])
        }
        return (gitBashPath, ["-c", command])
    }
}
