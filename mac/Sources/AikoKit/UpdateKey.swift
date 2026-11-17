// Made by tools/update-key.py from update-public-key.pem. Do not edit by hand: run the script.

import Foundation

/// The public half of the key that signs SHA256SUMS.txt of a release.
///
/// One source: `update-public-key.pem` at the root of the repository. Windows builds that file
/// into Aiko.Core, and this copy is written from it by the script above; UpdateKeyTests fails if
/// the two ever say different things.
public enum UpdateKey {
    private static let pemHeader = "-----BEGIN PUBLIC KEY-----"

    /// A file with no PEM block in it is no key: Aiko then installs nothing and writes one line saying why.
    public static func isPlaceholder(_ pem: String) -> Bool {
        !pem.contains(pemHeader)
    }

    public static let pem = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE32/wy6nUcqaEQUPy/pHlmocCr0JR
        OsmGXSZ+1eM2ZmflImp/8vLjaOjmPpvkllzrsw40H9OYAcNl4rsuyKlVjg==
        -----END PUBLIC KEY-----

        """
}
