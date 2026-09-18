import AikoKit
import Foundation

/// The few ways the bridge touches the disk. Nothing here decides anything.
enum Disk {
    static func readIfThere(_ path: String) -> String? {
        try? String(contentsOfFile: path, encoding: .utf8)
    }

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
