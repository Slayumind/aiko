import Foundation

/// Puts Aiko's command folder at the front of the user PATH and takes it out again.
///
/// Only the one entry is ever touched. Everything else in the value keeps its order, its case and
/// its spelling, including variables such as %USERPROFILE% the person wrote there.
public enum UserPathList {
    public static func addToFront(
        _ platform: PlatformConventions, _ value: String?, _ folder: String
    ) -> String {
        var entries = without(platform, value, folder)
        entries.insert(folder, at: 0)
        return entries.joined(separator: String(platform.pathListSeparator))
    }

    public static func remove(
        _ platform: PlatformConventions, _ value: String?, _ folder: String
    ) -> String {
        without(platform, value, folder).joined(separator: String(platform.pathListSeparator))
    }

    public static func contains(
        _ platform: PlatformConventions, _ value: String?, _ folder: String
    ) -> Bool {
        split(platform, value).contains { RealClaude.sameFolder($0, folder) }
    }

    private static func without(
        _ platform: PlatformConventions, _ value: String?, _ folder: String
    ) -> [String] {
        split(platform, value).filter { !RealClaude.sameFolder($0, folder) }
    }

    private static func split(_ platform: PlatformConventions, _ value: String?) -> [String] {
        (value ?? "")
            .split(separator: platform.pathListSeparator, omittingEmptySubsequences: false)
            .map { String($0) }
            .filter { !$0.trimmingCharacters(in: .whitespaces).isEmpty }
    }
}
