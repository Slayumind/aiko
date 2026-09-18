import AikoKit
import Foundation

/// Watches the folder the bridge writes into. No timers: the file system says when something
/// changed, and in between Aiko sleeps (D-161). The twin of SnapshotWatcher.cs on Windows.
@MainActor
final class SnapshotWatch {
    /// Keyed by the file name, which is the name of the Claude Code config folder. Matching those
    /// files with the environments the user named is the core's job, not ours.
    private(set) var byFile: [String: LimitSnapshot] = [:]

    var updated: (() -> Void)?

    private let folder: String
    private var stream: FSEventStreamRef?

    init(folder: String) {
        self.folder = folder
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        readAll()
        watch()
    }

    func stop() {
        guard let stream else { return }
        FSEventStreamStop(stream)
        FSEventStreamInvalidate(stream)
        FSEventStreamRelease(stream)
        self.stream = nil
    }

    private func watch() {
        // FSEvents, not a folder descriptor: the bridge moves a ready file over the old one, but a
        // writer that changes a file in place leaves the folder itself untouched, and a descriptor
        // on the folder would say nothing. Windows hears both, and so must this (D-244).
        var context = FSEventStreamContext(
            version: 0,
            info: Unmanaged.passUnretained(self).toOpaque(),
            retain: nil,
            release: nil,
            copyDescription: nil)

        let callback: FSEventStreamCallback = { _, info, _, _, _, _ in
            guard let info else { return }
            let watch = Unmanaged<SnapshotWatch>.fromOpaque(info).takeUnretainedValue()
            MainActor.assumeIsolated {
                watch.reload()
            }
        }

        guard let stream = FSEventStreamCreate(
            nil,
            callback,
            &context,
            [folder] as CFArray,
            FSEventStreamEventId(kFSEventStreamEventIdSinceNow),
            0.05,
            UInt32(kFSEventStreamCreateFlagFileEvents | kFSEventStreamCreateFlagNoDefer | kFSEventStreamCreateFlagUseCFTypes))
        else {
            Log.write("could not watch \(folder)")
            return
        }

        FSEventStreamSetDispatchQueue(stream, .main)
        FSEventStreamStart(stream)
        self.stream = stream
    }

    private func reload() {
        readAll()
        Log.write("snapshots read again: \(byFile.count)")
        updated?()
    }

    /// Reading every file costs nothing: there are one or two of them, and they are a few hundred
    /// bytes each. A file caught mid-swap simply parses as no data and comes back on the next event.
    private func readAll() {
        guard let names = try? FileManager.default.contentsOfDirectory(atPath: folder) else {
            return
        }

        var found: [String: LimitSnapshot] = [:]
        for file in names where file.hasSuffix(".json") {
            let name = (file as NSString).deletingPathExtension
            let path = (folder as NSString).appendingPathComponent(file)
            guard let text = try? String(contentsOfFile: path, encoding: .utf8) else { continue }

            found[name] = SnapshotFile.fromJson(text, fallbackEnvironment: name)
        }

        byFile = found
    }
}
