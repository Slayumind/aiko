using System.Security.Cryptography;
using System.Text;
using Aiko.Core;

namespace Aiko.Core.Tests;

public class UpdateManifestTests
{
    private const string PayloadHash = "5521dd4a9eeacccdef7dd1c7ea644fb1a1891157d5daed2eca8b6e47c3d8aa63";

    // The files openssl made. The Swift port reads the same folder, so both apps agree on the format.
    private static byte[] VectorFile(string name) => SpecCases.Bytes("update-manifest", name);

    private static string VectorKey => SpecCases.Text("update-manifest", "public.pem");

    [Fact]
    public void The_openssl_vector_verifies_and_its_payload_matches()
    {
        var result = UpdateManifest.Verify(
            VectorFile("SHA256SUMS.txt"), VectorFile("SHA256SUMS.txt.sig"), VectorKey);

        Assert.Equal(ManifestStatus.Ok, result.Status);
        Assert.Equal(["payload.bin"], result.Manifest!.FileNames);
        Assert.Equal(FileCheck.Ok, result.Manifest.CheckFile("payload.bin", VectorFile("payload.bin")));
    }

    [Fact]
    public void A_manifest_changed_by_one_byte_has_a_bad_signature()
    {
        var manifest = VectorFile("SHA256SUMS.txt");
        manifest[0] = manifest[0] == (byte)'5' ? (byte)'6' : (byte)'5';

        var result = UpdateManifest.Verify(manifest, VectorFile("SHA256SUMS.txt.sig"), VectorKey);

        Assert.Equal(ManifestStatus.BadSignature, result.Status);
        Assert.Null(result.Manifest);
    }

    [Fact]
    public void A_signature_from_another_key_is_bad()
    {
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var manifest = VectorFile("SHA256SUMS.txt");

        var result = UpdateManifest.Verify(manifest, Sign(manifest, otherKey), VectorKey);

        Assert.Equal(ManifestStatus.BadSignature, result.Status);
    }

    [Fact]
    public void A_truncated_signature_is_bad()
    {
        var signature = VectorFile("SHA256SUMS.txt.sig");

        var result = UpdateManifest.Verify(VectorFile("SHA256SUMS.txt"), signature[..^1], VectorKey);

        Assert.Equal(ManifestStatus.BadSignature, result.Status);
    }

    [Fact]
    public void An_empty_signature_is_bad()
    {
        var result = UpdateManifest.Verify(VectorFile("SHA256SUMS.txt"), [], VectorKey);

        Assert.Equal(ManifestStatus.BadSignature, result.Status);
    }

    [Fact]
    public void A_signature_in_the_raw_format_instead_of_der_is_bad()
    {
        // CryptoKit and .NET both have a raw r||s form. The release uses DER, and only DER counts.
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var manifest = Encoding.ASCII.GetBytes($"{PayloadHash}  payload.bin\n");
        var raw = key.SignData(
            manifest, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        var result = UpdateManifest.Verify(manifest, raw, PublicPem(key));

        Assert.Equal(ManifestStatus.BadSignature, result.Status);
    }

    [Fact]
    public void A_changed_payload_does_not_match_its_line()
    {
        var manifest = Verified(VectorFile("SHA256SUMS.txt"));
        var payload = VectorFile("payload.bin").Append((byte)0).ToArray();

        Assert.Equal(FileCheck.HashMismatch, manifest.CheckFile("payload.bin", payload));
    }

    [Fact]
    public void A_file_the_manifest_does_not_list_is_not_listed()
    {
        var manifest = Verified(VectorFile("SHA256SUMS.txt"));

        Assert.Equal(FileCheck.NotListed, manifest.CheckFile("Payload.bin", VectorFile("payload.bin")));
        Assert.Equal(FileCheck.NotListed, manifest.CheckFile("other.bin", VectorFile("payload.bin")));
    }

    [Fact]
    public void A_precomputed_hash_is_checked_like_the_file()
    {
        var manifest = Verified(VectorFile("SHA256SUMS.txt"));

        Assert.Equal(FileCheck.Ok, manifest.CheckHash("payload.bin", Convert.FromHexString(PayloadHash)));
        Assert.Equal(FileCheck.HashMismatch, manifest.CheckHash("payload.bin", new byte[32]));
        Assert.Equal(FileCheck.HashMismatch, manifest.CheckHash("payload.bin", new byte[31]));
    }

    [Fact]
    public void A_manifest_with_several_files_verifies_with_a_key_made_in_the_test()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var setupHash = Hex(SHA256.HashData("setup"u8));
        var manifest = Encoding.ASCII.GetBytes(
            $"{setupHash}  Slayumind.Aiko-win-Setup.exe\n{PayloadHash}  releases.win.json\n");

        var result = UpdateManifest.Verify(manifest, Sign(manifest, key), PublicPem(key));

        Assert.Equal(ManifestStatus.Ok, result.Status);
        Assert.Equal(FileCheck.Ok, result.Manifest!.CheckFile("Slayumind.Aiko-win-Setup.exe", "setup"u8));
        Assert.Equal(FileCheck.HashMismatch, result.Manifest.CheckFile("releases.win.json", "setup"u8));
    }

    [Fact]
    public void A_crlf_manifest_is_rejected_even_when_signed()
    {
        // The workflow writes LF. A CR would end up in every file name, so CRLF is not accepted.
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var manifest = Encoding.ASCII.GetBytes($"{PayloadHash}  payload.bin\r\n");

        var result = UpdateManifest.Verify(manifest, Sign(manifest, key), PublicPem(key));

        Assert.Equal(ManifestStatus.Malformed, result.Status);
        Assert.Null(result.Manifest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData(PayloadHash + "  payload.bin")] // no LF at the end
    [InlineData(PayloadHash + "  payload.bin\n\n")] // blank line
    [InlineData(PayloadHash + " payload.bin\n")] // one space
    [InlineData(PayloadHash + " *payload.bin\n")] // binary mode marker of sha256sum
    [InlineData(PayloadHash + "  \n")] // no name
    [InlineData(PayloadHash + "   payload.bin\n")] // name starts with a space
    [InlineData(PayloadHash + "  dir/payload.bin\n")]
    [InlineData(PayloadHash + "  dir\\payload.bin\n")]
    [InlineData(PayloadHash + "  ..\n")]
    [InlineData(PayloadHash + "  pay\tload.bin\n")]
    [InlineData("5521DD4A9EEACCCDEF7DD1C7EA644FB1A1891157D5DAED2ECA8B6E47C3D8AA63  payload.bin\n")]
    [InlineData("5521dd4a9eeacccdef7dd1c7ea644fb1a1891157d5daed2eca8b6e47c3d8aa6  payload.bin\n")]
    [InlineData("5521dd4a9eeacccdef7dd1c7ea644fb1a1891157d5daed2eca8b6e47c3d8aa633  payload.bin\n")]
    [InlineData("x521dd4a9eeacccdef7dd1c7ea644fb1a1891157d5daed2eca8b6e47c3d8aa63  payload.bin\n")]
    [InlineData(PayloadHash + "  payload.bin\n" + PayloadHash + "  payload.bin\n")] // duplicate
    public void Anything_but_the_workflow_format_is_malformed(string text)
    {
        Assert.False(UpdateManifest.TryParse(Encoding.UTF8.GetBytes(text), out var manifest));
        Assert.Null(manifest);
    }

    [Fact]
    public void A_name_outside_ascii_is_malformed()
    {
        var bytes = Encoding.UTF8.GetBytes($"{PayloadHash}  paylöad.bin\n");

        Assert.False(UpdateManifest.TryParse(bytes, out _));
    }

    [Fact]
    public void Names_that_differ_only_in_case_are_two_files()
    {
        var bytes = Encoding.ASCII.GetBytes($"{PayloadHash}  a.bin\n{PayloadHash}  A.bin\n");

        Assert.True(UpdateManifest.TryParse(bytes, out var manifest));
        Assert.Equal(2, manifest!.FileNames.Count);
    }

    [Fact]
    public void A_key_that_is_not_pem_is_a_bad_key()
    {
        var result = UpdateManifest.Verify(
            VectorFile("SHA256SUMS.txt"), VectorFile("SHA256SUMS.txt.sig"), "not a key");

        Assert.Equal(ManifestStatus.BadKey, result.Status);
    }

    [Fact]
    public void A_private_key_is_a_bad_key()
    {
        // The app must carry only the public half. A private key in its place is a mistake to catch.
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var manifest = VectorFile("SHA256SUMS.txt");

        var result = UpdateManifest.Verify(manifest, Sign(manifest, key), key.ExportPkcs8PrivateKeyPem());

        Assert.Equal(ManifestStatus.BadKey, result.Status);
    }

    [Theory]
    [InlineData("nistP384")]
    [InlineData("brainpoolP256r1")] // the same size as P-256, so the size alone proves nothing
    public void A_key_on_another_curve_is_a_bad_key(string curve)
    {
        using var key = ECDsa.Create(ECCurve.CreateFromFriendlyName(curve));
        var manifest = VectorFile("SHA256SUMS.txt");

        var result = UpdateManifest.Verify(manifest, Sign(manifest, key), PublicPem(key));

        Assert.Equal(ManifestStatus.BadKey, result.Status);
    }

    [Fact]
    public void A_public_key_with_broken_base64_is_a_bad_key()
    {
        var pem = "-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----\n";

        var result = UpdateManifest.Verify(
            VectorFile("SHA256SUMS.txt"), VectorFile("SHA256SUMS.txt.sig"), pem);

        Assert.Equal(ManifestStatus.BadKey, result.Status);
    }

    [Fact]
    public void A_comment_above_the_key_is_allowed()
    {
        var pem = "# Replace this with the real key.\n" + VectorKey;

        var result = UpdateManifest.Verify(
            VectorFile("SHA256SUMS.txt"), VectorFile("SHA256SUMS.txt.sig"), pem);

        Assert.Equal(ManifestStatus.Ok, result.Status);
    }

    [Fact]
    public void The_one_file_with_an_ending_is_found()
    {
        // How the macOS app finds its zip without knowing the name the workflow gave it.
        var manifest = Verified(VectorFile("SHA256SUMS.txt"));

        Assert.Equal("payload.bin", manifest.OnlyFileEndingWith(".bin"));
        Assert.Equal("payload.bin", manifest.OnlyFileEndingWith("payload.bin"));
        Assert.Null(manifest.OnlyFileEndingWith(".zip"));
        Assert.Null(manifest.OnlyFileEndingWith(".BIN"));
    }

    [Fact]
    public void Two_files_with_the_same_ending_are_no_answer()
    {
        // A release that changed shape. Guessing which zip is the app is how the wrong one gets
        // installed, so Aiko answers nothing at all.
        var text = $"{PayloadHash}  Aiko-mac.zip\n{PayloadHash}  Aiko-mac-debug.zip\n";

        Assert.True(UpdateManifest.TryParse(Encoding.ASCII.GetBytes(text), out var manifest));
        Assert.Null(manifest!.OnlyFileEndingWith(".zip"));
        Assert.Equal("Aiko-mac-debug.zip", manifest.OnlyFileEndingWith("debug.zip"));
    }

    private static UpdateManifest Verified(byte[] manifest)
    {
        var result = UpdateManifest.Verify(manifest, VectorFile("SHA256SUMS.txt.sig"), VectorKey);
        Assert.Equal(ManifestStatus.Ok, result.Status);
        return result.Manifest!;
    }

    private static byte[] Sign(byte[] data, ECDsa key) =>
        key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

    private static string PublicPem(ECDsa key) => key.ExportSubjectPublicKeyInfoPem();

    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);
}
