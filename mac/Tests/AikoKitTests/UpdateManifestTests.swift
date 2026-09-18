import CryptoKit
import Foundation
import Testing

@testable import AikoKit

/// The twin of UpdateManifestTests.cs. The same files under spec/cases/update-manifest and the
/// same answers, so a release either passes on both systems or on neither.
struct UpdateManifestTests {
    static let payloadHash = "5521dd4a9eeacccdef7dd1c7ea644fb1a1891157d5daed2eca8b6e47c3d8aa63"

    /// Made with `openssl ecparam -name brainpoolP256r1`. The same key size as P-256, so the size
    /// alone proves nothing; the curve has to be read.
    static let brainpoolKey = """
        -----BEGIN PUBLIC KEY-----
        MFowFAYHKoZIzj0CAQYJKyQDAwIIAQEHA0IABHFoCnvQtsAGE4GOSS0xVkSHDO44
        E6KU6BVu2e4reOJWcLf0oi2rLuDTjs/4OB7ZWopVZRGWEVJFyjpiVAT3DK8=
        -----END PUBLIC KEY-----
        """

    /// A P-384 key and a brainpool key of the same size as ours. Both are refused as keys.
    static let keysOnAnotherCurve: [String] = [
        P384.Signing.PrivateKey().publicKey.pemRepresentation,
        brainpoolKey,
    ]

    static let notTheWorkflowFormat: [String] = [
        "",
        "\n",
        payloadHash + "  payload.bin",  // no LF at the end
        payloadHash + "  payload.bin\n\n",  // blank line
        payloadHash + " payload.bin\n",  // one space
        payloadHash + " *payload.bin\n",  // binary mode marker of sha256sum
        payloadHash + "  \n",  // no name
        payloadHash + "   payload.bin\n",  // name starts with a space
        payloadHash + "  dir/payload.bin\n",
        payloadHash + "  dir\\payload.bin\n",
        payloadHash + "  ..\n",
        payloadHash + "  pay\tload.bin\n",
        "5521DD4A9EEACCCDEF7DD1C7EA644FB1A1891157D5DAED2ECA8B6E47C3D8AA63  payload.bin\n",
        "5521dd4a9eeacccdef7dd1c7ea644fb1a1891157d5daed2eca8b6e47c3d8aa6  payload.bin\n",
        "5521dd4a9eeacccdef7dd1c7ea644fb1a1891157d5daed2eca8b6e47c3d8aa633  payload.bin\n",
        "x521dd4a9eeacccdef7dd1c7ea644fb1a1891157d5daed2eca8b6e47c3d8aa63  payload.bin\n",
        payloadHash + "  payload.bin\n" + payloadHash + "  payload.bin\n",  // duplicate
    ]

    // The files openssl made. The Windows tests read the same folder.
    static func vectorFile(_ name: String) -> Data {
        SpecCases.bytes("update-manifest", name)
    }

    static var vectorKey: String { SpecCases.text("update-manifest", "public.pem") }

    @Test
    func theOpensslVectorVerifiesAndItsPayloadMatches() {
        let result = UpdateManifest.verify(
            Self.vectorFile("SHA256SUMS.txt"), Self.vectorFile("SHA256SUMS.txt.sig"), Self.vectorKey)

        #expect(result.status == .ok)
        #expect(result.manifest?.fileNames == ["payload.bin"])
        #expect(result.manifest?.checkFile("payload.bin", Self.vectorFile("payload.bin")) == .ok)
    }

    @Test
    func aManifestChangedByOneByteHasABadSignature() {
        var manifest = Self.vectorFile("SHA256SUMS.txt")
        manifest[manifest.startIndex] = manifest[manifest.startIndex] == UInt8(ascii: "5")
            ? UInt8(ascii: "6") : UInt8(ascii: "5")

        let result = UpdateManifest.verify(
            manifest, Self.vectorFile("SHA256SUMS.txt.sig"), Self.vectorKey)

        #expect(result.status == .badSignature)
        #expect(result.manifest == nil)
    }

    @Test
    func aSignatureFromAnotherKeyIsBad() {
        let other = P256.Signing.PrivateKey()
        let manifest = Self.vectorFile("SHA256SUMS.txt")
        let signature = try! other.signature(for: manifest).derRepresentation

        let result = UpdateManifest.verify(manifest, signature, Self.vectorKey)

        #expect(result.status == .badSignature)
    }

    @Test
    func aTruncatedSignatureIsBad() {
        let signature = Self.vectorFile("SHA256SUMS.txt.sig").dropLast()

        let result = UpdateManifest.verify(
            Self.vectorFile("SHA256SUMS.txt"), Data(signature), Self.vectorKey)

        #expect(result.status == .badSignature)
    }

    @Test
    func anEmptySignatureIsBad() {
        let result = UpdateManifest.verify(Self.vectorFile("SHA256SUMS.txt"), Data(), Self.vectorKey)

        #expect(result.status == .badSignature)
    }

    @Test
    func aSignatureInTheRawFormatInsteadOfDerIsBad() {
        // CryptoKit and .NET both have a raw r||s form. The release uses DER, and only DER counts.
        let key = P256.Signing.PrivateKey()
        let manifest = Data("\(Self.payloadHash)  payload.bin\n".utf8)
        let raw = try! key.signature(for: manifest).rawRepresentation

        let result = UpdateManifest.verify(manifest, raw, key.publicKey.pemRepresentation)

        #expect(result.status == .badSignature)
    }

    @Test
    func aChangedPayloadDoesNotMatchItsLine() {
        let manifest = Self.verified(Self.vectorFile("SHA256SUMS.txt"))
        var payload = Self.vectorFile("payload.bin")
        payload.append(0)

        #expect(manifest?.checkFile("payload.bin", payload) == .hashMismatch)
    }

    @Test
    func aFileTheManifestDoesNotListIsNotListed() {
        let manifest = Self.verified(Self.vectorFile("SHA256SUMS.txt"))

        #expect(manifest?.checkFile("Payload.bin", Self.vectorFile("payload.bin")) == .notListed)
        #expect(manifest?.checkFile("other.bin", Self.vectorFile("payload.bin")) == .notListed)
    }

    @Test
    func aPrecomputedHashIsCheckedLikeTheFile() {
        let manifest = Self.verified(Self.vectorFile("SHA256SUMS.txt"))

        #expect(manifest?.checkHash("payload.bin", Self.bytes(Self.payloadHash)) == .ok)
        #expect(manifest?.checkHash("payload.bin", [UInt8](repeating: 0, count: 32)) == .hashMismatch)
        #expect(manifest?.checkHash("payload.bin", [UInt8](repeating: 0, count: 31)) == .hashMismatch)
    }

    @Test
    func aManifestWithSeveralFilesVerifiesWithAKeyMadeInTheTest() {
        let key = P256.Signing.PrivateKey()
        let setupHash = SHA256.hash(data: Data("setup".utf8)).map { String(format: "%02x", $0) }.joined()
        let manifest = Data(
            "\(setupHash)  Slayumind.Aiko-win-Setup.exe\n\(Self.payloadHash)  releases.win.json\n".utf8)
        let signature = try! key.signature(for: manifest).derRepresentation

        let result = UpdateManifest.verify(manifest, signature, key.publicKey.pemRepresentation)

        #expect(result.status == .ok)
        #expect(result.manifest?.checkFile("Slayumind.Aiko-win-Setup.exe", Data("setup".utf8)) == .ok)
        #expect(result.manifest?.checkFile("releases.win.json", Data("setup".utf8)) == .hashMismatch)
    }

    @Test
    func aCrlfManifestIsRejectedEvenWhenSigned() {
        // The workflow writes LF. A CR would end up in every file name, so CRLF is not accepted.
        let key = P256.Signing.PrivateKey()
        let manifest = Data("\(Self.payloadHash)  payload.bin\r\n".utf8)
        let signature = try! key.signature(for: manifest).derRepresentation

        let result = UpdateManifest.verify(manifest, signature, key.publicKey.pemRepresentation)

        #expect(result.status == .malformed)
        #expect(result.manifest == nil)
    }

    @Test(arguments: UpdateManifestTests.notTheWorkflowFormat)
    func anythingButTheWorkflowFormatIsMalformed(text: String) {
        #expect(UpdateManifest.parse(Data(text.utf8)) == nil)
    }

    @Test
    func aNameOutsideAsciiIsMalformed() {
        #expect(UpdateManifest.parse(Data("\(Self.payloadHash)  paylöad.bin\n".utf8)) == nil)
    }

    @Test
    func namesThatDifferOnlyInCaseAreTwoFiles() {
        let manifest = UpdateManifest.parse(
            Data("\(Self.payloadHash)  a.bin\n\(Self.payloadHash)  A.bin\n".utf8))

        #expect(manifest?.fileNames.count == 2)
    }

    @Test
    func theOneFileWithAnEndingIsFound() {
        // How the macOS app finds its zip without knowing the name the workflow gave it.
        let manifest = Self.verified(Self.vectorFile("SHA256SUMS.txt"))

        #expect(manifest?.onlyFileEndingWith(".bin") == "payload.bin")
        #expect(manifest?.onlyFileEndingWith("payload.bin") == "payload.bin")
        #expect(manifest?.onlyFileEndingWith(".zip") == nil)
        #expect(manifest?.onlyFileEndingWith(".BIN") == nil)
    }

    @Test
    func twoFilesWithTheSameEndingAreNoAnswer() {
        // A release that changed shape. Guessing which zip is the app is how the wrong one gets
        // installed, so Aiko answers nothing at all.
        let manifest = UpdateManifest.parse(
            Data("\(Self.payloadHash)  Aiko-mac.zip\n\(Self.payloadHash)  Aiko-mac-debug.zip\n".utf8))

        #expect(manifest?.onlyFileEndingWith(".zip") == nil)
        #expect(manifest?.onlyFileEndingWith("debug.zip") == "Aiko-mac-debug.zip")
    }

    @Test
    func aKeyThatIsNotPemIsABadKey() {
        let result = UpdateManifest.verify(
            Self.vectorFile("SHA256SUMS.txt"), Self.vectorFile("SHA256SUMS.txt.sig"), "not a key")

        #expect(result.status == .badKey)
    }

    @Test
    func aPrivateKeyIsABadKey() {
        // The app must carry only the public half. A private key in its place is a mistake to catch.
        let key = P256.Signing.PrivateKey()
        let manifest = Self.vectorFile("SHA256SUMS.txt")
        let signature = try! key.signature(for: manifest).derRepresentation

        let result = UpdateManifest.verify(manifest, signature, key.pemRepresentation)

        #expect(result.status == .badKey)
    }

    @Test(arguments: UpdateManifestTests.keysOnAnotherCurve)
    func aKeyOnAnotherCurveIsABadKey(pem: String) {
        let result = UpdateManifest.verify(
            Self.vectorFile("SHA256SUMS.txt"), Self.vectorFile("SHA256SUMS.txt.sig"), pem)

        #expect(result.status == .badKey)
    }

    @Test
    func aPublicKeyWithBrokenBase64IsABadKey() {
        let pem = "-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----\n"

        let result = UpdateManifest.verify(
            Self.vectorFile("SHA256SUMS.txt"), Self.vectorFile("SHA256SUMS.txt.sig"), pem)

        #expect(result.status == .badKey)
    }

    @Test
    func aCommentAboveTheKeyIsAllowed() {
        let pem = "# Replace this with the real key.\n" + Self.vectorKey

        let result = UpdateManifest.verify(
            Self.vectorFile("SHA256SUMS.txt"), Self.vectorFile("SHA256SUMS.txt.sig"), pem)

        #expect(result.status == .ok)
    }

    // ---- helpers ----

    static func verified(_ manifest: Data) -> UpdateManifest? {
        let result = UpdateManifest.verify(manifest, vectorFile("SHA256SUMS.txt.sig"), vectorKey)
        #expect(result.status == .ok)
        return result.manifest
    }

    static func bytes(_ hex: String) -> [UInt8] {
        var out: [UInt8] = []
        var index = hex.startIndex
        while index < hex.endIndex {
            let next = hex.index(index, offsetBy: 2)
            out.append(UInt8(hex[index..<next], radix: 16)!)
            index = next
        }
        return out
    }
}
