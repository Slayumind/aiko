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

        // Claude Code takes the place of the shim instead of running under it. A child started by
        // Foundation.Process gets a process group of its own, and a process outside the terminal's
        // foreground group is stopped by SIGTTIN the moment it reads the keyboard: `claude`
        // printed nothing and the session never started, while `claude --version`, which reads
        // nothing, worked. After exec there is one process, in the right group, with the terminal
        // and Ctrl+C its own — which is also why SIGINT is left alone here. Windows has no such
        // thing and starts a child there.
        let environment = environmentFor(plan(platform, invokedAs: invokedAs), seen: seen, platform: platform)
        becomeClaudeCode(real, Array(CommandLine.arguments.dropFirst()), environment)

        // Only a failed exec comes back.
        FileHandle.standardError.write(Data("Aiko: could not start \(real).\n".utf8))
        return 1
    }

    /// Replaces this process with Claude Code. Returns only when the system refused to start it.
    private static func becomeClaudeCode(
        _ path: String, _ arguments: [String], _ environment: [String: String]
    ) {
        var argv = ([path] + arguments).map { strdup($0) }
        argv.append(nil)
        var envp = environment.map { strdup("\($0.key)=\($0.value)") }
        envp.append(nil)

        execve(path, &argv, &envp)

        for pointer in argv + envp {
            free(pointer)
        }
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
