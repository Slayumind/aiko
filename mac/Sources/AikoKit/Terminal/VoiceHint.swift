import Foundation

/// What the recogniser is told before it listens (D-313): the folder of the session, its branch,
/// the names of files from the conversation and the keys of Aiko's memory. These are the names a
/// person spells their own way, the words recognition gets wrong first. The facts of the memory
/// never go, only their keys.
public enum VoiceHint {
    /// Whisper reads about 220 tokens of a prompt; OpenAI reads a similar amount. 400 characters
    /// stays inside both for Russian and English.
    public static let maxLength = 400

    /// Words of the terminal a person says about their work, in the language Whisper hears. They
    /// come right after the folder and the branch, so a long list of files never cuts them (AIKO-311).
    private static let terminalWords: [String: [String]] = [
        "ru": ["Enter", "Esc", "поле ввода", "сессия", "отрисовано"],
        "en": ["Enter", "Esc", "input field", "session", "rendered"],
    ]

    /// files are paths or names; only the name is kept. memoryKeys is nil when the memory is off.
    /// language is "ru" or "en", or nil for no words of the terminal.
    public static func build(
        folder: String?,
        branch: String?,
        files: [String],
        memoryKeys: [String]?,
        language: String? = nil,
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
        (language.flatMap { terminalWords[$0] } ?? []).forEach { add($0) }
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

    /// Whether a phrase is the hint given back. A recogniser that does not catch the sound may
    /// answer with its prompt: on 2026-10-15 OpenAI wrote the whole hint for an English sentence.
    /// A phrase of three or more parts, every one an item of the hint, is that.
    public static func isEcho(_ phrase: String, hint: String) -> Bool {
        let items = Set(parts(hint))
        let said = parts(phrase)
        return !items.isEmpty && said.count >= 3 && said.allSatisfy(items.contains)
    }

    private static func parts(_ text: String) -> [String] {
        text.split(separator: ",").compactMap { part in
            var trimmed = part.trimmingCharacters(in: .whitespaces)
            while trimmed.hasSuffix(".") { trimmed.removeLast() }
            let clean = trimmed.trimmingCharacters(in: .whitespaces).lowercased().replacingOccurrences(of: "ё", with: "е")
            return clean.isEmpty ? nil : clean
        }
    }

    /// The files a conversation read or changed, the newest first, each once. The newest go first
    /// because the hint is cut from the end.
    public static func files(_ items: [FeedItem]) -> [String] {
        var seen = Set<String>()
        var files: [String] = []
        for item in items.reversed() {
            guard case .step(let step) = item.content,
                  step.line.kind == .read || step.line.kind == .edit,
                  !step.line.hidden, !step.line.subject.isEmpty,
                  seen.insert(step.line.subject.lowercased()).inserted else { continue }
            files.append(step.line.subject)
        }
        return files
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
