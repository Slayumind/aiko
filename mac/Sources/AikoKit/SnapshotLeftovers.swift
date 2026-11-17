import Foundation

/// The temporary files a bridge left in the folder of snapshots (AIKO-267). The twin of
/// SnapshotLeftovers.cs.
///
/// A bridge writes "claude.json.1234.tmp" and moves it over "claude.json". A bridge that was
/// stopped in the middle left such a file behind, and nothing ever removed it. Aiko removes them
/// when it starts; a file younger than a minute may still belong to a bridge that is writing right
/// now, so it stays.
public enum SnapshotLeftovers {
    public static let keepFor: TimeInterval = 60

    /// The names of the files to remove: "<name>.json.<process id>.tmp", older than keepFor.
    public static func stale(_ files: [(name: String, written: Date)], now: Date) -> [String] {
        files
            .filter { isLeftover($0.name) && now.timeIntervalSince($0.written) > keepFor }
            .map(\.name)
    }

    private static func isLeftover(_ name: String) -> Bool {
        let tail = ".tmp"
        guard name.hasSuffix(tail) else { return false }

        let stem = String(name.dropLast(tail.count))
        guard let dot = stem.lastIndex(of: "."), dot > stem.startIndex else { return false }

        let digits = stem[stem.index(after: dot)...]
        let before = stem[..<dot]
        return !digits.isEmpty
            && digits.unicodeScalars.allSatisfy { $0.value >= 48 && $0.value <= 57 }
            && before.hasSuffix(".json")
            && before.count > ".json".count
    }
}
