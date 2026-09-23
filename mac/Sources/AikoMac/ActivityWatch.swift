import AikoKit
import AppKit

/// Watches the session files the bridge writes from the persona hooks (D-206). Like SnapshotWatch,
/// no timers: the file system says when a session changed. The twin of Tray/ActivityWatcher.cs.
@MainActor
final class ActivityWatch {
    private let folder: String
    private var stream: FSEventStreamRef?

    /// What every session file said the last time it was read. What is already there when Aiko
    /// starts is history: it is remembered, so the next change has something to compare with, and
    /// it brings no face.
    private var sessions: [String: ActivityRecord] = [:]

    /// What the file said before and what it says now.
    var changed: ((ActivityRecord?, ActivityRecord) -> Void)?

    init(folder: String) {
        self.folder = folder
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        sessions = read()
        watch()
    }

    func stop() {
        guard let stream else { return }
        FSEventStreamStop(stream)
        FSEventStreamInvalidate(stream)
        FSEventStreamRelease(stream)
        self.stream = nil
        changed = nil
    }

    private func watch() {
        var context = FSEventStreamContext(
            version: 0,
            info: Unmanaged.passUnretained(self).toOpaque(),
            retain: nil,
            release: nil,
            copyDescription: nil)

        let callback: FSEventStreamCallback = { _, info, _, _, _, _ in
            guard let info else { return }
            let watch = Unmanaged<ActivityWatch>.fromOpaque(info).takeUnretainedValue()
            MainActor.assumeIsolated { watch.reload() }
        }

        guard let stream = FSEventStreamCreate(
            nil,
            callback,
            &context,
            [folder] as CFArray,
            FSEventStreamEventId(kFSEventStreamEventIdSinceNow),
            0.05,
            UInt32(kFSEventStreamCreateFlagFileEvents | kFSEventStreamCreateFlagNoDefer
                | kFSEventStreamCreateFlagUseCFTypes))
        else {
            Log.write("could not watch \(folder)")
            return
        }

        FSEventStreamSetDispatchQueue(stream, .main)
        FSEventStreamStart(stream)
        self.stream = stream
    }

    private func reload() {
        let now = read()
        let before = sessions
        sessions = now

        for (file, record) in now where before[file] != record {
            changed?(before[file], record)
        }
    }

    /// The bridge moves a ready file over the old one, so a read can land mid-swap. A file that
    /// does not parse is left out and comes back on the next event.
    private func read() -> [String: ActivityRecord] {
        guard let names = try? FileManager.default.contentsOfDirectory(atPath: folder) else {
            return [:]
        }

        var found: [String: ActivityRecord] = [:]
        for file in names where file.hasSuffix(".json") {
            let path = (folder as NSString).appendingPathComponent(file)
            guard let text = try? String(contentsOfFile: path, encoding: .utf8),
                  let record = ActivityRecord.fromJson(text) else {
                continue
            }

            found[file] = record
        }

        return found
    }
}
