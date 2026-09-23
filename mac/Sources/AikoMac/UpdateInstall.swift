import AikoKit
import AppKit
import Foundation

/// The app side of the macOS updater: where the files come from, where this copy sits, and how a
/// new version is started. The rule and the bundle swap live in AikoKit's UpdateInstaller, where
/// they have their own cases.
///
/// Nothing here starts by itself. The daily check only says that a newer version is out;
/// downloading begins when the person presses the button.
enum UpdateInstall {
    private static let repository = "https://github.com/Slayumind/aiko"

    /// The version that is in place and waiting for a restart, or nil while there is none.
    private(set) static nonisolated(unsafe) var readyVersion: String?

    static func download(version: String) async -> InstallOutcome {
        let installer = UpdateInstaller(
            base: "\(repository)/releases/download/v\(version)",
            publicKeyPem: UpdateKey.pem,
            bundle: Bundle.main.bundleURL,
            fetch: { address, timeout in await AikoHttp.bytes(address, timeout: timeout) },
            run: run,
            log: { Log.write($0) })

        let outcome = await installer.install(version: version)
        if outcome.step == .ready {
            readyVersion = version
        }

        return outcome
    }

    /// Starts the new bundle and lets this one go. `-n` because the old process is still running
    /// and macOS would otherwise only bring it to the front.
    @MainActor
    static func relaunch() {
        guard let version = readyVersion else { return }

        Log.write("update: restarting into \(version)")
        _ = run("/usr/bin/open", ["-n", Bundle.main.bundleURL.path])
        NSApp.terminate(nil)
    }

    private static let run: @Sendable (String, [String]) -> Bool = { tool, arguments in
        let process = Process()
        process.executableURL = URL(fileURLWithPath: tool)
        process.arguments = arguments
        process.standardOutput = FileHandle.nullDevice
        process.standardError = FileHandle.nullDevice

        do {
            try process.run()
            process.waitUntilExit()
            return process.terminationStatus == 0
        } catch {
            return false
        }
    }
}
