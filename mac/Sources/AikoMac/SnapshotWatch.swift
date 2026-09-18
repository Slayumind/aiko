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
    private var source: DispatchSourceFileSystemObject?

    init(folder: String) {
        self.folder = folder
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        readAll()
        watch()
    }

    func stop() {
        source?.cancel()
        source = nil
    }

    private func watch() {
        let descriptor = open(folder, O_EVTONLY)
        guard descriptor >= 0 else {
            Log.write("could not watch \(folder)")
            return
        }

        // The bridge moves a temporary file over the old one, so every new answer changes the
        // folder itself. Watching the folder is enough, and it survives a file being replaced.
        let source = DispatchSource.makeFileSystemObjectSource(
            fileDescriptor: descriptor,
            eventMask: [.write, .rename, .delete],
            queue: .main)

        source.setEventHandler { [weak self] in
            self?.readAll()
            self?.updated?()
        }
        source.setCancelHandler { close(descriptor) }
        source.resume()
        self.source = source
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
