import Foundation

public enum UpdateState: Sendable, Equatable {
    /// The check has not been made, or it failed. Aiko says so instead of guessing.
    case unknown
    case upToDate
    case available
}

/// What slayumind.org says about the latest version.
///
/// Aiko asks the site rather than GitHub, the same way the other tools of this author do. The
/// files still come from GitHub; only the number comes from the site.
public struct UpdateInfo: Sendable, Equatable {
    public let latest: String?
    public let downloadUrl: String?

    public init(latest: String?, downloadUrl: String?) {
        self.latest = latest
        self.downloadUrl = downloadUrl
    }

    public static let unknown = UpdateInfo(latest: nil, downloadUrl: nil)

    /// A missing "latest" stays missing.
    ///
    /// The lesson from Sparks: never fill in a fallback version. A client that is told a made up
    /// number decides it is up to date, and every copy in the world goes quiet at once.
    public static func fromJson(_ json: String) -> UpdateInfo {
        if json.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            return unknown
        }

        guard let root = JsonNode.parse(json)?.objectValue else {
            return unknown
        }

        let latest = root["latest"]?.stringValue
        let download = root["downloadUrl"]?.stringValue

        return latest == nil && download == nil ? unknown : UpdateInfo(latest: latest, downloadUrl: download)
    }

    /// Compares as version numbers, not as text: "0.10.0" is newer than "0.9.0", and a string
    /// comparison would say the opposite.
    public func compareWith(_ currentVersion: String) -> UpdateState {
        guard let latest, let newest = VersionNumber(latest), let current = VersionNumber(currentVersion) else {
            return .unknown
        }

        return newest > current ? .available : .upToDate
    }
}

/// The rules of System.Version: two to four whole numbers, a missing part counts as -1, so
/// "1.0" is older than "1.0.0".
public struct VersionNumber: Sendable, Equatable, Comparable {
    public let major: Int
    public let minor: Int
    public let build: Int
    public let revision: Int

    public init?(_ text: String) {
        let parts = text.components(separatedBy: ".")
        guard parts.count >= 2, parts.count <= 4 else { return nil }

        var numbers: [Int] = []
        for part in parts {
            guard let number = VersionNumber.component(part) else { return nil }
            numbers.append(number)
        }

        major = numbers[0]
        minor = numbers[1]
        build = numbers.count > 2 ? numbers[2] : -1
        revision = numbers.count > 3 ? numbers[3] : -1
    }

    /// Whitespace around a part and a leading plus are allowed, the way int.Parse reads a number.
    private static func component(_ text: String) -> Int? {
        var trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmed.hasPrefix("+") {
            trimmed.removeFirst()
        }
        guard !trimmed.isEmpty, trimmed.allSatisfy({ $0.isASCII && $0.isNumber }), let value = Int(trimmed) else {
            return nil
        }
        return value
    }

    public static func < (left: VersionNumber, right: VersionNumber) -> Bool {
        (left.major, left.minor, left.build, left.revision)
            < (right.major, right.minor, right.build, right.revision)
    }
}
