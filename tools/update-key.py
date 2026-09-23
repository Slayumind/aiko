"""Copies update-public-key.pem into the macOS core.

Run it from the repository root after the key at the root of the repository changes:

    python tools/update-key.py

The key has one home: update-public-key.pem. Windows builds that file into Aiko.Core as an
embedded resource, so it needs no copy. SwiftPM can only carry a resource that lives inside the
target folder, and a symlink across a checkout made on Windows is worse than a generated file, so
the macOS core gets the text written into UpdateKey.swift instead. UpdateKeyTests fails as soon as
the two drift apart.
"""

import io
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

HEAD = '''// Made by tools/update-key.py from update-public-key.pem. Do not edit by hand: run the script.

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
'''

TAIL = '''
        """
}
'''


def main():
    text = io.open(os.path.join(ROOT, "update-public-key.pem"), encoding="utf-8").read()

    # A Swift multi-line literal keeps the indentation of its closing quotes away from the text,
    # so every line is written with the same eight spaces in front and Swift strips them.
    body = "\n".join("        " + line if line else "" for line in text.split("\n"))
    io.open(
        os.path.join(ROOT, "mac", "Sources", "AikoKit", "UpdateKey.swift"),
        "w", encoding="utf-8", newline="\n").write(HEAD + body + TAIL)
    print("update key: %d bytes" % len(text))


if __name__ == "__main__":
    main()
