import Foundation

@testable import AikoKit

/// Stands in for BridgeCommand while that file is still Windows only: the same rule for
/// recognising our own status line, so the patch tests can use the payloads the xUnit ones use.
enum TestBridge {
    static let executableName = "Aiko.Bridge.exe"

    static func command(_ bridgePath: String, powerShell: Bool = false) -> String {
        let path = bridgePath.trimmingCharacters(in: .whitespacesAndNewlines)
        if path.isEmpty {
            return ""
        }

        let quoted = path.hasPrefix("\"") && path.hasSuffix("\"") ? path : "\"\(path)\""
        return powerShell ? "& \(quoted)" : quoted
    }

    static func hookCommand(_ bridgePath: String, powerShell: Bool = false) -> String {
        let text = command(bridgePath, powerShell: powerShell)
        return text.isEmpty ? text : text + " " + SessionReminder.argument
    }

    static func isAiko(_ command: String?) -> Bool {
        guard let path = executablePathIn(command) else {
            return false
        }
        let name = path.components(separatedBy: CharacterSet(charactersIn: "\\/")).last ?? path
        return name.caseInsensitiveCompare(executableName) == .orderedSame
    }

    /// The patch under test, with our own recogniser.
    static let patch = SettingsJsonPatch(isAiko: { isAiko($0) })

    private static func executablePathIn(_ command: String?) -> String? {
        var text = command?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        if text.isEmpty {
            return nil
        }

        if text.hasPrefix("& ") {
            text = String(text.dropFirst(2)).trimmingCharacters(in: .whitespaces)
        }

        if text.hasPrefix("\"") {
            let rest = text.dropFirst()
            guard let closing = rest.firstIndex(of: "\"") else { return nil }
            let inside = String(rest[rest.startIndex..<closing])
            return inside.isEmpty ? nil : inside
        }

        let head = text.components(separatedBy: " ").first ?? text
        return head.isEmpty ? nil : head
    }
}

/// A file system in a dictionary, so the code that writes to somebody else's settings file can be
/// tested without one.
final class FakeFiles: FileAccess {
    struct Refused: Error {}

    private var files: [String: String] = [:]

    /// Paths that answer every call with an error, for the folder Aiko may not touch.
    var unreadable: Set<String> = []

    var writes: [String] = []

    @discardableResult
    func with(_ path: String, _ text: String) -> FakeFiles {
        files[key(path)] = text
        return self
    }

    func has(_ path: String) -> Bool { files[key(path)] != nil }

    func read(_ path: String) -> String { files[key(path)] ?? "" }

    func exists(_ path: String) throws -> Bool {
        try refuse(path)
        return files[key(path)] != nil
    }

    func readAllText(_ path: String) throws -> String {
        try refuse(path)
        guard let text = files[key(path)] else { throw Refused() }
        return text
    }

    func writeAllText(_ path: String, _ text: String) throws {
        try refuse(path)
        writes.append(path)
        files[key(path)] = text
    }

    func copy(_ from: String, _ to: String) throws {
        try refuse(from)
        try refuse(to)
        files[key(to)] = files[key(from)]
    }

    func move(_ from: String, _ to: String) throws {
        try refuse(from)
        try refuse(to)
        files[key(to)] = files[key(from)]
        files[key(from)] = nil
    }

    func delete(_ path: String) throws {
        try refuse(path)
        files[key(path)] = nil
    }

    private func refuse(_ path: String) throws {
        if unreadable.contains(where: { $0.caseInsensitiveCompare(path) == .orderedSame }) {
            throw Refused()
        }
    }

    private func key(_ path: String) -> String { path.lowercased() }
}
