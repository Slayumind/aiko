import AikoKit
import Foundation

/// The conversations Claude Code keeps in the folders of the environments, for the palette of
/// sessions (D-353). Only the start and the end of each file are read, off the main thread, and a
/// file that has not changed since the last time is not read again. The twin of ConversationShelf.cs.
final class ConversationShelf: @unchecked Sendable {
    private struct Known {
        let written: Date
        let length: Int64
        let conversation: Conversation?
    }

    private var read: [String: Known] = [:]
    private let queue = DispatchQueue(label: "aiko.conversations")

    /// Every conversation of these environments: the name of each, with its folder of Claude Code.
    /// A folder or a file that cannot be read is skipped.
    func load(_ folders: [(environment: String, config: String)]) async -> [Conversation] {
        await withCheckedContinuation { done in
            queue.async {
                var found: [Conversation] = []
                for (environment, config) in folders {
                    for file in Self.files(config) {
                        if let conversation = self.readCached(file, environment) {
                            found.append(conversation)
                        }
                    }
                }
                done.resume(returning: found)
            }
        }
    }

    /// The top files of each project folder; the folders inside hold the conversations of subagents.
    private static func files(_ config: String) -> [String] {
        let projects = (config as NSString).appendingPathComponent("projects")
        let manager = FileManager.default
        guard let folders = try? manager.contentsOfDirectory(atPath: projects) else { return [] }

        return folders.flatMap { folder -> [String] in
            let path = (projects as NSString).appendingPathComponent(folder)
            guard let names = try? manager.contentsOfDirectory(atPath: path) else { return [] }
            return names.filter { $0.hasSuffix(".jsonl") }.map { (path as NSString).appendingPathComponent($0) }
        }
    }

    private func readCached(_ path: String, _ environment: String) -> Conversation? {
        guard let attributes = try? FileManager.default.attributesOfItem(atPath: path),
              let written = attributes[.modificationDate] as? Date,
              let length = (attributes[.size] as? NSNumber)?.int64Value else { return nil }

        if let known = read[path], known.written == written, known.length == length {
            return known.conversation
        }

        guard let (head, tail) = Self.pieces(path, length) else { return nil }
        let id = ((path as NSString).lastPathComponent as NSString).deletingPathExtension
        let conversation = ConversationFile.read(
            id: id, environment: environment, head: head, tail: tail,
            lastUsed: Int64(written.timeIntervalSince1970))
        read[path] = Known(written: written, length: length, conversation: conversation)
        return conversation
    }

    /// The first and the last pieces of a file. Claude Code may be writing it: it is only read.
    private static func pieces(_ path: String, _ length: Int64) -> (String, String)? {
        guard let file = FileHandle(forReadingAtPath: path) else { return nil }
        defer { try? file.close() }

        let head = piece(file, from: 0, count: min(length, Int64(ConversationFile.headBytes)))
        let tailLength = min(length, Int64(ConversationFile.tailBytes))
        let tail = piece(file, from: length - tailLength, count: tailLength)
        return (head, tail)
    }

    private static func piece(_ file: FileHandle, from: Int64, count: Int64) -> String {
        guard (try? file.seek(toOffset: UInt64(from))) != nil,
              let data = try? file.read(upToCount: Int(count)) else { return "" }
        return String(decoding: data, as: UTF8.self)
    }
}
