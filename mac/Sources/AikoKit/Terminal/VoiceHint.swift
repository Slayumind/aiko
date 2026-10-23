import Foundation

/// A sentence cut out as the hint given back: how many parts it had and how many of them were items
/// of the hint. The twin of EchoCut in VoiceHint.cs.
public struct EchoCut: Sendable, Equatable {
    public let parts: Int
    public let inHint: Int
}

/// A phrase without the hint given back. lookAlikes: sentences kept although three or more of
/// their parts are items of the hint. The twin of Unechoed in VoiceHint.cs.
public struct Unechoed: Sendable, Equatable {
    public let text: String
    public let dropped: [EchoCut]
    public let lookAlikes: Int
}

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

    /// Whether a sentence is the hint given back. A recogniser that does not catch the sound may
    /// answer with its prompt: on 2026-10-15 OpenAI wrote the whole hint for an English sentence.
    /// A sentence of three or more parts, every one an item of the hint, is that. So is one of four
    /// or more items and one other part: on 2026-10-23 OpenAI gave the hint back in the middle of
    /// speech five times, with the folder kodland spelled кодland.
    public static func isEcho(_ phrase: String, hint: String) -> Bool {
        let items = Set(parts(hint))
        let said = parts(phrase)
        let inHint = said.filter(items.contains).count
        return !items.isEmpty && said.count >= 3
            && (inHint == said.count || (inHint >= 4 && inHint == said.count - 1))
    }

    /// The phrase without the sentences that are the hint given back (isEcho); the speech around
    /// them stays. What was cut and what nearly was is counted for the log.
    public static func withoutEcho(_ phrase: String, hint: String) -> Unechoed {
        let items = Set(parts(hint))
        var kept: [String] = []
        var dropped: [EchoCut] = []
        var lookAlikes = 0
        for sentence in sentences(phrase.trimmingCharacters(in: .whitespacesAndNewlines)) {
            let said = parts(sentence)
            let inHint = said.filter(items.contains).count
            if isEcho(sentence, hint: hint) {
                dropped.append(EchoCut(parts: said.count, inHint: inHint))
                continue
            }
            if !items.isEmpty && inHint >= 3 {
                lookAlikes += 1
            }
            kept.append(sentence)
        }
        return Unechoed(text: kept.joined(separator: " "), dropped: dropped, lookAlikes: lookAlikes)
    }

    /// Sentences end with . ! ? or … and a space after it. The dot of a file name has no space after it.
    private static func sentences(_ text: String) -> [String] {
        var result: [String] = []
        var current = ""
        var afterEnd = false
        for character in text {
            if afterEnd && character.isWhitespace {
                if !current.isEmpty { result.append(current) }
                current = ""
                continue
            }
            afterEnd = ".!?…".contains(character)
            current.append(character)
        }
        if !current.isEmpty { result.append(current) }
        return result
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
