import CryptoKit
import Foundation

/// How far the install got. The words the user sees are picked from this in the settings page.
/// The twin of InstallStep in UpdateInstall.cs on Windows.
public enum InstallStep: Sendable, Equatable {
    /// This copy is not a bundle Aiko can replace where it sits: a build folder, or an app in a
    /// place this user may not write to. The download page is the only honest answer.
    case notInstalled

    case nothingNewer

    /// The release could not be reached, or the zip would not unpack.
    case failed

    /// The new bundle is in place. Restarting starts it.
    case ready

    /// The download did not pass the check. Nothing was replaced.
    case refused
}

public struct InstallOutcome: Sendable, Equatable {
    public let step: InstallStep
    public let verdict: UpdateVerdict
    public let version: String?

    public init(step: InstallStep, verdict: UpdateVerdict, version: String?) {
        self.step = step
        self.verdict = verdict
        self.version = version
    }
}

/// Replaces the app bundle of macOS Aiko with a newer one, but only when the release list is
/// signed with our key and the zip matches its line (D-246).
///
/// There is no Velopack on macOS, so this is the way spike S2 proved: the app downloads the zip
/// itself, which is why nothing carries quarantine; `ditto -xk` unpacks it; two renames swap the
/// bundle. Windows does the same checks around Velopack, in UpdateInstall.cs.
///
/// Everything that touches the network or another program comes in as a closure, so the whole
/// path is a test and not a thing that can only be tried by shipping it.
public struct UpdateInstaller {
    public typealias Fetch = @Sendable (_ address: String, _ timeout: TimeInterval) async -> Data?

    public typealias Run = @Sendable (_ tool: String, _ arguments: [String]) -> Bool

    public static let manifestName = "SHA256SUMS.txt"

    public static let signatureName = "SHA256SUMS.txt.sig"

    /// Long enough for the whole app on a slow line.
    public static let zipTimeout: TimeInterval = 600

    public static let listTimeout: TimeInterval = 30

    private let base: String
    private let publicKeyPem: String
    private let bundle: URL
    private let fetch: Fetch
    private let run: Run
    private let log: @Sendable (String) -> Void

    /// `base` is the folder of the release: the three files sit next to each other in it.
    public init(
        base: String,
        publicKeyPem: String,
        bundle: URL,
        fetch: @escaping Fetch,
        run: @escaping Run,
        log: @escaping @Sendable (String) -> Void
    ) {
        self.base = base
        self.publicKeyPem = publicKeyPem
        self.bundle = bundle
        self.fetch = fetch
        self.run = run
        self.log = log
    }

    public func install(version: String) async -> InstallOutcome {
        guard canReplace(bundle) else {
            log("update: this copy cannot be replaced where it sits, so nothing is applied")
            return InstallOutcome(step: .notInstalled, verdict: .ok, version: nil)
        }

        let manifest = await fetch("\(base)/\(Self.manifestName)", Self.listTimeout) ?? Data()
        let signature = await fetch("\(base)/\(Self.signatureName)", Self.listTimeout) ?? Data()

        let opened = UpdateGate.open(
            publicKeyPem: publicKeyPem, manifestBytes: manifest, signatureDer: signature)
        guard opened.verdict == .ok, let list = opened.manifest else {
            return refuse(opened.verdict, version)
        }

        // The workflow gives the zip its own name and Aiko does not need to know it: one release,
        // one zip.
        guard let zipName = list.onlyFileEndingWith(".zip") else {
            return refuse(.notListed, version)
        }

        log("update: downloading \(zipName) of \(version)")
        guard let zip = await fetch("\(base)/\(zipName)", Self.zipTimeout) else {
            log("update: the download did not finish")
            return InstallOutcome(step: .failed, verdict: .ok, version: version)
        }

        let verdict = UpdateGate.checkFile(
            list, fileName: zipName, fileSha256: Array(SHA256.hash(data: zip)))
        guard verdict == .ok else {
            return refuse(verdict, version)
        }

        guard swap(zip: zip) else {
            return InstallOutcome(step: .failed, verdict: .ok, version: version)
        }

        log("update: \(version) is checked and in place, waiting for the restart")
        return InstallOutcome(step: .ready, verdict: .ok, version: version)
    }

    // ---- the pieces ----

    private func refuse(_ verdict: UpdateVerdict, _ version: String) -> InstallOutcome {
        log("update refused: \(UpdateGate.reason(verdict))")
        return InstallOutcome(step: .refused, verdict: verdict, version: version)
    }

    /// A bundle Aiko may rename, in a folder it may write to. An app in /Applications on a Mac
    /// where this user is not an administrator is not one, and neither is a loose binary.
    private func canReplace(_ bundle: URL) -> Bool {
        let manager = FileManager.default
        return bundle.pathExtension == "app"
            && manager.isWritableFile(atPath: bundle.deletingLastPathComponent().path)
            && manager.isWritableFile(atPath: bundle.path)
    }

    private func swap(zip: Data) -> Bool {
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
                log("update: the zip would not unpack")
                return false
            }

            let apps = try manager.contentsOfDirectory(at: work, includingPropertiesForKeys: nil)
                .filter { $0.pathExtension == "app" }
            guard apps.count == 1 else {
                log("update: the zip does not hold exactly one app, so nothing was replaced")
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
            log("update: the bundle was replaced")
            return true
        } catch {
            log("update: could not replace the bundle (\(type(of: error)))")
            return false
        }
    }
}
