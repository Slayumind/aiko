import AikoKit
import Foundation

/// The memory file, as the settings window reads and changes it. The twin of MemoryStore.cs.
///
/// The bridge writes the same file from hooks and from the memory server of every open session,
/// so a change here follows the bridge's rules (aiko-bridge/Disk.swift): take the lock file, read
/// the file again, change what was read, write through a temporary file. Without the lock the
/// window would write back the list it showed a minute ago and undo a fact the model saved since.
///
/// The lock and the write are written a second time here: Disk.swift belongs to the bridge
/// program, and AikoKit does not touch the disk.
enum MemoryStore {
    static var path: String { Store.folders.memoryFile }

    /// What is in the file now. A file that cannot be read shows as an empty memory; it is kept
    /// aside as ".bad" only when something is written over it.
    static func load() -> MemoryFile {
        guard let text = try? read() else {
            return .empty
        }

        return MemoryFile.fromJson(text).memory
    }

    /// Reads the file under the lock, applies the change and writes the result when it changed.
    /// Nil when the file could not be locked, read or written: then nothing changed.
    static func change(_ change: (MemoryFile) -> MemoryHandEdit) -> MemoryHandEdit? {
        do {
            return try withLock {
                let read = MemoryFile.fromJson(try read())
                let edit = change(read.memory)
                if edit.changed {
                    try write(edit.memory.toJson(), keepBadFile: read.unreadable)
                }

                return edit
            }
        } catch {
            // The step only: the path has the person's name in it.
            Log.write("memory: the file could not be changed (\(error))")
            return nil
        }
    }

    /// For "Open file": a memory that was never written has no file to open yet.
    static func ensureFile() -> Bool {
        do {
            try withLock {
                if !FileManager.default.fileExists(atPath: path) {
                    try write(MemoryFile.empty.toJson(), keepBadFile: false)
                }
            }
            return true
        } catch {
            return false
        }
    }

    private enum Failed: Error {
        case lock
        case read
        case write
    }

    /// Nil only when there is no file. Bytes that are not UTF-8 are read as broken text, so the
    /// memory is marked unreadable and the file is kept aside before anything replaces it.
    private static func read() throws -> String? {
        guard FileManager.default.fileExists(atPath: path) else {
            return nil
        }

        guard let data = try? Data(contentsOf: URL(fileURLWithPath: path)) else {
            throw Failed.read
        }

        var text = String(decoding: data, as: UTF8.self)
        if text.unicodeScalars.first == "\u{FEFF}" {
            text.unicodeScalars.removeFirst()
        }
        return text
    }

    /// The same lock file and the same patience as the bridge: 80 tries, 25 ms apart. The lock
    /// opens by itself when the file is closed.
    private static func withLock<T>(_ work: () throws -> T) throws -> T {
        let folder = (path as NSString).deletingLastPathComponent
        guard (try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)) != nil
        else {
            throw Failed.lock
        }

        let descriptor = open(path + ".lock", O_CREAT | O_RDWR | O_CLOEXEC, 0o600)
        guard descriptor >= 0 else {
            throw Failed.lock
        }
        defer { close(descriptor) }

        var attempt = 0
        while flock(descriptor, LOCK_EX | LOCK_NB) != 0 {
            guard attempt < lockAttempts else {
                throw Failed.lock
            }
            attempt += 1
            usleep(lockPauseMicroseconds)
        }

        return try work()
    }

    private static let lockAttempts = 80
    private static let lockPauseMicroseconds: UInt32 = 25_000

    /// Written beside the old file and moved over it in one step, so the bridge never reads half
    /// a memory.
    private static func write(_ json: String, keepBadFile: Bool) throws {
        if keepBadFile && FileManager.default.fileExists(atPath: path) && rename(path, path + ".bad") != 0 {
            throw Failed.write
        }

        let temporary = "\(path).\(ProcessInfo.processInfo.processIdentifier).tmp"
        guard (try? json.write(toFile: temporary, atomically: false, encoding: .utf8)) != nil else {
            throw Failed.write
        }

        if rename(temporary, path) != 0 {
            try? FileManager.default.removeItem(atPath: temporary)
            throw Failed.write
        }
    }
}
