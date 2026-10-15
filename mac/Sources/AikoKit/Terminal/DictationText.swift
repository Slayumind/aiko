import Foundation

/// The text of one recording while it is spoken. Each pass of the recogniser reads all the sound
/// so far; the words two passes in a row agree on are confirmed and never change again, the rest
/// is the grey tail the next pass may still correct (D-315, spike in RESEARCH).
public struct DictationText: Sendable, Equatable {
    public static let empty = DictationText([], [], [])

    public let confirmed: [String]
    public let tail: [String]
    private let lastPass: [String]

    private init(_ confirmed: [String], _ tail: [String], _ lastPass: [String]) {
        self.confirmed = confirmed
        self.tail = tail
        self.lastPass = lastPass
    }

    public var confirmedText: String { confirmed.joined(separator: " ") }

    public var tailText: String { tail.joined(separator: " ") }

    public var text: String { (confirmed + tail).joined(separator: " ") }

    /// The text of a new pass over all the sound so far.
    public func pass(_ text: String) -> DictationText {
        let words = Self.words(text)
        let covered = self.covered(words)
        let agreed = Self.agreed(lastPass, words)
        let confirmed = agreed > covered ? self.confirmed + words[covered..<agreed] : self.confirmed
        return DictationText(confirmed, Array(words.dropFirst(max(agreed, covered))), words)
    }

    /// The last pass, after the mic is closed. Every word is confirmed; the words confirmed
    /// earlier stay as they were shown.
    public func final(_ text: String) -> DictationText {
        let words = Self.words(text)
        return DictationText(confirmed + words.dropFirst(covered(words)), [], words)
    }

    /// The words of a pass. Whisper writes sounds it heard in brackets, "[Музыка]" or "(смех)";
    /// those are not words anybody said.
    public static func words(_ text: String?) -> [String] {
        let characters = Array(text ?? "")
        var kept = ""
        var index = 0
        while index < characters.count {
            let character = characters[index]
            if let close = character == "[" ? "]" : character == "(" ? ")" : nil,
               let end = characters[(index + 1)...].firstIndex(of: Character(close)) {
                kept.append(" ")
                index = end + 1
                continue
            }
            kept.append(character)
            index += 1
        }
        return kept.split(whereSeparator: \.isWhitespace).map(String.init)
    }

    /// How many words of a new pass the confirmed words already cover. Counted by letters, not by
    /// words: a later pass may split "пригони.net" into "пригони .NET", or say "Делы" for "Делай".
    private func covered(_ words: [String]) -> Int {
        let target = Self.bare(confirmedText).count
        var letters = 0
        var count = 0
        while count < words.count && letters + Self.bare(words[count]).count <= target {
            letters += Self.bare(words[count]).count
            count += 1
        }
        return count
    }

    private static func agreed(_ a: [String], _ b: [String]) -> Int {
        var count = 0
        while count < a.count && count < b.count && same(a[count], b[count]) {
            count += 1
        }
        return count
    }

    /// Two passes may differ in case and punctuation around a word; that is still one word.
    private static func same(_ a: String, _ b: String) -> Bool {
        let bareA = bare(a)
        return bareA == bare(b) && !bareA.isEmpty
    }

    private static func bare(_ word: String) -> String {
        String(word.lowercased().replacingOccurrences(of: "ё", with: "е").filter { $0.isLetter || $0.isNumber })
    }
}
