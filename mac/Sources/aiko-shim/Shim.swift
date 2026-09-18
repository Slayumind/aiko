import AikoKit
import Foundation

/// The `claude` that stands in PATH before the real one, under its own name and under the command
/// name of every environment. Which environment a start belongs to is decided by `ShimLaunch`;
/// this starts the program and hands the exit code back.
enum Shim {
    /// For the release check: the file starts and its runtime is complete, with no Claude Code
    /// needed.
    static let selfTestVariable = "AIKO_SHIM_SELF_TEST"

    /// What a shell answers for a command it cannot find. The Windows shim answers 9009, which is
    /// the same answer from cmd.
    static let claudeMissing: Int32 = 127

    static func run() -> Int32 {
        let platform = PlatformConventions.macOS
        let invokedAs = CommandLine.arguments.first ?? RealClaude.executableName(platform)
        let seen = RealClaude.parseSeen(platform, ProcessInfo.processInfo.environment[RealClaude.seenVariable])
            + ownFolders()

        if ProcessInfo.processInfo.environment[selfTestVariable] == "1" {
            print("aiko shim ok")
            return 0
        }

        guard let real = RealClaude.find(
            platform, ProcessInfo.processInfo.environment["PATH"], seen,
            { FileManager.default.isExecutableFile(atPath: $0) })
        else {
            FileHandle.standardError.write(Data(
                "Aiko: the real claude isn't in PATH. Install Claude Code, or open a new terminal if you just did.\n".utf8))
            return claudeMissing
        }

        let process = Process()
        process.executableURL = URL(fileURLWithPath: real)
        process.arguments = Array(CommandLine.arguments.dropFirst())
        process.environment = environmentFor(plan(platform, invokedAs: invokedAs), seen: seen, platform: platform)

        // Ctrl+C reaches every process in the terminal. Claude Code handles it; the shim must not
        // quit first and leave the terminal waiting on nothing.
        signal(SIGINT, SIG_IGN)

        guard (try? process.run()) != nil else {
            return 1
        }

        process.waitUntilExit()

        // A program killed by a signal has no exit code of its own; shells report it as 128 plus
        // the signal, and whatever started the shim expects the same.
        return process.terminationReason == .uncaughtSignal
            ? 128 + process.terminationStatus
            : process.terminationStatus
    }

    /// The folders this shim was started from. A shim never runs Claude Code from a folder in this
    /// list, so two installs in PATH cannot start each other for ever.
    ///
    /// Both the path we were called by and the path it points at: the commands folder holds links
    /// to the program inside the app bundle, and either one is still a shim.
    private static func ownFolders() -> [String] {
        guard let executable = Bundle.main.executablePath else {
            return []
        }

        let url = URL(fileURLWithPath: executable)
        let folders = [
            url.deletingLastPathComponent().path,
            url.resolvingSymlinksInPath().deletingLastPathComponent().path,
        ]

        return folders.reduce(into: [String]()) { kept, folder in
            if !kept.contains(where: { RealClaude.sameFolder($0, folder) }) {
                kept.append(folder)
            }
        }
    }

    /// Any failure here, a broken environments file included, means Claude Code starts the way it
    /// would without Aiko.
    private static func plan(_ platform: PlatformConventions, invokedAs: String) -> ShimPlan {
        let folders = AikoFolders.forThisMac()
        let settings = EnvironmentSettings.fromJson(
            try? String(contentsOfFile: folders.environmentsFile, encoding: .utf8))

        return ShimLaunch.decide(
            platform,
            invokedAs: invokedAs,
            workingDirectory: FileManager.default.currentDirectoryPath,
            settings: settings,
            userProfile: NSHomeDirectory())
    }

    private static func environmentFor(
        _ plan: ShimPlan, seen: [String], platform: PlatformConventions
    ) -> [String: String] {
        var environment = ProcessInfo.processInfo.environment
        environment[RealClaude.seenVariable] = RealClaude.formatSeen(platform, seen)

        for change in plan.variableChanges {
            // A nil value takes the variable out, which is not the same as an empty one.
            environment[change.name] = change.value
        }

        return environment
    }
}
