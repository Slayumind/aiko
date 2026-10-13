import Foundation

/// The name of a model as the line under the input shows it: "Opus 5.5" for "claude-opus-5-5".
public enum ModelName {
    private static let prefix = "claude-"

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
