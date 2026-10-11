import AikoKit
import Foundation

/// The few ways the bridge touches the disk. Nothing here decides anything.
enum Disk {
    static func readIfThere(_ path: String) -> String? {
        try? String(contentsOfFile: path, encoding: .utf8)
    }

    /// The memory file. Nil only when there is no file: a file that is there and cannot be read is
    /// an error, because "no file" lets the next save write an empty memory over it. Bytes that
    /// are not UTF-8 are read as broken text for the same reason, and then the memory is marked
    /// unreadable and the file is kept aside before anything replaces it.
    static func readMemory(_ path: String) throws -> String? {
        guard FileManager.default.fileExists(atPath: path) else {
            return nil
        }

        let data = try Data(contentsOf: URL(fileURLWithPath: path))
        var text = String(decoding: data, as: UTF8.self)
        if text.unicodeScalars.first == "\u{FEFF}" {
            text.unicodeScalars.removeFirst()
        }
        return text
    }

    /// One writer at a time. Hooks of several sessions, the memory server and the app all read the
    /// memory file, change it and write it back; without a gate the slower one would undo the
    /// change of the other. The gate is a lock on a file beside it, tried for two seconds, and it
    /// opens by itself when this process ends.
    static func withLock<T>(_ path: String, _ work: () throws -> T) throws -> T {
        let folder = (path as NSString).deletingLastPathComponent
        try FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)

        let descriptor = open(path, O_CREAT | O_RDWR | O_CLOEXEC, 0o600)
        guard descriptor >= 0 else {
            throw Bridge.Failed("could not open \(path)")
        }
        defer { close(descriptor) }

        var attempt = 0
        while flock(descriptor, LOCK_EX | LOCK_NB) != 0 {
            guard attempt < lockAttempts else {
                throw Bridge.Failed("could not lock \(path)")
            }
            attempt += 1
            usleep(lockPauseMicroseconds)
        }

        return try work()
    }

    /// A memory file that was not a memory is kept as ".bad" before the new one takes its place,
    /// so a slip in a hand edit never costs the person their facts.
    static func writeMemory(_ path: String, _ json: String, keepBadFile: Bool) throws {
        if keepBadFile && FileManager.default.fileExists(atPath: path) && rename(path, path + ".bad") != 0 {
            throw Bridge.Failed("could not keep \(path) aside")
        }

        try writeInOneStep(FileWrite(path: path, text: json))
    }

    private static let lockAttempts = 80
    private static let lockPauseMicroseconds: UInt32 = 25_000

    /// Write beside the old file and move it over in one step, so the tray never sees half a file.
    /// `rename` replaces the old name without a moment where neither is there.
    static func writeInOneStep(_ file: FileWrite) throws {
        let folder = (file.path as NSString).deletingLastPathComponent
        try FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)

        let temporary = temporaryName(file.path)
        try file.text.write(toFile: temporary, atomically: false, encoding: .utf8)
        if rename(temporary, file.path) != 0 {
            try? FileManager.default.removeItem(atPath: temporary)
            throw Bridge.Failed("could not replace \(file.path)")
        }
    }

    /// The same for a whole folder, so Claude Code never copies a plugin with half the files in it.
    /// Two runs racing for the same folder write the same content; the one that loses the move just
    /// drops its copy.
    static func writeFolderInOneStep(_ folder: String, _ files: [(path: String, content: String)]) throws {
        let manager = FileManager.default
        let temporary = temporaryName(folder)

        for file in files {
            let path = (temporary as NSString).appendingPathComponent(file.path)
            try manager.createDirectory(
                atPath: (path as NSString).deletingLastPathComponent, withIntermediateDirectories: true)
            try file.content.write(toFile: path, atomically: false, encoding: .utf8)
        }

        try manager.createDirectory(
            atPath: (folder as NSString).deletingLastPathComponent, withIntermediateDirectories: true)

        // Renaming onto a folder that another run has just finished fails, and that is fine: the
        // content of both is the same.
        if rename(temporary, folder) != 0 {
            try? manager.removeItem(atPath: temporary)
            if !manager.fileExists(atPath: folder) {
                throw Bridge.Failed("could not write \(folder)")
            }
        }
    }

    private static func temporaryName(_ path: String) -> String {
        "\(path).\(ProcessInfo.processInfo.processIdentifier).tmp"
    }
}
