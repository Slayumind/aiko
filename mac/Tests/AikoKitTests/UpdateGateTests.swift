import CryptoKit
import Foundation
import Testing

@testable import AikoKit

/// The one rule that lets an update be installed, in Swift. The twin of UpdateGateTests.cs.
struct UpdateGateTests {
    static let placeholder = "# No key here yet.\n"

    static let missingFiles: [(Bool, Bool)] = [(true, false), (false, true), (true, true)]

    static let placeholderCases: [(String, Bool)] = [
        ("# a comment only\n", true),
        ("", true),
        ("-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----\n", false),
    ]

    static func vectorFile(_ name: String) -> Data { SpecCases.bytes("update-manifest", name) }

    static var vectorKey: String { SpecCases.text("update-manifest", "public.pem") }

    static var payloadHash: [UInt8] { Array(SHA256.hash(data: vectorFile("payload.bin"))) }

    @Test
    func aSignedListWithTheFileInItPasses() {
        let verdict = UpdateGate.check(
            publicKeyPem: Self.vectorKey,
            manifestBytes: Self.vectorFile("SHA256SUMS.txt"),
            signatureDer: Self.vectorFile("SHA256SUMS.txt.sig"),
            fileName: "payload.bin",
            fileSha256: Self.payloadHash)

        #expect(verdict == .ok)
    }

    @Test
    func aBuildWithThePlaceholderKeyInstallsNothing() {
        // Where the project is today. It is asked before anything else, so the log says "no key
        // yet" instead of "bad key", which would read like a bug worth reporting.
        let verdict = UpdateGate.check(
            publicKeyPem: Self.placeholder,
            manifestBytes: Self.vectorFile("SHA256SUMS.txt"),
            signatureDer: Self.vectorFile("SHA256SUMS.txt.sig"),
            fileName: "payload.bin",
            fileSha256: Self.payloadHash)

        #expect(verdict == .noKeyYet)
    }

    @Test(arguments: UpdateGateTests.missingFiles)
    func aReleaseWithoutBothFilesHasNoSignedList(noManifest: Bool, noSignature: Bool) {
        let verdict = UpdateGate.check(
            publicKeyPem: Self.vectorKey,
            manifestBytes: noManifest ? Data() : Self.vectorFile("SHA256SUMS.txt"),
            signatureDer: noSignature ? Data() : Self.vectorFile("SHA256SUMS.txt.sig"),
            fileName: "payload.bin",
            fileSha256: Self.payloadHash)

        #expect(verdict == .noManifest)
    }

    @Test
    func aListChangedAfterSigningIsRefused() {
        var manifest = Self.vectorFile("SHA256SUMS.txt")
        manifest[manifest.startIndex] = manifest[manifest.startIndex] == UInt8(ascii: "5")
            ? UInt8(ascii: "6") : UInt8(ascii: "5")

        let verdict = UpdateGate.check(
            publicKeyPem: Self.vectorKey,
            manifestBytes: manifest,
            signatureDer: Self.vectorFile("SHA256SUMS.txt.sig"),
            fileName: "payload.bin",
            fileSha256: Self.payloadHash)

        #expect(verdict == .badSignature)
    }

    @Test
    func aKeyThatIsNotAP256PublicKeyIsRefused() {
        let verdict = UpdateGate.check(
            publicKeyPem: "-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----\n",
            manifestBytes: Self.vectorFile("SHA256SUMS.txt"),
            signatureDer: Self.vectorFile("SHA256SUMS.txt.sig"),
            fileName: "payload.bin",
            fileSha256: Self.payloadHash)

        #expect(verdict == .badKey)
    }

    @Test
    func aSignedListInAnotherFormatIsRefused() {
        let key = P256.Signing.PrivateKey()
        let hex = Self.payloadHash.map { String(format: "%02x", $0) }.joined()
        let manifest = Data("\(hex)  payload.bin\r\n".utf8)

        let verdict = UpdateGate.check(
            publicKeyPem: key.publicKey.pemRepresentation,
            manifestBytes: manifest,
            signatureDer: try! key.signature(for: manifest).derRepresentation,
            fileName: "payload.bin",
            fileSha256: Self.payloadHash)

        #expect(verdict == .malformed)
    }

    @Test
    func aFileTheListDoesNotNameIsRefused() {
        let verdict = UpdateGate.check(
            publicKeyPem: Self.vectorKey,
            manifestBytes: Self.vectorFile("SHA256SUMS.txt"),
            signatureDer: Self.vectorFile("SHA256SUMS.txt.sig"),
            fileName: "Slayumind.Aiko-win-Setup.exe",
            fileSha256: Self.payloadHash)

        #expect(verdict == .notListed)
    }

    @Test
    func aFileThatDoesNotMatchItsLineIsRefused() {
        let verdict = UpdateGate.check(
            publicKeyPem: Self.vectorKey,
            manifestBytes: Self.vectorFile("SHA256SUMS.txt"),
            signatureDer: Self.vectorFile("SHA256SUMS.txt.sig"),
            fileName: "payload.bin",
            fileSha256: [UInt8](repeating: 0, count: 32))

        #expect(verdict == .hashMismatch)
    }

    @Test
    func everyAnswerHasAReasonOfItsOwnForTheLog() {
        let verdicts: [UpdateVerdict] = [
            .ok, .noKeyYet, .badKey, .noManifest, .badSignature, .malformed, .notListed, .hashMismatch,
        ]
        let reasons = verdicts.map(UpdateGate.reason)

        #expect(reasons.allSatisfy { !$0.isEmpty })
        #expect(Set(reasons).count == verdicts.count)
    }

    @Test
    func theKeyThisBuildCarriesIsTheFileInTheRepository() {
        // One source (D-246). Run tools/update-key.py when the file at the root changes.
        let path = SpecCases.repository().appendingPathComponent("update-public-key.pem")
        let file = (try? String(contentsOf: path, encoding: .utf8)) ?? ""

        #expect(file == UpdateKey.pem)
    }

    @Test(arguments: UpdateGateTests.placeholderCases)
    func aFileWithNoPemBlockIsThePlaceholder(pem: String, placeholder: Bool) {
        #expect(UpdateKey.isPlaceholder(pem) == placeholder)
    }
}
