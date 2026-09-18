import CryptoKit
import Foundation
import Testing

@testable import AikoKit

/// The whole macOS update path against real files: a real zip made by `ditto`, a real bundle
/// swapped on disk, a real P-256 signature. Only the network is a closure.
///
/// Windows has no twin for this: there Velopack replaces the install folder, and what the two
/// systems share is UpdateGate, which both suites cover.
struct UpdateInstallerTests {
    @Test
    func aSignedReleaseReplacesTheBundle() async throws {
        let world = try World(version: "2.0.0")

        let outcome = await world.installer().install(version: "2.0.0")

        #expect(outcome.step == .ready)
        #expect(outcome.verdict == .ok)
        #expect(try world.installedVersion() == "2.0.0")
        #expect(world.lines.value.contains("update: the bundle was replaced"))
    }

    @Test
    func aManifestChangedAfterSigningLeavesTheOldVersion() async throws {
        let world = try World(version: "2.0.0")
        world.files["\(World.base)/SHA256SUMS.txt"] = Data("0".utf8) + world.manifest.dropFirst()

        let outcome = await world.installer().install(version: "2.0.0")

        #expect(outcome.step == .refused)
        #expect(outcome.verdict == .badSignature)
        #expect(try world.installedVersion() == "1.0.0")
        #expect(world.lines.value.contains(
            "update refused: the signature of the release list is wrong"))
    }

    @Test
    func aZipChangedAfterSigningLeavesTheOldVersion() async throws {
        let world = try World(version: "2.0.0")
        world.files["\(World.base)/Aiko-mac.zip"]?.append(0)

        let outcome = await world.installer().install(version: "2.0.0")

        #expect(outcome.step == .refused)
        #expect(outcome.verdict == .hashMismatch)
        #expect(try world.installedVersion() == "1.0.0")
        #expect(world.lines.value.contains(
            "update refused: the downloaded file does not match the release list"))
    }

    @Test
    func aBuildWithThePlaceholderKeyReplacesNothing() async throws {
        let world = try World(version: "2.0.0")

        let outcome = await world.installer(key: "# no key yet\n").install(version: "2.0.0")

        #expect(outcome.step == .refused)
        #expect(outcome.verdict == .noKeyYet)
        #expect(try world.installedVersion() == "1.0.0")
    }

    @Test
    func aReleaseWithoutASignedListReplacesNothing() async throws {
        let world = try World(version: "2.0.0")
        world.files["\(World.base)/SHA256SUMS.txt.sig"] = nil

        let outcome = await world.installer().install(version: "2.0.0")

        #expect(outcome.step == .refused)
        #expect(outcome.verdict == .noManifest)
        #expect(try world.installedVersion() == "1.0.0")
    }

    @Test
    func aCopyThatIsNotABundleIsNotReplaced() async throws {
        let world = try World(version: "2.0.0")
        let loose = world.folder.appendingPathComponent("aiko-binary")
        try Data("not an app".utf8).write(to: loose)

        let outcome = await world.installer(bundle: loose).install(version: "2.0.0")

        #expect(outcome.step == .notInstalled)
        #expect(try world.installedVersion() == "1.0.0")
    }

    /// A folder with version 1 installed and version 2 zipped and signed, the way a release is.
    final class World: @unchecked Sendable {
        static let base = "https://example.invalid/releases/v2.0.0"

        let folder: URL
        let bundle: URL
        let manifest: Data
        let key: String
        var files: [String: Data] = [:]
        let lines = Lines()

        init(version: String) throws {
            let manager = FileManager.default
            folder = manager.temporaryDirectory
                .appendingPathComponent("aiko-installer-\(UUID().uuidString)")
            try manager.createDirectory(at: folder, withIntermediateDirectories: true)

            let installed = folder.appendingPathComponent("apps")
            try manager.createDirectory(at: installed, withIntermediateDirectories: true)
            bundle = installed.appendingPathComponent("Aiko.app")
            try Self.makeApp(at: bundle, version: "1.0.0")

            // The release: version 2 zipped exactly as the workflow zips it.
            let release = folder.appendingPathComponent("release")
            try manager.createDirectory(at: release, withIntermediateDirectories: true)
            let newApp = release.appendingPathComponent("Aiko.app")
            try Self.makeApp(at: newApp, version: version)

            let zipFile = folder.appendingPathComponent("Aiko-mac.zip")
            #expect(Self.run("/usr/bin/ditto", ["-c", "-k", "--keepParent", newApp.path, zipFile.path]))
            let zip = try Data(contentsOf: zipFile)

            let hash = SHA256.hash(data: zip).map { String(format: "%02x", $0) }.joined()
            manifest = Data("\(hash)  Aiko-mac.zip\n".utf8)

            let signing = P256.Signing.PrivateKey()
            key = signing.publicKey.pemRepresentation

            files["\(Self.base)/SHA256SUMS.txt"] = manifest
            files["\(Self.base)/SHA256SUMS.txt.sig"] = try signing.signature(for: manifest)
                .derRepresentation
            files["\(Self.base)/Aiko-mac.zip"] = zip
        }

        deinit {
            try? FileManager.default.removeItem(at: folder)
        }

        func installer(key: String? = nil, bundle: URL? = nil) -> UpdateInstaller {
            UpdateInstaller(
                base: Self.base,
                publicKeyPem: key ?? self.key,
                bundle: bundle ?? self.bundle,
                fetch: { [files] address, _ in files[address] },
                run: Self.run,
                log: { [lines] line in lines.add(line) })
        }

        func installedVersion() throws -> String {
            let plist = bundle.appendingPathComponent("Contents/Info.plist")
            let text = try String(contentsOf: plist, encoding: .utf8)
            guard let range = text.range(of: "<key>CFBundleShortVersionString</key><string>"),
                  let end = text.range(of: "</string>", range: range.upperBound..<text.endIndex)
            else {
                return ""
            }
            return String(text[range.upperBound..<end.lowerBound])
        }

        /// A bundle with nothing in it but the one line the test reads back.
        static func makeApp(at app: URL, version: String) throws {
            let contents = app.appendingPathComponent("Contents")
            try FileManager.default.createDirectory(
                at: contents.appendingPathComponent("MacOS"), withIntermediateDirectories: true)
            try Data("hello".utf8).write(to: contents.appendingPathComponent("MacOS/Aiko"))
            let plist = """
                <?xml version="1.0" encoding="UTF-8"?>
                <plist version="1.0"><dict>
                <key>CFBundleShortVersionString</key><string>\(version)</string>
                </dict></plist>
                """
            try Data(plist.utf8).write(to: contents.appendingPathComponent("Info.plist"))
        }

        static let run: @Sendable (String, [String]) -> Bool = { tool, arguments in
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

    /// What the installer wrote down, so a test can read the log the owner would read.
    final class Lines: @unchecked Sendable {
        private let lock = NSLock()
        private var written: [String] = []

        var value: [String] {
            lock.lock()
            defer { lock.unlock() }
            return written
        }

        func add(_ line: String) {
            lock.lock()
            defer { lock.unlock() }
            written.append(line)
        }
    }
}
