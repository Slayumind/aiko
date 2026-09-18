import Foundation

/// Why an update was installed or refused. One list for both systems, so a Windows log line and a
/// macOS log line say the same thing about the same problem. The twin of UpdateGate.cs.
public enum UpdateVerdict: Sendable, Equatable {
    /// The list of files is signed with our key and the downloaded file matches its line.
    case ok

    /// This build carries the placeholder instead of a key, so it cannot check anything.
    case noKeyYet

    /// The key in this build is not a P-256 public key. A mistake in Aiko, not in the release.
    case badKey

    /// The release has no SHA256SUMS.txt, or no signature beside it.
    case noManifest

    case badSignature

    case malformed

    /// The file we downloaded is not named in the list at all.
    case notListed

    case hashMismatch
}

/// The one rule that decides whether a downloaded update may be installed.
///
/// Both apps call it with the same four things: the key they carry, the two files of the release
/// and the hash of what they downloaded. Both do the same thing with the answer: install it, or
/// throw it away and write the reason down. Nothing installs on a maybe.
public enum UpdateGate {
    /// The whole answer: the list is read and the one file is looked up in it.
    public static func check(
        publicKeyPem: String,
        manifestBytes: Data,
        signatureDer: Data,
        fileName: String,
        fileSha256: [UInt8]
    ) -> UpdateVerdict {
        let opened = open(
            publicKeyPem: publicKeyPem, manifestBytes: manifestBytes, signatureDer: signatureDer)
        guard opened.verdict == .ok, let manifest = opened.manifest else {
            return opened.verdict
        }

        return checkFile(manifest, fileName: fileName, fileSha256: fileSha256)
    }

    /// The first half: is this list of files ours? macOS asks for the list on its own, because the
    /// name of its zip is written in it.
    public static func open(
        publicKeyPem: String, manifestBytes: Data, signatureDer: Data
    ) -> (verdict: UpdateVerdict, manifest: UpdateManifest?) {
        // Asked first, so that a build with no key says "no key yet" rather than "bad key": one is
        // where the project is today, the other would be a bug worth reporting.
        if UpdateKey.isPlaceholder(publicKeyPem) {
            return (.noKeyYet, nil)
        }

        if manifestBytes.isEmpty || signatureDer.isEmpty {
            return (.noManifest, nil)
        }

        let verified = UpdateManifest.verify(manifestBytes, signatureDer, publicKeyPem)
        switch verified.status {
        case .badKey: return (.badKey, nil)
        case .badSignature: return (.badSignature, nil)
        case .malformed: return (.malformed, nil)
        case .ok: return (.ok, verified.manifest)
        }
    }

    /// The second half: is this the file the list names?
    public static func checkFile(
        _ manifest: UpdateManifest, fileName: String, fileSha256: [UInt8]
    ) -> UpdateVerdict {
        switch manifest.checkHash(fileName, fileSha256) {
        case .ok: return .ok
        case .notListed: return .notListed
        case .hashMismatch: return .hashMismatch
        }
    }

    /// The line for the log. English on both systems: the log is read by whoever looks at a bug
    /// report, and the words the user sees come from the string table instead.
    public static func reason(_ verdict: UpdateVerdict) -> String {
        switch verdict {
        case .ok: return "the release list is signed and the file matches it"
        case .noKeyYet: return "this build has no update key yet"
        case .badKey: return "the update key in this build is not a P-256 public key"
        case .noManifest: return "this release has no signed list of files"
        case .badSignature: return "the signature of the release list is wrong"
        case .malformed: return "the release list is not in the format Aiko reads"
        case .notListed: return "the downloaded file is not in the release list"
        case .hashMismatch: return "the downloaded file does not match the release list"
        }
    }
}
