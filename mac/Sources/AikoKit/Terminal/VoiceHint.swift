import Foundation

/// What the recogniser is told before it listens (D-313): the folder of the session, its branch,
/// the names of files from the conversation and the keys of Aiko's memory. These are the names a
/// person spells their own way, the words recognition gets wrong first. The facts of the memory
/// never go, only their keys.
public enum VoiceHint {
    /// Whisper reads about 220 tokens of a prompt; OpenAI reads a similar amount. 400 characters
    /// stays inside both for Russian and English.
    public static let maxLength = 400

    /// files are paths or names; only the name is kept. memoryKeys is nil when the memory is off.
    public static func build(
        folder: String?,
        branch: String?,
        files: [String],
        memoryKeys: [String]?,
        maxLength: Int = maxLength
    ) -> String {
        var items: [String] = []
        var seen = Set<String>()

        func add(_ item: String?) {
            guard let text = item?.trimmingCharacters(in: .whitespaces), !text.isEmpty,
                  seen.insert(text.lowercased()).inserted else { return }
            items.append(text)
        }

        add(lastPart(folder))
        add(branch)
        files.forEach { add(lastPart($0)) }
        (memoryKeys ?? []).forEach { add($0) }

        var hint = ""
        for item in items {
            let longer = hint.isEmpty ? item : "\(hint), \(item)"
            if longer.count + 1 > maxLength { break }
            hint = longer
        }

        return hint.isEmpty ? "" : hint + "."
    }

    /// The language the recogniser is told to hear: the language of Aiko's interface (D-314).
    /// Automatic detection was worse and slower in the spike.
    public static func language(_ setting: AikoLanguage, systemLanguage: String) -> String {
        switch setting {
        case .russian: return "ru"
        case .english: return "en"
        case .system: return systemLanguage.lowercased().hasPrefix("ru") ? "ru" : "en"
        }
    }

    /// The branch a .git/HEAD file names: "ref: refs/heads/feature/voice" is feature/voice. A HEAD
    /// that holds a commit (a detached checkout) names no branch.
    public static func branch(_ head: String?) -> String? {
        let prefix = "ref: refs/heads/"
        guard let line = head?.trimmingCharacters(in: .whitespacesAndNewlines),
              line.hasPrefix(prefix), line.count > prefix.count else { return nil }
        return String(line.dropFirst(prefix.count))
    }

    /// In a worktree .git is a file, "gitdir: <folder>", and HEAD sits in that folder.
    public static func gitFolder(_ dotGitFile: String?) -> String? {
        let prefix = "gitdir:"
        guard let line = dotGitFile?.trimmingCharacters(in: .whitespacesAndNewlines),
              line.hasPrefix(prefix), line.count > prefix.count else { return nil }
        return line.dropFirst(prefix.count).trimmingCharacters(in: .whitespaces)
    }

    /// The last part of a path written on either system: C:\work\Aiko\, /Users/me/Aiko.
    private static func lastPart(_ path: String?) -> String? {
        guard let path else { return nil }
        var trimmed = Substring(path)
        while let last = trimmed.last, last == "\\" || last == "/" { trimmed = trimmed.dropLast() }
        return trimmed.split(separator: "/", omittingEmptySubsequences: false)
            .last.map { $0.split(separator: "\\", omittingEmptySubsequences: false).last.map(String.init) ?? "" }
    }
}
