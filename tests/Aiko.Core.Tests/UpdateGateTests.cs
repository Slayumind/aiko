using System.Security.Cryptography;
using System.Text;
using Aiko.Core;

namespace Aiko.Core.Tests;

/// The one rule that lets an update be installed. Every answer but Ok means the file is thrown
/// away and the old version stays.
public class UpdateGateTests
{
    private const string Placeholder = "# No key here yet.\n";

    private static byte[] VectorFile(string name) => SpecCases.Bytes("update-manifest", name);

    private static string VectorKey => SpecCases.Text("update-manifest", "public.pem");

    private static byte[] PayloadHash => SHA256.HashData(VectorFile("payload.bin"));

    [Fact]
    public void A_signed_list_with_the_file_in_it_passes()
    {
        var verdict = UpdateGate.Check(
            VectorKey, VectorFile("SHA256SUMS.txt"), VectorFile("SHA256SUMS.txt.sig"),
            "payload.bin", PayloadHash);

        Assert.Equal(UpdateVerdict.Ok, verdict);
    }

    [Fact]
    public void A_build_with_the_placeholder_key_installs_nothing()
    {
        // Where the project is today. It is asked before anything else, so the log says "no key
        // yet" instead of "bad key", which would read like a bug worth reporting.
        var verdict = UpdateGate.Check(
            Placeholder, VectorFile("SHA256SUMS.txt"), VectorFile("SHA256SUMS.txt.sig"),
            "payload.bin", PayloadHash);

        Assert.Equal(UpdateVerdict.NoKeyYet, verdict);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void A_release_without_both_files_has_no_signed_list(bool noManifest, bool noSignature)
    {
        var verdict = UpdateGate.Check(
            VectorKey,
            noManifest ? [] : VectorFile("SHA256SUMS.txt"),
            noSignature ? [] : VectorFile("SHA256SUMS.txt.sig"),
            "payload.bin",
            PayloadHash);

        Assert.Equal(UpdateVerdict.NoManifest, verdict);
    }

    [Fact]
    public void A_list_changed_after_signing_is_refused()
    {
        var manifest = VectorFile("SHA256SUMS.txt");
        manifest[0] = manifest[0] == (byte)'5' ? (byte)'6' : (byte)'5';

        var verdict = UpdateGate.Check(
            VectorKey, manifest, VectorFile("SHA256SUMS.txt.sig"), "payload.bin", PayloadHash);

        Assert.Equal(UpdateVerdict.BadSignature, verdict);
    }

    [Fact]
    public void A_key_that_is_not_a_p256_public_key_is_refused()
    {
        var pem = "-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----\n";

        var verdict = UpdateGate.Check(
            pem, VectorFile("SHA256SUMS.txt"), VectorFile("SHA256SUMS.txt.sig"),
            "payload.bin", PayloadHash);

        Assert.Equal(UpdateVerdict.BadKey, verdict);
    }

    [Fact]
    public void A_signed_list_in_another_format_is_refused()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var manifest = Encoding.ASCII.GetBytes(
            $"{Convert.ToHexStringLower(PayloadHash)}  payload.bin\r\n");

        var verdict = UpdateGate.Check(
            key.ExportSubjectPublicKeyInfoPem(),
            manifest,
            key.SignData(manifest, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence),
            "payload.bin",
            PayloadHash);

        Assert.Equal(UpdateVerdict.Malformed, verdict);
    }

    [Fact]
    public void A_file_the_list_does_not_name_is_refused()
    {
        var verdict = UpdateGate.Check(
            VectorKey, VectorFile("SHA256SUMS.txt"), VectorFile("SHA256SUMS.txt.sig"),
            "Slayumind.Aiko-win-Setup.exe", PayloadHash);

        Assert.Equal(UpdateVerdict.NotListed, verdict);
    }

    [Fact]
    public void A_file_that_does_not_match_its_line_is_refused()
    {
        var verdict = UpdateGate.Check(
            VectorKey, VectorFile("SHA256SUMS.txt"), VectorFile("SHA256SUMS.txt.sig"),
            "payload.bin", new byte[32]);

        Assert.Equal(UpdateVerdict.HashMismatch, verdict);
    }

    [Fact]
    public void Every_answer_has_a_reason_of_its_own_for_the_log()
    {
        var verdicts = Enum.GetValues<UpdateVerdict>();
        var reasons = verdicts.Select(UpdateGate.Reason).ToList();

        Assert.DoesNotContain(reasons, reason => string.IsNullOrWhiteSpace(reason));
        Assert.Equal(verdicts.Length, reasons.Distinct().Count());
    }

    [Fact]
    public void The_key_this_build_carries_is_the_file_in_the_repository()
    {
        // One source (D-246). The macOS copy is checked the same way in UpdateKeyTests.swift.
        var file = File.ReadAllText(Path.Combine(SpecCases.Repository(), "update-public-key.pem"));

        Assert.Equal(file, UpdateKey.Pem);
    }

    [Theory]
    [InlineData("# a comment only\n", true)]
    [InlineData("", true)]
    [InlineData("-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----\n", false)]
    public void A_file_with_no_pem_block_is_the_placeholder(string pem, bool placeholder)
    {
        Assert.Equal(placeholder, UpdateKey.IsPlaceholder(pem));
    }
}
