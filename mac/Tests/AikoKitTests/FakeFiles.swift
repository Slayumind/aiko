import Foundation

@testable import AikoKit

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
