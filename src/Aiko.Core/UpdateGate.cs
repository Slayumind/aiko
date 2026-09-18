namespace Aiko.Core;

/// Why an update was installed or refused. One list for both systems, so a Windows log line and a
/// macOS log line say the same thing about the same problem.
public enum UpdateVerdict
{
    /// The list of files is signed with our key and the downloaded file matches its line.
    Ok,

    /// This build carries the placeholder instead of a key, so it cannot check anything.
    NoKeyYet,

    /// The key in this build is not a P-256 public key. A mistake in Aiko, not in the release.
    BadKey,

    /// The release has no SHA256SUMS.txt, or no signature beside it.
    NoManifest,

    BadSignature,

    Malformed,

    /// The file we downloaded is not named in the list at all.
    NotListed,

    HashMismatch,
}

/// The one rule that decides whether a downloaded update may be installed.
///
/// Both apps call it with the same four things — the key they carry, the two files of the release
/// and the hash of what they downloaded — and both do the same thing with the answer: install it,
/// or throw it away and write the reason down. Nothing installs on a maybe.
public static class UpdateGate
{
    /// The whole answer: the list is read and the one file is looked up in it.
    public static UpdateVerdict Check(
        string publicKeyPem,
        ReadOnlySpan<byte> manifestBytes,
        ReadOnlySpan<byte> signatureDer,
        string fileName,
        ReadOnlySpan<byte> fileSha256)
    {
        var (verdict, manifest) = Open(publicKeyPem, manifestBytes, signatureDer);
        return verdict == UpdateVerdict.Ok ? CheckFile(manifest!, fileName, fileSha256) : verdict;
    }

    /// The first half: is this list of files ours? macOS asks for the list on its own, because the
    /// name of its zip is written in it.
    public static (UpdateVerdict Verdict, UpdateManifest? Manifest) Open(
        string publicKeyPem, ReadOnlySpan<byte> manifestBytes, ReadOnlySpan<byte> signatureDer)
    {
        // Asked first, so that a build with no key says "no key yet" rather than "bad key": one is
        // where the project is today, the other would be a bug worth reporting.
        if (UpdateKey.IsPlaceholder(publicKeyPem))
        {
            return (UpdateVerdict.NoKeyYet, null);
        }

        if (manifestBytes.IsEmpty || signatureDer.IsEmpty)
        {
            return (UpdateVerdict.NoManifest, null);
        }

        var verified = UpdateManifest.Verify(manifestBytes, signatureDer, publicKeyPem);
        return verified.Status switch
        {
            ManifestStatus.BadKey => (UpdateVerdict.BadKey, null),
            ManifestStatus.BadSignature => (UpdateVerdict.BadSignature, null),
            ManifestStatus.Malformed => (UpdateVerdict.Malformed, null),
            _ => (UpdateVerdict.Ok, verified.Manifest),
        };
    }

    /// The second half: is this the file the list names?
    public static UpdateVerdict CheckFile(
        UpdateManifest manifest, string fileName, ReadOnlySpan<byte> fileSha256) =>
        manifest.CheckHash(fileName, fileSha256) switch
        {
            FileCheck.Ok => UpdateVerdict.Ok,
            FileCheck.NotListed => UpdateVerdict.NotListed,
            _ => UpdateVerdict.HashMismatch,
        };

    /// The line for the log. English on both systems: the log is read by whoever looks at a bug
    /// report, and the words the user sees come from the string table instead.
    public static string Reason(UpdateVerdict verdict) => verdict switch
    {
        UpdateVerdict.Ok => "the release list is signed and the file matches it",
        UpdateVerdict.NoKeyYet => "this build has no update key yet",
        UpdateVerdict.BadKey => "the update key in this build is not a P-256 public key",
        UpdateVerdict.NoManifest => "this release has no signed list of files",
        UpdateVerdict.BadSignature => "the signature of the release list is wrong",
        UpdateVerdict.Malformed => "the release list is not in the format Aiko reads",
        UpdateVerdict.NotListed => "the downloaded file is not in the release list",
        _ => "the downloaded file does not match the release list",
    };
}
