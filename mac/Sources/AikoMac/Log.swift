import AikoKit
import Foundation

/// A short log of what Aiko did. It answers days when the menu bar stays quiet and nothing on
/// screen says why. The twin of Log.cs on Windows, down to the file it writes.
///
/// Only our own events go in here. Never a token, never anything Claude Code handed us.
enum Log {
    private static let maxBytes = 256 * 1024
    private static let lock = NSLock()

    static var filePath: String { Store.folders.logFile }

    static func write(_ message: String) {
        lock.lock()
        defer { lock.unlock() }

        let path = filePath
        let manager = FileManager.default
        try? manager.createDirectory(
            atPath: (path as NSString).deletingLastPathComponent, withIntermediateDirectories: true)

        // The log is for the last little while, not forever. Past the limit it starts over, which
        // is simpler than rotating files and enough for one bug report.
        if let size = try? manager.attributesOfItem(atPath: path)[.size] as? Int, size > maxBytes {
            try? manager.removeItem(atPath: path)
        }

        let line = "\(stamp())  \(message)\n"
        guard let bytes = line.data(using: .utf8) else { return }

        if let handle = FileHandle(forWritingAtPath: path) {
            defer { try? handle.close() }
            _ = try? handle.seekToEnd()
            try? handle.write(contentsOf: bytes)
        } else {
            try? bytes.write(to: URL(fileURLWithPath: path))
        }
    }

    private static func stamp() -> String {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = .current
        let now = Date()
        let parts = calendar.dateComponents([.hour, .minute, .second, .nanosecond], from: now)
        return String(
            format: "%02d:%02d:%02d.%03d",
            parts.hour ?? 0, parts.minute ?? 0, parts.second ?? 0, (parts.nanosecond ?? 0) / 1_000_000)
    }
}
