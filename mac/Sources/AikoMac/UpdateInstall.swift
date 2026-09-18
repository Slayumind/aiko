import AikoKit
import AppKit
import CryptoKit
import Foundation

/// How far the install got. The words the user sees are picked from this in the settings page.
/// The twin of InstallStep in UpdateInstall.cs.
enum InstallStep {
    /// This copy is not a bundle Aiko can replace where it sits: a build folder, or an app in a
    /// place this user may not write to. The download page is the only honest answer.
    case notInstalled

    case nothingNewer

    /// GitHub could not be reached, or the zip would not unpack.
    case failed

    /// The new bundle is in place. Restarting starts it.
    case ready

    /// The download did not pass the check. It has been thrown away.
    case refused
}

struct InstallOutcome {
    let step: InstallStep
    let verdict: UpdateVerdict
    let version: String?
}

/// Downloads a new version from GitHub and puts it in place, but only when the release list is
/// signed with our key and the zip matches its line (D-246).
///
/// There is no Velopack on macOS, so the way is the one spike S2 proved: the app downloads the zip
/// itself, which is why nothing is marked with quarantine; `ditto -xk` unpacks it; two renames
/// swap the bundle; `open -n` starts the new one. Nothing here starts by itself — the daily check
/// only says that a newer version is out.
enum UpdateInstall {
    private static let repository = "https://github.com/Slayumind/aiko"

    private static let manifestName = "SHA256SUMS.txt"

    private static let signatureName = "SHA256SUMS.txt.sig"

    /// Long enough for the whole app on a slow line.
    private static let zipTimeout: TimeInterval = 600

    private static let listTimeout: TimeInterval = 30

    /// The version that is in place and waiting for a restart, or nil while there is none.
    private(set) static nonisolated(unsafe) var readyVersion: String?

    static func download(version: String) async -> InstallOutcome {
        let bundle = Bundle.main.bundleURL
        guard canReplace(bundle) else {
            Log.write("update: this copy cannot be replaced where it sits, so nothing is applied")
            return InstallOutcome(step: .notInstalled, verdict: .ok, version: nil)
        }

        let base = "\(repository)/releases/download/v\(version)"
        let manifest = await AikoHttp.bytes("\(base)/\(manifestName)", timeout: listTimeout) ?? Data()
        let signature = await AikoHttp.bytes("\(base)/\(signatureName)", timeout: listTimeout) ?? Data()

        let opened = UpdateGate.open(
            publicKeyPem: UpdateKey.pem, manifestBytes: manifest, signatureDer: signature)
        guard opened.verdict == .ok, let list = opened.manifest else {
            return refuse(opened.verdict, version)
        }

        // The workflow gives the zip its own name and Aiko does not need to know it: one release,
        // one zip.
        guard let zipName = list.onlyFileEndingWith(".zip") else {
            return refuse(.notListed, version)
        }

        Log.write("update: downloading \(zipName) of \(version)")
        guard let zip = await AikoHttp.bytes("\(base)/\(zipName)", timeout: zipTimeout) else {
            Log.write("update: the download did not finish")
            return InstallOutcome(step: .failed, verdict: .ok, version: version)
        }

        let verdict = UpdateGate.checkFile(
            list, fileName: zipName, fileSha256: Array(SHA256.hash(data: zip)))
        guard verdict == .ok else {
            return refuse(verdict, version)
        }

        guard swap(zip: zip, into: bundle) else {
            return InstallOutcome(step: .failed, verdict: .ok, version: version)
        }

        readyVersion = version
        Log.write("update: \(version) is checked and in place, waiting for the restart")
        return InstallOutcome(step: .ready, verdict: .ok, version: version)
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

    // ---- the pieces ----

    private static func refuse(_ verdict: UpdateVerdict, _ version: String) -> InstallOutcome {
        Log.write("update refused: \(UpdateGate.reason(verdict))")
        return InstallOutcome(step: .refused, verdict: verdict, version: version)
    }

    /// A bundle Aiko may rename, in a folder it may write to. An app in /Applications on a Mac
    /// where this user is not an administrator is not one, and neither is a loose binary.
    private static func canReplace(_ bundle: URL) -> Bool {
        bundle.pathExtension == "app"
            && FileManager.default.isWritableFile(atPath: bundle.deletingLastPathComponent().path)
            && FileManager.default.isWritableFile(atPath: bundle.path)
    }

    private static func swap(zip: Data, into bundle: URL) -> Bool {
        let manager = FileManager.default
        let work = manager.temporaryDirectory
            .appendingPathComponent("aiko-update-\(UUID().uuidString)")
        defer { try? manager.removeItem(at: work) }

        do {
            try manager.createDirectory(at: work, withIntermediateDirectories: true)
            let file = work.appendingPathComponent("aiko.zip")
            try zip.write(to: file)

            // ditto, not unzip: it keeps the symlinks and the file modes a signed bundle needs.
            guard run("/usr/bin/ditto", ["-xk", file.path, work.path]) else {
                Log.write("update: the zip would not unpack")
                return false
            }

            let apps = try manager.contentsOfDirectory(at: work, includingPropertiesForKeys: nil)
                .filter { $0.pathExtension == "app" }
            guard apps.count == 1 else {
                Log.write("update: the zip does not hold exactly one app, so nothing was replaced")
                return false
            }

            // Two renames in one folder. A rename is one step for the file system, so there is
            // never a moment with no Aiko at that path.
            let parked = bundle.deletingLastPathComponent().appendingPathComponent(
                "\(bundle.lastPathComponent).old-\(Int(Date().timeIntervalSince1970))")
            try manager.moveItem(at: bundle, to: parked)
            do {
                try manager.moveItem(at: apps[0], to: bundle)
            } catch {
                // The new one would not go in. Put the old one back rather than leave nothing.
                try? manager.moveItem(at: parked, to: bundle)
                throw error
            }

            try? manager.removeItem(at: parked)
            Log.write("update: the bundle was replaced")
            return true
        } catch {
            Log.write("update: could not replace the bundle (\(type(of: error)))")
            return false
        }
    }

    private static func run(_ tool: String, _ arguments: [String]) -> Bool {
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
