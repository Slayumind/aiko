import AikoKit
import Foundation

/// The last hook events of the sessions of the terminal (D-352, D-360): the files the bridge
/// writes, one per session. The twin of TerminalEvents.cs. No timers: the folder tells when the
/// bridge put a file in it, as the bridge writes each one beside and moves it over.
@MainActor
final class TerminalEvents {
    private let folder = Store.folders.terminalEventsFolder
    private var source: DispatchSourceFileSystemObject?
    private var seen: [String: Date] = [:]

    /// A session did something: its id and the event, on the main thread.
    var changed: ((String, TerminalEvent) -> Void)?

    /// The files already there belong to sessions of an earlier run, so only what is written from
    /// now on is told.
    init() {
        try? FileManager.default.createDirectory(atPath: folder, withIntermediateDirectories: true)
        for name in names() {
            seen[name] = modified(name)
        }
        watch()
    }

    /// The conversation the last hook of the session named; empty when there is none.
    static func lastConversation(_ id: String) -> String {
        guard let file = TerminalEvent.fileName(id) else { return "" }
        let path = (Store.folders.terminalEventsFolder as NSString).appendingPathComponent(file)
        return TerminalEvent.fromJson(try? String(contentsOfFile: path, encoding: .utf8))?.conversation ?? ""
    }

    /// The settings file with the hook of the terminal, written when it is not there or names
    /// another bridge. Empty when there is no bridge or the file cannot be written: the session then
    /// starts without the hook, and its row knows only what the title says.
    static func hooksFile() -> String {
        guard let bridge = BridgePath.current() else { return "" }
        let path = Store.folders.terminalHooksFile
        let text = TerminalHooks.settingsJson(bridge)
        if (try? String(contentsOfFile: path, encoding: .utf8)) != text {
            do {
                try FileManager.default.createDirectory(
                    atPath: (path as NSString).deletingLastPathComponent, withIntermediateDirectories: true)
                try text.write(toFile: path, atomically: true, encoding: .utf8)
            } catch {
                return ""
            }
        }
        return path
    }

    /// The session was closed: its file goes, so a session started later never sees it.
    func forget(_ id: String) {
        guard let file = TerminalEvent.fileName(id) else { return }
        try? FileManager.default.removeItem(atPath: (folder as NSString).appendingPathComponent(file))
        seen[file] = nil
    }

    private func watch() {
        let descriptor = open(folder, O_EVTONLY)
        guard descriptor >= 0 else { return }
        let source = DispatchSource.makeFileSystemObjectSource(fileDescriptor: descriptor, eventMask: .write, queue: .main)
        source.setEventHandler { [weak self] in
            MainActor.assumeIsolated { self?.look() }
        }
        source.setCancelHandler { close(descriptor) }
        self.source = source
        source.resume()
    }

    /// Something in the folder changed: the files newer than the last look are read.
    private func look() {
        for name in names() {
            let time = modified(name)
            guard time != seen[name] else { continue }
            seen[name] = time
            let id = (name as NSString).deletingPathExtension
            guard TerminalEvent.fileName(id) != nil,
                  let text = try? String(contentsOfFile: (folder as NSString).appendingPathComponent(name), encoding: .utf8),
                  let event = TerminalEvent.fromJson(text) else { continue }
            changed?(id, event)
        }
    }

    private func names() -> [String] {
        ((try? FileManager.default.contentsOfDirectory(atPath: folder)) ?? []).filter { $0.hasSuffix(".json") }
    }

    private func modified(_ name: String) -> Date? {
        (try? FileManager.default.attributesOfItem(atPath: (folder as NSString).appendingPathComponent(name)))?[.modificationDate] as? Date
    }
}
