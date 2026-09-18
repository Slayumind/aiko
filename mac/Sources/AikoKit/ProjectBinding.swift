import Foundation

/// Which environment Claude Code should start in, for the folder it is started from.
///
/// It works like includeIf in git: binding D:\work covers every folder inside it. When two bound
/// folders contain the working folder, the deeper one wins, so D:\work\personal-side-project can
/// belong to the other environment. A folder bound nowhere gets the default environment.
public enum ProjectBinding {
    public static func environmentFor(
        _ platform: PlatformConventions,
        workingDirectory: String,
        settings: EnvironmentSettings,
        userProfile: String
    ) -> AikoEnvironment? {
        boundEnvironmentFor(platform, workingDirectory: workingDirectory, settings: settings)
            ?? settings.defaultEnvironmentIn(platform, userProfile)
    }

    /// Only a real binding, with no default to fall back on.
    public static func boundEnvironmentFor(
        _ platform: PlatformConventions,
        workingDirectory: String,
        settings: EnvironmentSettings
    ) -> AikoEnvironment? {
        var best: AikoEnvironment?
        var bestLength = -1

        let path = normalize(platform, workingDirectory)
        for environment in settings.environments {
            for folder in environment.projectFolders {
                let root = normalize(platform, folder)
                if root.count > bestLength && contains(platform, root: root, path: path) {
                    best = environment
                    bestLength = root.count
                }
            }
        }

        return best
    }

    /// D:\work contains D:\work and D:\work\app, and not D:\workshop.
    private static func contains(_ platform: PlatformConventions, root: String, path: String) -> Bool {
        let separator = String(platform.directorySeparator)
        let lowerRoot = root.lowercased()
        let lowerPath = path.lowercased()

        return lowerPath == lowerRoot
            || lowerPath.hasPrefix(lowerRoot + separator)
            || (root.hasSuffix(separator) && lowerPath.hasPrefix(lowerRoot))
    }

    private static func normalize(_ platform: PlatformConventions, _ path: String) -> String {
        let other: Character = platform.directorySeparator == "/" ? "\\" : "/"
        let unified = String(
            path.trimmingCharacters(in: .whitespacesAndNewlines)
                .map { $0 == other ? platform.directorySeparator : $0 })

        // "D:\" must keep its separator, or it turns into "D:", which means the current folder on D.
        return unified.count > 3 ? PathText.trimOneEndSeparator(unified) : unified
    }
}
