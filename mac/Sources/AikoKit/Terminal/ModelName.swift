import Foundation

/// The model in the line under the input. Waiting is set while Claude Code has not named the
/// full id yet: the line shows the family with "…" in a quiet colour.
public struct ModelLabel: Sendable, Equatable {
    public let text: String
    public let waiting: Bool

    public init(text: String, waiting: Bool) {
        self.text = text
        self.waiting = waiting
    }
}

/// The name of a model as the line under the input shows it: "Opus 5.5" for "claude-opus-5-5".
public enum ModelName {
    private static let prefix = "claude-"
    private static let switched = "<local-command-stdout>Set model to "
    private static let notYet = " …"

    /// Asked is the model sent to Claude Code that it has not confirmed. Known is the last model
    /// Claude Code named, or the one kept for the session. A short name like "sonnet" has no
    /// version, so the line waits for Claude Code to name the full id.
    public static func label(asked: String?, known: String?) -> ModelLabel {
        let askedText = (asked ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if !askedText.isEmpty {
            return ModelLabel(text: display(askedText) + notYet, waiting: true)
        }

        let knownText = (known ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if knownText.isEmpty {
            return ModelLabel(text: "", waiting: false)
        }

        return knownText.hasPrefix(prefix)
            ? ModelLabel(text: display(knownText), waiting: false)
            : ModelLabel(text: display(knownText) + notYet, waiting: true)
    }

    /// The full id from what Claude Code writes after a change of model:
    /// "<local-command-stdout>Set model to `sonnet (claude-sonnet-5-5)`". Nil for any other text.
    public static func fromSwitch(_ text: String?) -> String? {
        guard let text, let at = text.range(of: switched),
              let open = text[at.upperBound...].firstIndex(of: "("),
              let close = text[open...].firstIndex(of: ")") else {
            return nil
        }

        let id = text[text.index(after: open)..<close].trimmingCharacters(in: .whitespacesAndNewlines)
        return id.count > prefix.count && id.hasPrefix(prefix) ? id : nil
    }

    /// An id that does not look like a Claude model comes back as it is.
    public static func display(_ id: String?) -> String {
        let text = (id ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
        if text.isEmpty {
            return ""
        }

        var words: [String] = []
        var numbers: [String] = []
        let body = text.hasPrefix(prefix) ? String(text.dropFirst(prefix.count)) : text

        for part in body.split(separator: "-", omittingEmptySubsequences: false).map(String.init) {
            if isDate(part) {
                continue
            }
            if isShortNumber(part) {
                numbers.append(part)
            } else if isWord(part) {
                words.append(part.prefix(1).uppercased() + part.dropFirst())
            } else {
                return text
            }
        }

        if words.isEmpty {
            return text
        }

        let name = words.joined(separator: " ")
        return numbers.isEmpty ? name : name + " " + numbers.joined(separator: ".")
    }

    /// The day a model was released, the tail of older ids: 20251001.
    private static func isDate(_ part: String) -> Bool {
        part.unicodeScalars.count == 8 && part.unicodeScalars.allSatisfy(isDigit)
    }

    private static func isShortNumber(_ part: String) -> Bool {
        (1...2).contains(part.unicodeScalars.count) && part.unicodeScalars.allSatisfy(isDigit)
    }

    private static func isWord(_ part: String) -> Bool {
        !part.isEmpty && part.unicodeScalars.allSatisfy { $0.value >= 97 && $0.value <= 122 }
    }

    private static func isDigit(_ scalar: Unicode.Scalar) -> Bool {
        scalar.value >= 48 && scalar.value <= 57
    }
}
