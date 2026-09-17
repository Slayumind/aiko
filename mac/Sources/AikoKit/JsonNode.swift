import Foundation

/// A JSON value that keeps the order of the file it came from.
///
/// The Windows core uses System.Text.Json, which keeps the order of keys and the text of numbers
/// when it writes a file back. Foundation's JSONSerialization does neither, and Aiko rewrites
/// settings files that belong to Claude Code, so the order and the numbers have to survive. This
/// is a small reader and writer with the same rules: no comments, no trailing commas, at most 64
/// levels deep.
public enum JsonNode: Sendable, Equatable {
    case object(JsonObject)
    case array([JsonNode])
    case string(String)

    /// The number as it was written. Kept as text so that "1.50e3" is written back the same way.
    case number(String)

    case bool(Bool)
    case null
}

/// The members of a JSON object, in order, like JsonObject in the Windows core.
public struct JsonObject: Sendable, Equatable {
    public struct Member: Sendable, Equatable {
        public var key: String
        public var value: JsonNode

        public init(key: String, value: JsonNode) {
            self.key = key
            self.value = value
        }
    }

    public var members: [Member]

    public init() {
        members = []
    }

    public init(_ members: [Member]) {
        self.members = members
    }

    public init(_ pairs: [(String, JsonNode)]) {
        members = pairs.map { Member(key: $0.0, value: $0.1) }
    }

    public var count: Int { members.count }

    public var isEmpty: Bool { members.isEmpty }

    public var keys: [String] { members.map(\.key) }

    /// The last value for a key, the way JsonDocument reads a file with a repeated key.
    public subscript(key: String) -> JsonNode? {
        get { members.last(where: { $0.key == key })?.value }
        set {
            guard let newValue else {
                remove(key)
                return
            }
            if let index = members.firstIndex(where: { $0.key == key }) {
                members[index].value = newValue
            } else {
                members.append(Member(key: key, value: newValue))
            }
        }
    }

    public func contains(_ key: String) -> Bool {
        members.contains { $0.key == key }
    }

    @discardableResult
    public mutating func remove(_ key: String) -> Bool {
        let before = members.count
        members.removeAll { $0.key == key }
        return members.count != before
    }
}

// MARK: - Reading values

extension JsonNode {
    public var objectValue: JsonObject? {
        if case .object(let value) = self { return value }
        return nil
    }

    public var arrayValue: [JsonNode]? {
        if case .array(let value) = self { return value }
        return nil
    }

    public var stringValue: String? {
        if case .string(let value) = self { return value }
        return nil
    }

    public var boolValue: Bool? {
        if case .bool(let value) = self { return value }
        return nil
    }

    /// The number as a double. A number too big for a double reads as infinity, as GetDouble does.
    public var doubleValue: Double? {
        guard case .number(let text) = self else { return nil }
        return Double(text)
    }

    /// Whole numbers only: GetInt64 refuses a fraction or an exponent and throws instead.
    public var int64Value: Int64? {
        guard case .number(let text) = self else { return nil }
        guard !text.contains(".") && !text.lowercased().contains("e") else { return nil }
        return Int64(text)
    }

    public var isObject: Bool { objectValue != nil }

    public subscript(key: String) -> JsonNode? {
        objectValue?[key]
    }
}

// MARK: - Parsing

extension JsonNode {
    /// The default depth of System.Text.Json: a file nested deeper than this is refused.
    public static let maxDepth = 64

    /// Null when the text is not JSON we accept, the way every parser in the core answers a broken
    /// file with "no data".
    public static func parse(_ text: String?) -> JsonNode? {
        guard let text, !text.isEmpty else { return nil }
        var parser = JsonParser(text: Array(text.utf8))
        return parser.parseDocument()
    }
}

private struct JsonParser {
    let text: [UInt8]
    var at = 0
    var depth = 0

    init(text: [UInt8]) {
        self.text = text
    }

    mutating func parseDocument() -> JsonNode? {
        skipWhitespace()
        guard let value = parseValue() else { return nil }
        skipWhitespace()
        return at == text.count ? value : nil
    }

    private mutating func skipWhitespace() {
        while at < text.count {
            switch text[at] {
            case 0x20, 0x09, 0x0A, 0x0D: at += 1
            default: return
            }
        }
    }

    private mutating func parseValue() -> JsonNode? {
        guard at < text.count else { return nil }
        switch text[at] {
        case UInt8(ascii: "{"): return parseObject()
        case UInt8(ascii: "["): return parseArray()
        case UInt8(ascii: "\""): return parseString().map(JsonNode.string)
        case UInt8(ascii: "t"): return parseWord("true") ? .bool(true) : nil
        case UInt8(ascii: "f"): return parseWord("false") ? .bool(false) : nil
        case UInt8(ascii: "n"): return parseWord("null") ? JsonNode.null : nil
        default: return parseNumber()
        }
    }

    private mutating func parseWord(_ word: String) -> Bool {
        let bytes = Array(word.utf8)
        guard at + bytes.count <= text.count else { return false }
        for (offset, byte) in bytes.enumerated() where text[at + offset] != byte {
            return false
        }
        at += bytes.count
        return true
    }

    private mutating func parseObject() -> JsonNode? {
        depth += 1
        defer { depth -= 1 }
        guard depth <= JsonNode.maxDepth else { return nil }

        at += 1
        var object = JsonObject()
        skipWhitespace()
        if at < text.count && text[at] == UInt8(ascii: "}") {
            at += 1
            return .object(object)
        }

        while true {
            skipWhitespace()
            guard at < text.count, text[at] == UInt8(ascii: "\""), let key = parseString() else { return nil }
            skipWhitespace()
            guard at < text.count, text[at] == UInt8(ascii: ":") else { return nil }
            at += 1
            skipWhitespace()
            guard let value = parseValue() else { return nil }
            object.members.append(JsonObject.Member(key: key, value: value))

            skipWhitespace()
            guard at < text.count else { return nil }
            if text[at] == UInt8(ascii: ",") {
                at += 1
                continue
            }
            if text[at] == UInt8(ascii: "}") {
                at += 1
                return .object(object)
            }
            return nil
        }
    }

    private mutating func parseArray() -> JsonNode? {
        depth += 1
        defer { depth -= 1 }
        guard depth <= JsonNode.maxDepth else { return nil }

        at += 1
        var items: [JsonNode] = []
        skipWhitespace()
        if at < text.count && text[at] == UInt8(ascii: "]") {
            at += 1
            return .array(items)
        }

        while true {
            skipWhitespace()
            guard let value = parseValue() else { return nil }
            items.append(value)

            skipWhitespace()
            guard at < text.count else { return nil }
            if text[at] == UInt8(ascii: ",") {
                at += 1
                continue
            }
            if text[at] == UInt8(ascii: "]") {
                at += 1
                return .array(items)
            }
            return nil
        }
    }

    /// Control characters have to be escaped, and a lone surrogate is refused: the Windows parser
    /// fails on both.
    private mutating func parseString() -> String? {
        at += 1
        var scalars = String.UnicodeScalarView()
        var bytes: [UInt8] = []

        func flush() -> Bool {
            guard !bytes.isEmpty else { return true }
            guard let text = String(bytes: bytes, encoding: .utf8) else { return false }
            scalars.append(contentsOf: text.unicodeScalars)
            bytes.removeAll(keepingCapacity: true)
            return true
        }

        while at < text.count {
            let byte = text[at]
            if byte == UInt8(ascii: "\"") {
                at += 1
                return flush() ? String(scalars) : nil
            }
            if byte == UInt8(ascii: "\\") {
                guard flush(), at + 1 < text.count else { return nil }
                at += 1
                let escape = text[at]
                at += 1
                switch escape {
                case UInt8(ascii: "\""): scalars.append("\"")
                case UInt8(ascii: "\\"): scalars.append("\\")
                case UInt8(ascii: "/"): scalars.append("/")
                case UInt8(ascii: "b"): scalars.append(Unicode.Scalar(0x08)!)
                case UInt8(ascii: "f"): scalars.append(Unicode.Scalar(0x0C)!)
                case UInt8(ascii: "n"): scalars.append("\n")
                case UInt8(ascii: "r"): scalars.append("\r")
                case UInt8(ascii: "t"): scalars.append("\t")
                case UInt8(ascii: "u"):
                    guard let first = parseHex4() else { return nil }
                    if first >= 0xD800 && first <= 0xDBFF {
                        guard at + 1 < text.count,
                              text[at] == UInt8(ascii: "\\"),
                              text[at + 1] == UInt8(ascii: "u") else { return nil }
                        at += 2
                        guard let second = parseHex4(), second >= 0xDC00, second <= 0xDFFF else { return nil }
                        let value = 0x10000 + ((first - 0xD800) << 10) + (second - 0xDC00)
                        guard let scalar = Unicode.Scalar(value) else { return nil }
                        scalars.append(scalar)
                    } else {
                        guard let scalar = Unicode.Scalar(first), !(0xD800...0xDFFF).contains(first) else { return nil }
                        scalars.append(scalar)
                    }
                default: return nil
                }
                continue
            }
            if byte < 0x20 {
                return nil
            }
            bytes.append(byte)
            at += 1
        }

        return nil
    }

    private mutating func parseHex4() -> UInt32? {
        guard at + 4 <= text.count else { return nil }
        var value: UInt32 = 0
        for _ in 0..<4 {
            let byte = text[at]
            let digit: UInt32
            switch byte {
            case UInt8(ascii: "0")...UInt8(ascii: "9"): digit = UInt32(byte - UInt8(ascii: "0"))
            case UInt8(ascii: "a")...UInt8(ascii: "f"): digit = UInt32(byte - UInt8(ascii: "a")) + 10
            case UInt8(ascii: "A")...UInt8(ascii: "F"): digit = UInt32(byte - UInt8(ascii: "A")) + 10
            default: return nil
            }
            value = value * 16 + digit
            at += 1
        }
        return value
    }

    /// The JSON grammar: no leading zero, no leading plus, a digit on both sides of the point.
    private mutating func parseNumber() -> JsonNode? {
        let start = at
        if at < text.count && text[at] == UInt8(ascii: "-") {
            at += 1
        }

        guard let first = digit() else { return nil }
        if first != UInt8(ascii: "0") {
            while digit() != nil {}
        }

        if at < text.count && text[at] == UInt8(ascii: ".") {
            at += 1
            guard digit() != nil else { return nil }
            while digit() != nil {}
        }

        if at < text.count && (text[at] | 0x20) == UInt8(ascii: "e") {
            at += 1
            if at < text.count && (text[at] == UInt8(ascii: "+") || text[at] == UInt8(ascii: "-")) {
                at += 1
            }
            guard digit() != nil else { return nil }
            while digit() != nil {}
        }

        return String(bytes: text[start..<at], encoding: .utf8).map(JsonNode.number)
    }

    private mutating func digit() -> UInt8? {
        guard at < text.count, text[at] >= UInt8(ascii: "0"), text[at] <= UInt8(ascii: "9") else { return nil }
        let byte = text[at]
        at += 1
        return byte
    }
}

// MARK: - Writing

/// How much a writer escapes. The Windows core uses both: the relaxed one for files a person may
/// open, the strict one where the default of System.Text.Json is left alone.
public enum JsonEscaping: Sendable {
    /// JavaScriptEncoder.UnsafeRelaxedJsonEscaping: letters of any language stay readable.
    case relaxed

    /// The default encoder: everything but plain ASCII is escaped, HTML characters included.
    case strict
}

extension JsonNode {
    public func toJsonString(indented: Bool = false, escaping: JsonEscaping = .strict) -> String {
        var out = ""
        write(into: &out, indented: indented, escaping: escaping, level: 0)
        return out
    }

    private func write(into out: inout String, indented: Bool, escaping: JsonEscaping, level: Int) {
        switch self {
        case .null:
            out += "null"
        case .bool(let value):
            out += value ? "true" : "false"
        case .number(let text):
            out += text
        case .string(let text):
            out += JsonNode.quote(text, escaping: escaping)
        case .array(let items):
            if items.isEmpty {
                out += "[]"
                return
            }
            out += "["
            for (index, item) in items.enumerated() {
                if index > 0 { out += "," }
                if indented {
                    out += "\n" + String(repeating: " ", count: (level + 1) * 2)
                }
                item.write(into: &out, indented: indented, escaping: escaping, level: level + 1)
            }
            if indented {
                out += "\n" + String(repeating: " ", count: level * 2)
            }
            out += "]"
        case .object(let object):
            if object.isEmpty {
                out += "{}"
                return
            }
            out += "{"
            for (index, member) in object.members.enumerated() {
                if index > 0 { out += "," }
                if indented {
                    out += "\n" + String(repeating: " ", count: (level + 1) * 2)
                }
                out += JsonNode.quote(member.key, escaping: escaping)
                out += indented ? ": " : ":"
                member.value.write(into: &out, indented: indented, escaping: escaping, level: level + 1)
            }
            if indented {
                out += "\n" + String(repeating: " ", count: level * 2)
            }
            out += "}"
        }
    }

    static func quote(_ text: String, escaping: JsonEscaping) -> String {
        var out = "\""
        for scalar in text.unicodeScalars {
            switch scalar {
            case "\"":
                out += escaping == .relaxed ? "\\\"" : "\\u0022"
            case "\\":
                out += "\\\\"
            case "\n":
                out += "\\n"
            case "\r":
                out += "\\r"
            case "\t":
                out += "\\t"
            case Unicode.Scalar(0x08):
                out += "\\b"
            case Unicode.Scalar(0x0C):
                out += "\\f"
            default:
                if needsEscape(scalar, escaping: escaping) {
                    out += escaped(scalar)
                } else {
                    out.unicodeScalars.append(scalar)
                }
            }
        }
        return out + "\""
    }

    /// The strict encoder keeps printable ASCII apart from the HTML characters. The relaxed one
    /// keeps every letter and escapes what no editor shows: control, separator, private use and
    /// unassigned characters, the byte order mark and everything above the basic plane.
    private static func needsEscape(_ scalar: Unicode.Scalar, escaping: JsonEscaping) -> Bool {
        switch escaping {
        case .strict:
            if scalar.value < 0x20 || scalar.value > 0x7E { return true }
            return "&'+<>`".unicodeScalars.contains(scalar)
        case .relaxed:
            if scalar.value < 0x20 || scalar.value == 0x7F { return true }
            if scalar.value > 0xFFFF { return true }
            if scalar.value == 0xFEFF { return true }
            if scalar == " " { return false }
            switch scalar.properties.generalCategory {
            case .control, .format, .surrogate, .privateUse, .unassigned,
                 .lineSeparator, .paragraphSeparator, .spaceSeparator:
                // The format category is mixed: only the byte order mark above is escaped.
                return scalar.properties.generalCategory != .format
            default:
                return false
            }
        }
    }

    private static func escaped(_ scalar: Unicode.Scalar) -> String {
        if scalar.value > 0xFFFF {
            let value = scalar.value - 0x10000
            let high = 0xD800 + (value >> 10)
            let low = 0xDC00 + (value & 0x3FF)
            return String(format: "\\u%04X\\u%04X", high, low)
        }
        return String(format: "\\u%04X", scalar.value)
    }
}
