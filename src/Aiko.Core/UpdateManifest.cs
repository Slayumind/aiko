using System.Security.Cryptography;
using System.Text;

namespace Aiko.Core;

public enum ManifestStatus
{
    Ok,

    /// The signature does not match these bytes and this key. The manifest is not read at all.
    BadSignature,

    /// The public key is not a P-256 public key in PEM. A problem in Aiko itself, not in the release.
    BadKey,

    /// The signature is good, but the text is not in the format the release workflow writes.
    Malformed,
}

public enum FileCheck
{
    Ok,
    NotListed,
    HashMismatch,
}

public sealed record ManifestVerification(ManifestStatus Status, UpdateManifest? Manifest);

/// SHA256SUMS.txt of a release: one line per file, `<64 lowercase hex>  <file name>`, LF, ASCII.
///
/// The release workflow signs the raw bytes of the file with an ECDSA P-256 key, and the signature
/// is DER, as `openssl dgst -sha256 -sign` writes it. An update is installed only when the
/// signature is good and the downloaded file matches its line. The macOS app checks the same
/// bytes the same way, with the test vector in spec/cases/update-manifest.
///
/// Bytes in, answers out: the app downloads the files and reads the key.
public sealed class UpdateManifest
{
    private const int HashHexLength = 64;
    private const string Separator = "  ";

    private readonly Dictionary<string, byte[]> _hashes;

    private UpdateManifest(Dictionary<string, byte[]> hashes) => _hashes = hashes;

    public IReadOnlyCollection<string> FileNames => _hashes.Keys;

    /// The signature is checked before a single line is parsed: text nobody signed is not read.
    public static ManifestVerification Verify(
        ReadOnlySpan<byte> manifestBytes, ReadOnlySpan<byte> signatureDer, string publicKeyPem)
    {
        using var key = ImportPublicKey(publicKeyPem);
        if (key is null)
        {
            return new ManifestVerification(ManifestStatus.BadKey, null);
        }

        if (!key.VerifyData(
                manifestBytes, signatureDer, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence))
        {
            return new ManifestVerification(ManifestStatus.BadSignature, null);
        }

        return TryParse(manifestBytes, out var manifest)
            ? new ManifestVerification(ManifestStatus.Ok, manifest)
            : new ManifestVerification(ManifestStatus.Malformed, null);
    }

    /// Strict on purpose. The workflow writes exactly one form, so anything else is not ours:
    /// CRLF, upper case hex, blank lines, a missing last LF, a repeated name or a path in a name.
    public static bool TryParse(ReadOnlySpan<byte> bytes, out UpdateManifest? manifest)
    {
        manifest = null;
        if (bytes.IsEmpty || bytes[^1] != (byte)'\n' || !Ascii.IsValid(bytes))
        {
            return false;
        }

        var text = Encoding.ASCII.GetString(bytes[..^1]);
        var hashes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n'))
        {
            if (!TryParseLine(line, out var name, out var hash) || !hashes.TryAdd(name, hash))
            {
                return false;
            }
        }

        manifest = new UpdateManifest(hashes);
        return true;
    }

    /// The one file of the release whose name ends this way, or null when there is none or more
    /// than one.
    ///
    /// It is how the macOS app finds its zip without knowing what the workflow called it. A
    /// release carries the Windows installer, a disk image and one zip, so ".zip" names that zip.
    /// Two of them mean the release changed shape, and then Aiko installs nothing rather than
    /// guessing which one is the app.
    public string? OnlyFileEndingWith(string suffix)
    {
        string? found = null;
        foreach (var name in _hashes.Keys)
        {
            if (!name.EndsWith(suffix, StringComparison.Ordinal))
            {
                continue;
            }

            if (found is not null)
            {
                return null;
            }

            found = name;
        }

        return found;
    }

    public FileCheck CheckFile(string fileName, ReadOnlySpan<byte> fileBytes) =>
        CheckHash(fileName, SHA256.HashData(fileBytes));

    /// For a file too big to hold in memory: the app hashes the stream and passes the result.
    public FileCheck CheckHash(string fileName, ReadOnlySpan<byte> sha256)
    {
        if (!_hashes.TryGetValue(fileName, out var expected))
        {
            return FileCheck.NotListed;
        }

        return CryptographicOperations.FixedTimeEquals(expected, sha256)
            ? FileCheck.Ok
            : FileCheck.HashMismatch;
    }

    private static bool TryParseLine(string line, out string name, out byte[] hash)
    {
        name = "";
        hash = [];
        if (line.Length <= HashHexLength + Separator.Length
            || !IsLowerHex(line.AsSpan(0, HashHexLength))
            || !line.AsSpan(HashHexLength).StartsWith(Separator, StringComparison.Ordinal))
        {
            return false;
        }

        name = line[(HashHexLength + Separator.Length)..];
        if (!IsPlainFileName(name))
        {
            return false;
        }

        hash = Convert.FromHexString(line.AsSpan(0, HashHexLength));
        return true;
    }

    private static bool IsLowerHex(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (!char.IsAsciiDigit(c) && c is not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }
        return true;
    }

    /// A name, never a path, and nothing that could hide in a log: printable ASCII, no slashes,
    /// no space at either end.
    private static bool IsPlainFileName(string name)
    {
        if (name.Length == 0 || name[0] == ' ' || name[^1] == ' ' || name is "." or "..")
        {
            return false;
        }

        foreach (var c in name)
        {
            if (c is < ' ' or > '~' or '/' or '\\')
            {
                return false;
            }
        }
        return true;
    }

    private static ECDsa? ImportPublicKey(string pem)
    {
        if (!PemEncoding.TryFind(pem, out var fields)
            || !pem.AsSpan()[fields.Label].SequenceEqual("PUBLIC KEY"))
        {
            return null;
        }

        var key = ECDsa.Create();
        try
        {
            var der = Convert.FromBase64String(pem[fields.Base64Data]);
            key.ImportSubjectPublicKeyInfo(der, out _);
            if (IsP256(key))
            {
                return key;
            }
        }
        catch (Exception e) when (e is CryptographicException or FormatException)
        {
        }

        key.Dispose();
        return null;
    }

    /// Windows names the curve by its friendly name and other systems by its OID, so both count.
    private static bool IsP256(ECDsa key)
    {
        var oid = key.ExportParameters(includePrivateParameters: false).Curve.Oid;
        var p256 = ECCurve.NamedCurves.nistP256.Oid;
        return oid.Value == p256.Value || oid.FriendlyName == p256.FriendlyName;
    }
}
