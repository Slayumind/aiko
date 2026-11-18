import Foundation

/// Gives the voice of the personality its colour in the terminal (D-457). The twin of VoiceTint.cs.
public final class VoiceTint {
    private static let escape: Unicode.Scalar = "\u{1b}"
    private static let mostHeld = 128

    private let colour: String
    private var held: [Unicode.Scalar] = []
    private var voice = false

    public init(red: UInt8, green: UInt8, blue: UInt8) {
        colour = "38;2;\(red);\(green);\(blue)"
    }

    public func apply(_ piece: String) -> String {
        let text = held + Array(piece.unicodeScalars)
        held = []
        var result = String.UnicodeScalarView()
        var at = 0
        while at < text.count {
            if text[at] != Self.escape {
                result.append(text[at])
                at += 1
                continue
            }

            guard let end = Self.sequenceEnd(text, at) else {
                if text.count - at <= Self.mostHeld {
                    held = Array(text[at...])
                    return String(result)
                }
                result.append(text[at])
                at += 1
                continue
            }

            let sequence = text[at...end]
            if text[end] == "m", text[at + 1] == "[" {
                var inner = String.UnicodeScalarView()
                inner.append(contentsOf: text[(at + 2)..<end])
                result.append(contentsOf: style(String(inner), sequence).unicodeScalars)
            } else {
                result.append(contentsOf: sequence)
            }
            at = end + 1
        }
        return String(result)
    }

    /// The last character of the sequence at this place; nil when it has not all come yet.
    private static func sequenceEnd(_ text: [Unicode.Scalar], _ at: Int) -> Int? {
        guard at + 1 < text.count else { return nil }
        guard text[at + 1] == "[" else { return at + 1 }
        var i = at + 2
        while i < text.count {
            if text[i].value >= 0x40, text[i].value <= 0x7E { return i }
            i += 1
        }
        return nil
    }

    private func style(_ parameters: String, _ original: ArraySlice<Unicode.Scalar>) -> String {
        guard parameters.unicodeScalars.allSatisfy({ ($0.value >= 0x30 && $0.value <= 0x39) || $0 == ";" }) else {
            var view = String.UnicodeScalarView()
            view.append(contentsOf: original)
            return String(view)
        }

        let codes = parameters.split(separator: ";", omittingEmptySubsequences: false).map(String.init)
        var written: [String] = []
        var i = 0
        while i < codes.count {
            let code = String(codes[i].drop { $0 == "0" })
            switch code {
            case "":
                voice = false
                written.append(codes[i])
            case "3":
                voice = true
                written.append(colour)
            case "23" where voice:
                voice = false
                written.append("39")
            case "39" where voice:
                written.append(colour)
            case "38", "48", "58":
                // A colour carries its own numbers: none of them is a style.
                let extra = i + 1 < codes.count ? (codes[i + 1] == "5" ? 2 : codes[i + 1] == "2" ? 4 : 0) : 0
                let last = min(codes.count - 1, i + extra)
                written += codes[i...last]
                i = last
            default:
                written.append(codes[i])
            }
            i += 1
        }
        return "\u{1b}[" + written.joined(separator: ";") + "m"
    }
}
