import Foundation

/// Text read the way the Windows core reads it, for the rules of the memory.
///
/// .NET counts a string in UTF-16 units, trims and splits on its own list of white space, and its
/// regular expressions look at one unit at a time. Swift counts characters and has other lists.
/// The memory has limits and word edges that both cores must agree on to the unit, so the few
/// questions the memory asks about a text are answered here, in .NET's terms.
enum MemoryText {
    /// char.IsWhiteSpace: the three separator categories, tab to carriage return and the next line
    /// character.
    static func isWhiteSpace(_ scalar: Unicode.Scalar) -> Bool {
        switch scalar.value {
        case 0x09...0x0D, 0x85:
            return true
        default:
            switch scalar.properties.generalCategory {
            case .spaceSeparator, .lineSeparator, .paragraphSeparator: return true
            default: return false
            }
        }
    }

    /// string.IsNullOrWhiteSpace.
    static func isBlank(_ text: String?) -> Bool {
        text?.unicodeScalars.allSatisfy(isWhiteSpace) ?? true
    }

    /// string.Trim().
    static func trim(_ text: String) -> String {
        let scalars = text.unicodeScalars
        guard let first = scalars.firstIndex(where: { !isWhiteSpace($0) }),
              let last = scalars.lastIndex(where: { !isWhiteSpace($0) }) else {
            return ""
        }

        return String(scalars[first...last])
    }

    /// The words of a text joined by one space: a fact is one line of the memory block, so a line
    /// break inside it would break the list.
    static func oneLine(_ text: String?) -> String {
        var words: [String] = []
        var word = String.UnicodeScalarView()
        for scalar in (text ?? "").unicodeScalars {
            if !isWhiteSpace(scalar) {
                word.append(scalar)
            } else if !word.isEmpty {
                words.append(String(word))
                word = String.UnicodeScalarView()
            }
        }
        if !word.isEmpty {
            words.append(String(word))
        }

        return words.joined(separator: " ")
    }

    /// StringComparison.OrdinalIgnoreCase for the words the memory compares this way: category
    /// names and ids, which are ASCII. Only A to Z are folded.
    static func sameIgnoringAsciiCase(_ left: String, _ right: String) -> Bool {
        left.utf16.count == right.utf16.count
            && zip(left.utf16, right.utf16).allSatisfy { lowerAscii($0) == lowerAscii($1) }
    }

    /// What \w means in a .NET regular expression, asked of one UTF-16 unit: a letter, a
    /// non-spacing mark, a decimal digit or a connector such as the underscore. Half of a
    /// surrogate pair is none of these, so a letter above the basic plane is not a word character
    /// there, and it is not one here.
    static func isWordUnit(_ unit: UInt16) -> Bool {
        if unit < 0x80 {
            return (unit | 0x20 >= 0x61 && unit | 0x20 <= 0x7A) || (unit >= 0x30 && unit <= 0x39) || unit == 0x5F
        }

        guard let scalar = Unicode.Scalar(unit) else {
            return false
        }

        switch scalar.properties.generalCategory {
        case .uppercaseLetter, .lowercaseLetter, .titlecaseLetter, .modifierLetter, .otherLetter,
             .nonspacingMark, .decimalNumber, .connectorPunctuation:
            return true
        default:
            return false
        }
    }

    /// One unit as RegexOptions.IgnoreCase with the invariant culture compares it: two units are
    /// the same letter when they have the same small letter. So «Я» is «я» and the Kelvin sign is
    /// k, but the final sigma «ς» is not «σ» and the long «ſ» is not s: each of those is already
    /// small. Checked against .NET 10 for every unit of the basic plane.
    static func fold(_ unit: UInt16) -> UInt16 {
        if unit < 0x80 {
            return lowerAscii(unit)
        }

        guard let scalar = Unicode.Scalar(unit),
              let lower = single(scalar.properties.lowercaseMapping),
              lower.value <= 0xFFFF else {
            return unit
        }

        return UInt16(lower.value)
    }

    static func folded(_ text: String) -> [UInt16] {
        text.utf16.map(fold)
    }

    private static func lowerAscii(_ unit: UInt16) -> UInt16 {
        unit >= 0x41 && unit <= 0x5A ? unit + 0x20 : unit
    }

    /// A case mapping that is one character long. The small form of the dotted capital «İ» is two
    /// characters, and .NET leaves that letter as it is.
    private static func single(_ mapping: String) -> Unicode.Scalar? {
        let scalars = mapping.unicodeScalars
        return scalars.count == 1 ? scalars.first : nil
    }
}

/// A message of the person, read once for every word it is searched for: its UTF-16 units, and the
/// same units with the case folded away.
struct SearchedText {
    let units: [UInt16]
    let folded: [UInt16]

    init(_ text: String) {
        units = Array(text.utf16)
        folded = units.map(MemoryText.fold)
    }

    /// True when the folded word stands at this place.
    func has(_ word: [UInt16], at start: Int) -> Bool {
        guard start + word.count <= folded.count else {
            return false
        }

        for offset in 0..<word.count where folded[start + offset] != word[offset] {
            return false
        }

        return true
    }
}
