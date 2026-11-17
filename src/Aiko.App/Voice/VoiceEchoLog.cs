using Aiko.Core.Terminal;

namespace Aiko.App;

/// What the echo filter of voice input did, in the log of Aiko (AIKO-380). The hint given back in
/// the middle of speech could not be repeated on purpose, so the log keeps the next one in numbers:
/// how many parts were cut and how many of them matched the hint. The words a person said never go
/// there (PRIVACY.md).
static class VoiceEchoLog
{
    public static void Write(string engine, Unechoed result)
    {
        foreach (var cut in result.Dropped)
        {
            Log.Write($"voice input: {engine} gave the hint back, cut {cut.Parts} parts, {cut.InHint} of them in the hint");
        }

        if (result.LookAlikes > 0)
        {
            Log.Write($"voice input: {engine} kept {result.LookAlikes} sentence(s) with three or more items of the hint");
        }
    }
}
