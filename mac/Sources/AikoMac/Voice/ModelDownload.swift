import AikoKit
import CryptoKit
import Foundation

/// The download of the speech model (D-313, D-316), one for the whole app: the settings page can
/// close and open again and still see it going. The twin of ModelDownload.cs.
///
/// The file grows as "<name>.part" next to its place and gets its real name only after its SHA256
/// matched, so a model that is there is a model that is whole. A stopped download keeps its part
/// and goes on from there next time.
@MainActor
enum ModelDownload {
    private static var model: String { Store.folders.voiceModelFile }
    private static var part: String { model + ".part" }
    private static var current: Task<Void, Never>?

    /// Bytes so far. The page reads it on each change.
    private(set) static var done: Int64 = 0

    /// Something changed: more bytes came, or the download ended. The settings page redraws.
    static var changed: (() -> Void)?

    /// Why the last download ended without a model, or nil.
    private(set) static var problem: String?

    static var running: Bool { current != nil }

    /// The size of the model file, or nil when it is not there.
    static var modelBytes: Int64? {
        (try? FileManager.default.attributesOfItem(atPath: model)[.size] as? NSNumber)?.int64Value
    }

    static func start() {
        guard current == nil, modelBytes != VoiceModel.bytes else { return }
        problem = nil
        let model = self.model
        let part = self.part
        current = Task.detached(priority: .utility) {
            let result = await Fetch(model: model, part: part).run()
            await MainActor.run {
                Log.write("voice model download: \(result ?? "done")")
                ModelDownload.problem = result
                ModelDownload.current = nil
                ModelDownload.changed?()
            }
        }
        changed?()
    }

    static func stop() {
        current?.cancel()
    }

    /// Removes the model and any part of it: the page offers this when the model is there.
    static func remove() {
        stop()
        try? FileManager.default.removeItem(atPath: model)
        try? FileManager.default.removeItem(atPath: part)
        done = 0
        changed?()
    }

    fileprivate static func report(_ bytes: Int64) {
        done = bytes
        changed?()
    }
}

/// One run of the download, off the main thread.
private struct Fetch: Sendable {
    let model: String
    let part: String

    /// Nil when the model is in place, or a short reason for the log and the page.
    func run() async -> String? {
        let manager = FileManager.default
        try? manager.createDirectory(atPath: (model as NSString).deletingLastPathComponent, withIntermediateDirectories: true)
        var have = ((try? manager.attributesOfItem(atPath: part)[.size]) as? NSNumber)?.int64Value ?? 0
        // A part as long as the model itself asks the server for nothing, and it answers 416 every
        // time: such a part is read again from the start.
        if have >= VoiceModel.bytes {
            try? manager.removeItem(atPath: part)
            have = 0
        }
        if !manager.fileExists(atPath: part) {
            manager.createFile(atPath: part, contents: nil)
        }
        guard let file = FileHandle(forUpdatingAtPath: part) else { return "the part could not be opened" }
        defer { try? file.close() }

        // Going on from a part: what is already there counts into the hash first.
        let state = State()
        if have > 0 {
            while let piece = try? file.read(upToCount: 1 << 20), !piece.isEmpty {
                state.hash.update(data: piece)
            }
        }
        state.written = have
        await report(have)

        // A large file over a slow line: the time is for the whole file, the stop button is the
        // real limit.
        let resumed = have > 0
        let status = await AikoHttp.stream(VoiceModel.url, from: have, timeout: 2 * 3600) { status, piece in
            guard status == 200 || status == 206 else { return }
            // The page hears each whole megabyte, not each piece of a few kilobytes.
            if try state.write(piece, to: file, startingOver: status == 200 && resumed) {
                let written = state.written
                Task { await report(written) }
            }
        }

        if Task.isCancelled { return "stopped" }
        guard let status else { return "the line broke" }
        guard status == 200 || status == 206 else { return "HTTP \(status)" }

        let digest = state.hash.finalize().map { String(format: "%02x", $0) }.joined()
        guard state.written == VoiceModel.bytes, digest == VoiceModel.sha256 else {
            try? manager.removeItem(atPath: part)
            return "the file did not match"
        }
        try? manager.removeItem(atPath: model)
        do {
            try manager.moveItem(atPath: part, toPath: model)
        } catch {
            return "the model could not be put in place"
        }
        return nil
    }

    private func report(_ bytes: Int64) async {
        await MainActor.run { ModelDownload.report(bytes) }
    }

    /// The hash and the count, touched only by the pieces of one download, one after another.
    private final class State: @unchecked Sendable {
        var hash = SHA256()
        var written: Int64 = 0
        private var restarted = false

        /// A server that ignores the range sends the whole file again (200): the part starts over.
        /// True when the count passed a whole megabyte.
        func write(_ piece: Data, to file: FileHandle, startingOver: Bool) throws -> Bool {
            if startingOver && !restarted {
                restarted = true
                try file.truncate(atOffset: 0)
                hash = SHA256()
                written = 0
            }
            try file.seek(toOffset: UInt64(written))
            try file.write(contentsOf: piece)
            hash.update(data: piece)
            let before = written >> 20
            written += Int64(piece.count)
            return written >> 20 != before
        }
    }
}
