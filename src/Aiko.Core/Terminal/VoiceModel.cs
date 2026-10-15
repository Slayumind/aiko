namespace Aiko.Core.Terminal;

/// The speech model of voice input and the person's own OpenAI key: where the model comes from,
/// when each engine is ready, and how a key is shown (D-313, D-314, D-316).
public static class VoiceModel
{
    /// Our own release on GitHub: its hosts are already allowed, and the file is the one of
    /// whisper.cpp on Hugging Face, checked by the hash below (D-316).
    public const string Url = "https://github.com/Slayumind/aiko/releases/download/voice-model-1/ggml-small.bin";

    public const long Bytes = 487_601_967;

    public const string Sha256 = "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b";

    /// The OpenAI model of D-314.
    public const string CloudModel = "gpt-4o-transcribe";

    /// Whole megabytes, as the settings page and the line above the field write them: 465.
    public static long Megabytes(long bytes) => Math.Max(0, bytes) / (1024 * 1024);

    /// modelBytes is the size of the model file on disk, null when there is none. A file of
    /// another size is a download that stopped half way.
    public static VoiceReadiness Readiness(VoiceEngine engine, long? modelBytes, bool keyStored) => engine switch
    {
        VoiceEngine.OpenAI => keyStored ? VoiceReadiness.Ready : VoiceReadiness.NeedsKey,
        _ => modelBytes == Bytes ? VoiceReadiness.Ready : VoiceReadiness.NeedsModel,
    };

    /// An OpenAI key as people paste it: "sk-" and then letters, digits, "-" and "_". Spaces
    /// around it are trimmed; null when it is not a key at all.
    public static string? CleanKey(string? pasted)
    {
        var key = pasted?.Trim();
        if (key is null || key.Length < 20 || key.Length > 400 || !key.StartsWith("sk-", StringComparison.Ordinal))
        {
            return null;
        }

        return key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? key : null;
    }

    /// A stored key is never shown whole: its kind and its last four characters, enough to tell
    /// two keys apart. "sk-proj-…" keeps "sk-proj-", any other keeps "sk-".
    public static string MaskKey(string key)
    {
        var start = key.StartsWith("sk-proj-", StringComparison.Ordinal) ? "sk-proj-" : "sk-";
        return key.Length < start.Length + 8 ? start + Dots : start + Dots + key[^4..];
    }

    private const string Dots = "••••••••••";
}
