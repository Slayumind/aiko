using System.Reflection;

namespace Aiko.Core;

/// The public half of the key that signs SHA256SUMS.txt of a release.
///
/// One source: `update-public-key.pem` at the root of the repository. Windows builds it into this
/// assembly, and `tools/update-key.py` copies the same text into `mac/Sources/AikoKit/UpdateKey.swift`,
/// where a test fails as soon as the two drift apart.
public static class UpdateKey
{
    private const string ResourceName = "update-public-key.pem";

    private const string PemHeader = "-----BEGIN PUBLIC KEY-----";

    public static string Pem { get; } = Read();

    /// A file with no PEM block in it is no key: Aiko then installs nothing and writes one line saying why.
    public static bool IsPlaceholder(string pem) => !pem.Contains(PemHeader, StringComparison.Ordinal);

    private static string Read()
    {
        using var stream = typeof(UpdateKey).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
