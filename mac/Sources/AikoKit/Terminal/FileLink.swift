import Foundation

/// A file an answer points at, with the line when it names one: "src/Form.cs:42".
///
/// This only says that the words look like a file. Whether the file is there is asked by the
/// window, which knows the folder of the conversation: "string.Empty" looks like a file too.
public struct FileLink: Sendable, Equatable {
    public let path: String
    public let line: Int?

    public init(path: String, line: Int?) {
        self.path = path
        self.line = line
    }

    private static let maxExtension = 16
    private static let maxLineDigits = 9

    /// The text is read in UTF-16 units, the way the Windows core reads a string.
    public static func parse(_ text: String?) -> FileLink? {
        var path = trim(Array((text ?? "").utf16))
        // A dash first would reach VS Code as an option of its own, not as a file.
        if path.isEmpty || path.contains(10) || path.contains(13) || path.first == 45 {
            return nil
        }

        let line = takeLine(&path)

        // A colon is the mark of a drive and nothing else: "C:\work". Anywhere further it means
        // an address with a scheme, such as mailto: or https:.
        let lastColon = path.lastIndex(of: colon) ?? -1
        if path.contains(where: { forbidden.contains($0) }) || lastColon > 1 || path.first == colon {
            return nil
        }

        let nameStart = (path.lastIndex { $0 == slash || $0 == backslash } ?? -1) + 1
        let name = Array(path[nameStart...])
        guard let dotAt = name.lastIndex(of: dot), name.count >= 2 else {
            return nil
        }

        // The ending has a letter in it: "3.14" and "v0.4.1" are numbers, not files.
        let ending = name[(dotAt + 1)...]
        guard !ending.isEmpty, ending.count <= maxExtension,
              ending.allSatisfy({ isDigit($0) || isLetter($0) }),
              ending.contains(where: isLetter) else {
            return nil
        }
        return FileLink(path: String(decoding: path, as: UTF16.self), line: line)
    }

    /// Cuts ":42", ":42:7" or "#L42" off the end and gives the line. A column is dropped.
    private static func takeLine(_ path: inout [UInt16]) -> Int? {
        if let anchor = lastAnchor(path), anchor > 0, let anchored = numberIn(path[(anchor + 2)...]) {
            path = Array(path[..<anchor])
            return positive(anchored)
        }

        guard let last = takeNumber(&path) else {
            return nil
        }
        return positive(takeNumber(&path) ?? last)
    }

    private static func takeNumber(_ path: inout [UInt16]) -> Int? {
        guard let at = path.lastIndex(of: colon), at > 0, let number = numberIn(path[(at + 1)...]) else {
            return nil
        }

        path = Array(path[..<at])
        return number
    }

    /// The last "#L" in the path.
    private static func lastAnchor(_ path: [UInt16]) -> Int? {
        var at = path.count - 2
        while at >= 0 {
            if path[at] == hash && path[at + 1] == letterL {
                return at
            }
            at -= 1
        }
        return nil
    }

    private static func numberIn(_ digits: ArraySlice<UInt16>) -> Int? {
        guard !digits.isEmpty, digits.count <= maxLineDigits, digits.allSatisfy(isDigit) else {
            return nil
        }
        return Int(String(decoding: digits, as: UTF16.self))
    }

    private static func positive(_ line: Int) -> Int? {
        line > 0 ? line : nil
    }

    static func trim(_ units: [UInt16]) -> [UInt16] {
        var from = 0
        var end = units.count
        while from < end && (units[from] == 32 || units[from] == 9) {
            from += 1
        }
        while end > from && (units[end - 1] == 32 || units[end - 1] == 9) {
            end -= 1
        }
        return Array(units[from..<end])
    }

    private static func isDigit(_ unit: UInt16) -> Bool {
        unit >= 48 && unit <= 57
    }

    private static func isLetter(_ unit: UInt16) -> Bool {
        (unit >= 65 && unit <= 90) || (unit >= 97 && unit <= 122)
    }

    private static let colon: UInt16 = 58
    private static let dot: UInt16 = 46
    private static let slash: UInt16 = 47
    private static let backslash: UInt16 = 92
    private static let hash: UInt16 = 35
    private static let letterL: UInt16 = 76

    /// < > " | ? * and the backtick: none of them is part of a file name.
    private static let forbidden: Set<UInt16> = [60, 62, 34, 124, 63, 42, 96]
}

/// Where a link of an answer leads.
public enum LinkTarget: Sendable, Equatable {
    /// An address for the browser. Only http and https: nothing else is opened from an answer.
    case web(String)

    /// A file for the editor.
    case file(FileLink)

    /// Nothing the terminal opens.
    case none

    public static func of(_ href: String?) -> LinkTarget {
        let target = String(decoding: FileLink.trim(Array((href ?? "").utf16)), as: UTF16.self)
        if (target.hasPrefix("https://") && target.utf16.count > 8)
            || (target.hasPrefix("http://") && target.utf16.count > 7) {
            return .web(target)
        }

        if let link = FileLink.parse(target) {
            return .file(link)
        }
        return .none
    }
}
