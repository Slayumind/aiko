import Foundation

/// The values of a JSON object or array as they were written, spaces and number text included.
///
/// The Windows core asks JsonElement.GetRawText for this. The memory needs it twice: an entry of
/// memory.json that Aiko cannot use goes back into the file exactly as the person wrote it, and
/// the id of a JSON-RPC message goes back to the client exactly as it came.
///
/// This does not check the text. Call it for text JsonNode.parse has already read; for anything
/// else the answer is nil or meaningless.
enum JsonRawText {
    /// The members of the object this text is, in order. Nil when the text is not an object.
    static func members(_ text: String) -> [(key: String, value: String)]? {
        var reader = Reader(Array(text.utf8))
        return reader.members()
    }

    /// The items of the array this text is, in order. Nil when the text is not an array.
    static func items(_ text: String) -> [String]? {
        var reader = Reader(Array(text.utf8))
        return reader.items()
    }

    private struct Reader {
        let text: [UInt8]
        var at = 0

        init(_ text: [UInt8]) {
            self.text = text
        }

        mutating func members() -> [(key: String, value: String)]? {
            skipWhitespace()
            guard take("{") else { return nil }

            var found: [(key: String, value: String)] = []
            skipWhitespace()
            if take("}") { return found }

            while true {
                skipWhitespace()
                guard let name = value(), let key = JsonNode.parse(name)?.stringValue else { return nil }
                skipWhitespace()
                guard take(":") else { return nil }
                skipWhitespace()
                guard let written = value() else { return nil }
                found.append((key, written))

                skipWhitespace()
                if take(",") { continue }
                return take("}") ? found : nil
            }
        }

        mutating func items() -> [String]? {
            skipWhitespace()
            guard take("[") else { return nil }

            var found: [String] = []
            skipWhitespace()
            if take("]") { return found }

            while true {
                skipWhitespace()
                guard let written = value() else { return nil }
                found.append(written)

                skipWhitespace()
                if take(",") { continue }
                return take("]") ? found : nil
            }
        }

        /// One value, from its first character to its last.
        private mutating func value() -> String? {
            let start = at
            guard at < text.count else { return nil }

            switch text[at] {
            case UInt8(ascii: "\""):
                guard skipString() else { return nil }
            case UInt8(ascii: "{"), UInt8(ascii: "["):
                guard skipNested() else { return nil }
            default:
                while at < text.count, !endsPlainValue(text[at]) {
                    at += 1
                }
            }

            return at > start ? String(decoding: text[start..<at], as: UTF8.self) : nil
        }

        private mutating func skipString() -> Bool {
            at += 1
            while at < text.count {
                switch text[at] {
                case UInt8(ascii: "\\"):
                    at += 2
                case UInt8(ascii: "\""):
                    at += 1
                    return true
                default:
                    at += 1
                }
            }

            return false
        }

        /// An object or an array with everything inside it. Brackets inside a string do not count.
        private mutating func skipNested() -> Bool {
            var depth = 0
            while at < text.count {
                switch text[at] {
                case UInt8(ascii: "\""):
                    guard skipString() else { return false }
                    continue
                case UInt8(ascii: "{"), UInt8(ascii: "["):
                    depth += 1
                case UInt8(ascii: "}"), UInt8(ascii: "]"):
                    depth -= 1
                default:
                    break
                }

                at += 1
                if depth == 0 {
                    return true
                }
            }

            return false
        }

        /// A number, true, false or null ends at a comma, a closing bracket or white space.
        private func endsPlainValue(_ byte: UInt8) -> Bool {
            switch byte {
            case UInt8(ascii: ","), UInt8(ascii: "}"), UInt8(ascii: "]"), 0x20, 0x09, 0x0A, 0x0D: return true
            default: return false
            }
        }

        private mutating func skipWhitespace() {
            while at < text.count {
                switch text[at] {
                case 0x20, 0x09, 0x0A, 0x0D: at += 1
                default: return
                }
            }
        }

        private mutating func take(_ character: Character) -> Bool {
            guard at < text.count, text[at] == character.asciiValue else { return false }
            at += 1
            return true
        }
    }
}
