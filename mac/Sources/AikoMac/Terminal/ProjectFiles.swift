import Foundation

/// The files of a session's folder for "@", as paths relative to it with "/". The twin of
/// ProjectFiles.cs.
///
/// A git folder answers with git ls-files: the tracked files and the new ones it does not ignore.
/// Any other folder is walked, without the folders nobody mentions (.git, node_modules, bin, obj),
/// up to mostFiles. The list is made per folder off the main thread; until it is ready, "@" shows
/// nothing. A list older than freshFor is shown once more while a new one is made, so files made
/// during the session come into "@" too.
@MainActor
enum ProjectFiles {
    private nonisolated static let mostFiles = 5000
    private nonisolated static let gitWait: TimeInterval = 3
    private static let freshFor: TimeInterval = 30
    private nonisolated static let skipped: Set<String> = [".git", "node_modules", "bin", "obj", ".vs", ".idea"]

    private struct Listing {
        var files: [String]?
        let made: Date
    }

    private static var lists: [String: Listing] = [:]

    /// The list if it is ready, or nil; ready is called on the main thread once it is.
    static func get(_ folder: String, ready: @escaping @MainActor () -> Void) -> [String]? {
        var isFolder: ObjCBool = false
        guard FileManager.default.fileExists(atPath: folder, isDirectory: &isFolder), isFolder.boolValue else {
            return []
        }

        guard let listing = lists[folder] else {
            start(folder, then: ready)
            return nil
        }
        guard let files = listing.files else {
            return nil
        }
        if Date().timeIntervalSince(listing.made) > freshFor {
            // The next "@" gets the new list; this one shows what is known.
            start(folder, keeping: files, then: nil)
        }
        return files
    }

    private static func start(_ folder: String, keeping old: [String]? = nil, then ready: (@MainActor () -> Void)?) {
        lists[folder] = Listing(files: old, made: Date())
        Task.detached(priority: .utility) {
            let files = read(folder)
            await MainActor.run {
                lists[folder] = Listing(files: files, made: Date())
                ready?()
            }
        }
    }

    private nonisolated static func read(_ folder: String) -> [String] {
        fromGit(folder) ?? walk(folder)
    }

    private nonisolated static func fromGit(_ folder: String) -> [String]? {
        guard FileManager.default.fileExists(atPath: (folder as NSString).appendingPathComponent(".git")) else {
            return nil
        }

        let git = Process()
        git.executableURL = URL(fileURLWithPath: "/usr/bin/git")
        // Names as they are: by default git writes names outside ASCII as quoted octal escapes.
        git.arguments = ["-c", "core.quotePath=false", "-C", folder, "ls-files", "--cached", "--others", "--exclude-standard"]
        let output = Pipe()
        git.standardOutput = output
        git.standardError = FileHandle.nullDevice
        do {
            try git.run()
        } catch {
            return nil
        }

        // Read to the end even past the limit: a full pipe would hold git until it is stopped, and
        // the list would fall back to the walk that knows nothing of .gitignore.
        let stopper = DispatchWorkItem { if git.isRunning { git.terminate() } }
        DispatchQueue.global().asyncAfter(deadline: .now() + gitWait, execute: stopper)
        let data = output.fileHandleForReading.readDataToEndOfFile()
        git.waitUntilExit()
        stopper.cancel()
        guard git.terminationReason == .exit, git.terminationStatus == 0 else { return nil }

        let lines = String(decoding: data, as: UTF8.self).split(separator: "\n", omittingEmptySubsequences: true)
        return lines.prefix(mostFiles).map(String.init)
    }

    private nonisolated static func walk(_ folder: String) -> [String] {
        var files: [String] = []
        var waiting = [folder]
        let manager = FileManager.default
        let start = folder.hasSuffix("/") ? folder : folder + "/"
        while !waiting.isEmpty && files.count < mostFiles {
            let here = waiting.removeFirst()
            guard let names = try? manager.contentsOfDirectory(atPath: here) else { continue }
            for name in names.sorted() {
                let path = (here as NSString).appendingPathComponent(name)
                var isFolder: ObjCBool = false
                guard manager.fileExists(atPath: path, isDirectory: &isFolder) else { continue }
                if isFolder.boolValue {
                    if !skipped.contains(name) { waiting.append(path) }
                } else if files.count < mostFiles {
                    files.append(String(path.dropFirst(start.count)))
                }
            }
        }
        return files
    }
}
