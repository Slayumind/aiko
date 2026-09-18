import Foundation

/// Finds the real Claude Code program further along PATH, never another copy of the shim.
///
/// The spike found the trap: with two shim folders in PATH, say an old install and a new one,
/// each shim took the other for Claude Code and they started each other until there were
/// thousands of processes. So every shim adds its own folder to a variable that its children
/// inherit, and no shim ever runs Claude Code from a folder already in that list.
public enum RealClaude {
    public static let seenVariable = "AIKO_SHIM_SEEN"

    /// More shims than this in one chain is a loop that got past the list somehow. Stop.
    public static let maxChain = 8

    /// Claude Code's program file: claude.exe on Windows, claude on macOS.
    public static func executableName(_ platform: PlatformConventions) -> String {
        platform.executableName(ShimLaunch.claudeName)
    }

    public static func parseSeen(_ platform: PlatformConventions, _ value: String?) -> [String] {
        (value ?? "")
            .split(separator: platform.pathListSeparator, omittingEmptySubsequences: true)
            .map { $0.trimmingCharacters(in: .whitespaces) }
            .filter { !$0.isEmpty }
    }

    public static func formatSeen(_ platform: PlatformConventions, _ folders: [String]) -> String {
        folders.joined(separator: String(platform.pathListSeparator))
    }

    public static func find(
        _ platform: PlatformConventions,
        _ pathVariable: String?,
        _ seenFolders: [String],
        _ fileExists: (String) -> Bool
    ) -> String? {
        if seenFolders.count > maxChain {
            return nil
        }

        for entry in (pathVariable ?? "").split(separator: platform.pathListSeparator, omittingEmptySubsequences: true) {
            var folder = entry.trimmingCharacters(in: .whitespaces)
            while folder.first == "\"" { folder.removeFirst() }
            while folder.last == "\"" { folder.removeLast() }

            if folder.isEmpty || seenFolders.contains(where: { sameFolder($0, folder) }) {
                continue
            }

            let candidate = platform.join(folder, executableName(platform))
            if fileExists(candidate) {
                return candidate
            }
        }

        return nil
    }

    /// Both separators are trimmed, whatever system the path was written for: a folder list can
    /// hold either, and the answer must not depend on the machine reading it.
    public static func sameFolder(_ a: String, _ b: String) -> Bool {
        trimEnd(a).caseInsensitiveCompare(trimEnd(b)) == .orderedSame
    }

    private static func trimEnd(_ path: String) -> String {
        PathText.trimOneEndSeparator(path.trimmingCharacters(in: .whitespaces))
    }
}
