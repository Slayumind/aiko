import CryptoKit
import Foundation

public enum ManifestStatus: Sendable, Equatable {
    case ok

    /// The signature does not match these bytes and this key. The manifest is not read at all.
    case badSignature

    /// The public key is not a P-256 public key in PEM. A problem in Aiko itself, not in the release.
    case badKey

    /// The signature is good, but the text is not in the format the release workflow writes.
    case malformed
}

public enum FileCheck: Sendable, Equatable {
    case ok
    case notListed
    case hashMismatch
}

public struct ManifestVerification: Sendable {
    public let status: ManifestStatus
    public let manifest: UpdateManifest?
}

/// SHA256SUMS.txt of a release: one line per file, `<64 lowercase hex>  <file name>`, LF, ASCII.
///
/// The twin of UpdateManifest.cs, down to the test vector in spec/cases/update-manifest, which
/// both suites read. The release workflow signs the raw bytes of the file with an ECDSA P-256 key,
/// and the signature is DER, as `openssl dgst -sha256 -sign` writes it.
///
/// Bytes in, answers out: the app downloads the files and reads the key.
public struct UpdateManifest: Sendable {
    private static let hashHexLength = 64

    private let hashes: [String: [UInt8]]

    private init(hashes: [String: [UInt8]]) {
        self.hashes = hashes
    }

    public var fileNames: Set<String> { Set(hashes.keys) }

    /// The signature is checked before a single line is parsed: text nobody signed is not read.
    public static func verify(
        _ manifestBytes: Data, _ signatureDer: Data, _ publicKeyPem: String
    ) -> ManifestVerification {
        guard let key = importPublicKey(publicKeyPem) else {
            return ManifestVerification(status: .badKey, manifest: nil)
        }

        guard let signature = try? P256.Signing.ECDSASignature(derRepresentation: signatureDer),
              key.isValidSignature(signature, for: manifestBytes)
        else {
            return ManifestVerification(status: .badSignature, manifest: nil)
        }

        guard let manifest = parse(manifestBytes) else {
            return ManifestVerification(status: .malformed, manifest: nil)
        }

        return ManifestVerification(status: .ok, manifest: manifest)
    }

    /// Strict on purpose. The workflow writes exactly one form, so anything else is not ours:
    /// CRLF, upper case hex, blank lines, a missing last LF, a repeated name or a path in a name.
    public static func parse(_ bytes: Data) -> UpdateManifest? {
        guard bytes.last == UInt8(ascii: "\n"), bytes.allSatisfy({ $0 < 0x80 }) else {
            return nil
        }

        let text = String(decoding: bytes.dropLast(), as: UTF8.self)
        var hashes: [String: [UInt8]] = [:]
        for line in text.split(separator: "\n", omittingEmptySubsequences: false) {
            guard let parsed = parseLine(String(line)), hashes[parsed.name] == nil else {
                return nil
            }
            hashes[parsed.name] = parsed.hash
        }

        return UpdateManifest(hashes: hashes)
    }

    /// The one file of the release whose name ends this way, or nil when there is none or more
    /// than one.
    ///
    /// It is how the macOS app finds its zip without knowing what the workflow called it. A
    /// release carries the Windows installer, a disk image and one zip, so ".zip" names that zip.
    /// Two of them mean the release changed shape, and then Aiko installs nothing rather than
    /// guessing which one is the app.
    public func onlyFileEndingWith(_ suffix: String) -> String? {
        var found: String?
        for name in hashes.keys where name.hasSuffix(suffix) {
            if found != nil { return nil }
            found = name
        }
        return found
    }

    public func checkFile(_ fileName: String, _ fileBytes: Data) -> FileCheck {
        checkHash(fileName, Array(SHA256.hash(data: fileBytes)))
    }

    /// For a file too big to hold in memory: the app hashes the stream and passes the result.
    public func checkHash(_ fileName: String, _ sha256: [UInt8]) -> FileCheck {
        guard let expected = hashes[fileName] else {
            return .notListed
        }

        return Self.sameBytes(expected, sha256) ? .ok : .hashMismatch
    }

    // ---- reading the text ----

    private static func parseLine(_ line: String) -> (name: String, hash: [UInt8])? {
        // Every byte was checked as ASCII, so one scalar is one character here.
        let scalars = Array(line.unicodeScalars)
        guard scalars.count > hashHexLength + 2,
              scalars[hashHexLength] == " ",
              scalars[hashHexLength + 1] == " ",
              let hash = hexBytes(scalars[0..<hashHexLength])
        else {
            return nil
        }

        let name = String(String.UnicodeScalarView(scalars[(hashHexLength + 2)...]))
        guard isPlainFileName(name) else {
            return nil
        }

        return (name, hash)
    }

    private static func hexBytes(_ scalars: ArraySlice<Unicode.Scalar>) -> [UInt8]? {
        var bytes: [UInt8] = []
        var high: UInt8?
        for scalar in scalars {
            guard let value = lowerHexValue(scalar) else { return nil }
            if let started = high {
                bytes.append(started << 4 | value)
                high = nil
            } else {
                high = value
            }
        }
        return bytes
    }

    private static func lowerHexValue(_ scalar: Unicode.Scalar) -> UInt8? {
        switch scalar {
        case "0"..."9": return UInt8(scalar.value - Unicode.Scalar("0").value)
        case "a"..."f": return UInt8(scalar.value - Unicode.Scalar("a").value) + 10
        default: return nil
        }
    }

    /// A name, never a path, and nothing that could hide in a log: printable ASCII, no slashes,
    /// no space at either end.
    private static func isPlainFileName(_ name: String) -> Bool {
        guard let first = name.unicodeScalars.first, let last = name.unicodeScalars.last,
              first != " ", last != " ", name != ".", name != ".."
        else {
            return false
        }

        return name.unicodeScalars.allSatisfy { scalar in
            scalar >= " " && scalar <= "~" && scalar != "/" && scalar != Unicode.Scalar(92)
        }
    }

    /// The same time whatever the bytes are, as CryptographicOperations.FixedTimeEquals is on
    /// Windows.
    private static func sameBytes(_ left: [UInt8], _ right: [UInt8]) -> Bool {
        guard left.count == right.count else { return false }
        var difference: UInt8 = 0
        for index in left.indices {
            difference |= left[index] ^ right[index]
        }
        return difference == 0
    }

    // ---- reading the key ----

    private static func importPublicKey(_ pem: String) -> P256.Signing.PublicKey? {
        guard let block = pemBlock(pem) else { return nil }
        return try? P256.Signing.PublicKey(pemRepresentation: block)
    }

    /// Takes the PEM block out of the file, so a comment above the key is allowed on both systems.
    /// CryptoKit wants the document on its own, while .NET finds the block inside any text.
    private static func pemBlock(_ pem: String) -> String? {
        let begin = "-----BEGIN PUBLIC KEY-----"
        let end = "-----END PUBLIC KEY-----"
        guard let start = pem.range(of: begin),
              let stop = pem.range(of: end, range: start.upperBound..<pem.endIndex)
        else {
            return nil
        }

        return String(pem[start.lowerBound..<stop.upperBound]) + "\n"
    }
}
