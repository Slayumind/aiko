import Foundation

/// What a command line asks Claude Code (D-358, D-360): to explain a failed command, and to turn words
/// into a command. Both go through claude -p in the limits of the environment; this only writes the
/// questions and reads the answers. Rows: spec/cases/shell-ask. The twin of ShellAsk.cs.
public enum ShellAsk {
    /// The most of an output that goes into a question: its end, where the error usually is.
    public static let mostOutput = 6000

    /// Words that ask for a command rather than run one: only a line that starts with "#" (D-420).
    public static func isRequest(_ text: String) -> Bool {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.hasPrefix("#") && requestText(trimmed).utf16.count >= 2
    }

    /// The request without the "#" that marked it.
    public static func requestText(_ text: String) -> String {
        var rest = Substring(text.trimmingCharacters(in: .whitespacesAndNewlines))
        while rest.first == "#" { rest = rest.dropFirst() }
        return rest.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    /// The last part of a folder path, with either kind of slash: "/Users/me/aiko" is "aiko".
    public static func folderName(_ folder: String) -> String {
        var trimmed = Substring(folder)
        while let last = trimmed.last, last == "\\" || last == "/" { trimmed = trimmed.dropLast() }
        guard let cut = trimmed.lastIndex(where: { $0 == "\\" || $0 == "/" }) else {
            return String(trimmed)
        }
        return String(trimmed[trimmed.index(after: cut)...])
    }

    public static func explain(shell: String, command: String, exitCode: Int, output: String, language: String) -> String {
        let tail = output.count > mostOutput ? String(output.suffix(mostOutput)) : output
        return """
            A command failed in \(place(shell)).
            Command: \(command)
            Exit code: \(exitCode)
            Its output (the end of it):
            \(tail)

            In \(language), say in two to four short sentences why it failed and what to do. Plain text only,
            no Markdown, no code blocks. If a fixed command helps, put it on a line of its own.
            """
    }

    /// folder: the folder of the session. Only its name goes into the question, not the path above it
    /// (D-420): the path names the person and their other folders.
    public static func suggest(shell: String, folder: String, request: String) -> String {
        """
        Write one command for \(place(shell)), in the current folder (\(folderName(folder))), that does this: \(requestText(request))
        Answer with the command only: one line, no explanation, no Markdown, no code block.
        """
    }

    /// The command in an answer: the first line that is not a fence of a code block, without the
    /// marks a model puts around a command now and then.
    public static func commandIn(_ answer: String) -> String? {
        for raw in answer.split(separator: "\n", omittingEmptySubsequences: false) {
            var line = raw.trimmingCharacters(in: .whitespacesAndNewlines)
            if line.isEmpty || line.hasPrefix("```") {
                continue
            }

            line = line.trimmingCharacters(in: CharacterSet(charactersIn: "`")).trimmingCharacters(in: .whitespacesAndNewlines)
            for prompt in ["$ ", "> ", "PS> "] where line.hasPrefix(prompt) {
                line = String(line.dropFirst(prompt.count)).trimmingCharacters(in: .whitespacesAndNewlines)
            }

            return line.isEmpty ? nil : line
        }

        return nil
    }

    /// The lines of an explanation, each broken to fit the width in characters; a long word is cut.
    public static func wrap(_ text: String, width: Int) -> [String] {
        let width = max(10, width)
        var lines: [String] = []
        let paragraphs = text.replacingOccurrences(of: "\r", with: "")
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .split(separator: "\n", omittingEmptySubsequences: false)
        for paragraph in paragraphs {
            var line = ""
            for word in paragraph.split(separator: " ") {
                var piece = String(word)
                while piece.count > width {
                    if !line.isEmpty {
                        lines.append(line)
                        line = ""
                    }
                    lines.append(String(piece.prefix(width)))
                    piece = String(piece.dropFirst(width))
                }

                if line.isEmpty {
                    line = piece
                } else if line.count + 1 + piece.count <= width {
                    line += " " + piece
                } else {
                    lines.append(line)
                    line = piece
                }
            }
            lines.append(line)
        }

        return lines
    }

    /// The shell and the system it runs on, as the question names them.
    static func place(_ shell: String) -> String {
        switch shell {
        case "bash": return "Git Bash on Windows"
        case "pwsh": return "PowerShell 7 on Windows"
        case "zsh": return "zsh on macOS"
        default: return "Windows PowerShell on Windows"
        }
    }
}

/// The address or the file under the mouse in a line of output, for Cmd+click (D-358): an http(s)
/// address, or words that look like a file with an optional line ("src/a.swift:42"). The column
/// counts UTF-16 units, as on Windows. Rows: spec/cases/shell-ask. The twin of ShellLink.cs.
public enum ShellLink {
    private static let stops = Set(" \t\"'()[]<>,{}".utf16)

    public static func at(_ line: String, column: Int) -> String? {
        let units = Array(line.utf16)
        guard column >= 0, column < units.count, !stops.contains(units[column]) else {
            return nil
        }

        var start = column
        while start > 0 && !stops.contains(units[start - 1]) { start -= 1 }
        var end = column + 1
        while end < units.count && !stops.contains(units[end]) { end += 1 }

        var word = String(decoding: units[start..<end], as: UTF16.self)
        while let last = word.last, ".,;!?:".contains(last) { word.removeLast() }

        let lower = word.lowercased()
        if lower.hasPrefix("http://") || lower.hasPrefix("https://") {
            return word.utf16.count > 8 ? word : nil
        }

        return FileLink.parse(word) != nil ? word : nil
    }
}
