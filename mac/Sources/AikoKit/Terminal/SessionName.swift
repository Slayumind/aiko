import Foundation

/// The name of a session (D-299). The form does not ask for one: an empty field gives the folder
/// with a number, "aiko 3", and the form shows that name before the session is made.
///
/// The twin of SessionName.cs. Texts are read as UTF-16 units, the way C# reads them, so a line
/// break of two characters is two units here as well.
public enum SessionName {
    private static let space: UInt16 = 32
    private static let slash: UInt16 = 47
    private static let backslash: UInt16 = 92
    private static let colon: UInt16 = 58

    /// The folder with the lowest number no session of that folder has yet.
    public static func defaultName(_ folder: String, taken: [String]) -> String {
        let name = folderName(folder)
        var number = 1
        while taken.contains("\(name) \(number)") {
            number += 1
        }
        return "\(name) \(number)"
    }

    /// A name as it is kept: one line, without spaces around it.
    public static func clean(_ name: String?) -> String {
        var units: [UInt16] = []
        let all = Array((name ?? "").utf16)
        var index = 0
        while index < all.count {
            let unit = all[index]
            if unit == 13, index + 1 < all.count, all[index + 1] == 10 {
                units.append(space)
                index += 2
                continue
            }
            units.append(unit == 13 || unit == 10 || unit == 9 ? space : unit)
            index += 1
        }
        return text(trimmed(units))
    }

    /// A folder as it is kept: without a slash at the end, so one folder is one group. The root
    /// of a disk keeps its slash.
    public static func folderPath(_ folder: String?) -> String {
        var units = trimmed(Array((folder ?? "").utf16))
        while units.count > 1, isSlash(units[units.count - 1]), units[units.count - 2] != colon {
            units.removeLast()
        }
        return text(units)
    }

    /// The last part of a path, with a slash of either kind: the name of the folder. A root has
    /// no last part and is named as it is written.
    /// The name of the folder this folder sits in, shown beside its name in the form of a new
    /// session instead of the whole path (D-332). A drive or the root has none.
    public static func parentName(_ folder: String) -> String {
        var path = Array(folderPath(folder).utf16)
        while let last = path.last, isSlash(last) {
            path.removeLast()
        }
        guard let cut = path.lastIndex(where: isSlash), cut > 0 else { return "" }
        let parent = Array(path[..<cut])
        return parent.last == UInt16(UInt8(ascii: ":")) ? "" : folderName(text(parent))
    }

    public static func folderName(_ folder: String) -> String {
        let path = Array(folderPath(folder).utf16)
        var name = path
        while let last = name.last, isSlash(last) {
            name.removeLast()
        }
        let start = (name.lastIndex(where: isSlash) ?? -1) + 1
        name = Array(name[start...])
        return text(name.isEmpty ? path : name)
    }

    private static func isSlash(_ unit: UInt16) -> Bool {
        unit == slash || unit == backslash
    }

    private static func trimmed(_ units: [UInt16]) -> [UInt16] {
        var units = units[...]
        while units.first == space {
            units.removeFirst()
        }
        while units.last == space {
            units.removeLast()
        }
        return Array(units)
    }

    private static func text(_ units: [UInt16]) -> String {
        String(decoding: units, as: UTF16.self)
    }
}
