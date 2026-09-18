import Foundation

/// Aiko's block in the person's zsh profile.
///
/// Windows puts the command folder into the user PATH in the registry, and every new process sees
/// it. macOS has no such place: `path_helper` only reads /etc/paths and /etc/paths.d, which are the
/// system's, and a folder under the home directory reaches the PATH of a shell only if the shell's
/// own profile puts it there (spike S4). So Aiko writes one block into ~/.zshrc, asks for that in
/// the checklist exactly as the Windows wizard asks before touching PATH, and takes the block out
/// again on removal.
///
/// The block is marked at both ends so it can be found and removed whole, whatever the person put
/// around it. Nothing else in the file is ever read or changed.
public enum ZshProfile {
    public static let fileName = ".zshrc"

    public static let startMark = "# Aiko: launch commands"
    public static let endMark = "# end Aiko"

    /// The line Aiko writes, shown in the checklist before it is written.
    public static func exportLine(_ commandsFolder: String) -> String {
        "export PATH=\(quote(commandsFolder)):$PATH"
    }

    public static func block(_ commandsFolder: String) -> String {
        blockLines(commandsFolder).joined(separator: "\n")
    }

    public static func has(_ text: String) -> Bool {
        blockRange(lines(of: text)) != nil
    }

    /// Whether the block is there and points at this folder.
    public static func holds(_ text: String, _ commandsFolder: String) -> Bool {
        let all = lines(of: text)
        guard let range = blockRange(all) else { return false }
        return Array(all[range]) == blockLines(commandsFolder)
    }

    /// The profile with Aiko's block in it, or nil when there is nothing to change. A block that
    /// points somewhere else is rewritten in place, so a reinstall does not leave two of them.
    public static func add(_ text: String, _ commandsFolder: String) -> String? {
        var all = lines(of: text)
        let wanted = blockLines(commandsFolder)

        if let range = blockRange(all) {
            if Array(all[range]) == wanted { return nil }
            all.replaceSubrange(range, with: wanted)
            return all.joined(separator: "\n")
        }

        if all.isEmpty {
            return (wanted + [""]).joined(separator: "\n")
        }

        // The empty last element is the file's own final newline; the block after it becomes one
        // blank line and then the three lines of ours.
        if all.last != "" { all.append("") }
        all.append(contentsOf: wanted)
        all.append("")
        return all.joined(separator: "\n")
    }

    /// The profile without Aiko's block, or nil when there is none. The blank line Aiko put in
    /// front of the block goes with it, so adding and removing leaves the file as it was.
    public static func remove(_ text: String) -> String? {
        var all = lines(of: text)
        guard let range = blockRange(all) else { return nil }

        all.removeSubrange(range)

        let before = range.lowerBound - 1
        if before >= 1, before < all.count, all[before] == "" {
            all.remove(at: before)
        }

        return all.joined(separator: "\n")
    }

    private static func blockLines(_ commandsFolder: String) -> [String] {
        [startMark, exportLine(commandsFolder), endMark]
    }

    /// An empty file has no lines at all; every other text keeps its trailing newline as a last
    /// empty element, so joining the lines back gives exactly the same text.
    private static func lines(of text: String) -> [String] {
        text.isEmpty ? [] : text.components(separatedBy: "\n")
    }

    private static func blockRange(_ all: [String]) -> Range<Int>? {
        guard let start = all.firstIndex(where: { trimmed($0) == startMark }) else { return nil }
        guard let end = all[start...].firstIndex(where: { trimmed($0) == endMark }) else { return nil }
        return start..<(end + 1)
    }

    private static func trimmed(_ line: String) -> String {
        line.trimmingCharacters(in: .whitespaces)
    }

    /// Single quotes, because a path may hold anything but a quote of its own, and a quote inside
    /// is closed, escaped and opened again the way a shell wants it.
    static func quote(_ text: String) -> String {
        "'" + text.replacingOccurrences(of: "'", with: "'\\''") + "'"
    }
}

/// Opening Claude Code in a Terminal window.
///
/// Windows starts claude.exe and gets a console window for free. macOS has no such thing: a GUI
/// app that starts a program gets no terminal at all. Telling Terminal what to run by AppleScript
/// needs the Automation permission and a prompt nobody asked for, so Aiko writes a small script of
/// its own and opens that: `open` hands the file to Terminal, Terminal runs it in a new window,
/// and no extra permission is involved.
public enum MacTerminal {
    public static let scriptName = "open-claude.command"

    /// The script for one environment. Environment 1 is the plain .claude folder, and it is used
    /// the way a plain `claude` would use it: by leaving the variable out, never by pointing it
    /// at the folder.
    public static func script(
        claude: String, configFolder: String, workingDirectory: String, isDefaultFolder: Bool
    ) -> String {
        let variable = isDefaultFolder
            ? "unset \(ClaudeConfigFolder.variableName)"
            : "export \(ClaudeConfigFolder.variableName)=\(ZshProfile.quote(configFolder))"

        return """
            #!/bin/zsh
            # Written by Aiko every time it opens Claude Code. Safe to delete.
            cd \(ZshProfile.quote(workingDirectory)) || cd "$HOME"
            \(variable)
            exec \(ZshProfile.quote(claude))

            """
    }
}

/// How Claude Code is installed on each system. The checklist shows the command and waits.
public enum ClaudeCodeInstall {
    public static func command(_ platform: PlatformConventions) -> String {
        platform == .windows
            ? "irm https://claude.ai/install.ps1 | iex"
            : "curl -fsSL https://claude.ai/install.sh | bash"
    }
}
