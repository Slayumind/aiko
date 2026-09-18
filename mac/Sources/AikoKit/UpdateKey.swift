// Made by tools/update-key.py from update-public-key.pem. Do not edit by hand: run the script.

import Foundation

/// The public half of the key that signs SHA256SUMS.txt of a release.
///
/// One source: `update-public-key.pem` at the root of the repository. Windows builds that file
/// into Aiko.Core, and this copy is written from it by the script above; UpdateKeyTests fails if
/// the two ever say different things.
///
/// Until the owner makes the key pair, the file holds comment lines only. Aiko then installs
/// nothing at all and writes one line saying why, which is what it did before auto-update existed.
public enum UpdateKey {
    private static let pemHeader = "-----BEGIN PUBLIC KEY-----"

    /// A file with no PEM block in it is the placeholder that ships today.
    public static func isPlaceholder(_ pem: String) -> Bool {
        !pem.contains(pemHeader)
    }

    public static let pem = """
        # Placeholder. The public key that checks update signatures goes here.
        # The owner makes an ECDSA P-256 key pair, keeps the private key only in the GitHub secret
        # AIKO_UPDATE_SIGNING_KEY and replaces this text with the PEM public key (BEGIN PUBLIC KEY).
        # While this placeholder is here, release.yml does not require or check a signature.

        """
}
