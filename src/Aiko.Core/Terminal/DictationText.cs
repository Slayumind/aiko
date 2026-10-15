using System.Text;
using System.Text.RegularExpressions;

namespace Aiko.Core.Terminal;

/// The text of one recording while it is spoken. Each pass of the recogniser reads all the sound
/// so far; the words two passes in a row agree on are confirmed and never change again, the rest
/// is the grey tail the next pass may still correct (D-315, spike in RESEARCH).
public sealed partial record DictationText
{
    public static readonly DictationText Empty = new([], [], []);

    private DictationText(IReadOnlyList<string> confirmed, IReadOnlyList<string> tail, IReadOnlyList<string> lastPass)
    {
        Confirmed = confirmed;
        Tail = tail;
        LastPass = lastPass;
    }

    public IReadOnlyList<string> Confirmed { get; }

    public IReadOnlyList<string> Tail { get; }

    private IReadOnlyList<string> LastPass { get; }

    public string ConfirmedText => string.Join(' ', Confirmed);

    public string TailText => string.Join(' ', Tail);

    public string Text => string.Join(' ', Confirmed.Concat(Tail));

    /// The text of a new pass over all the sound so far.
    public DictationText Pass(string text)
    {
        var words = Words(text);
        var covered = Covered(words);
        var agreed = Agreed(LastPass, words);
        var confirmed = agreed > covered ? [.. Confirmed, .. words.Take(agreed).Skip(covered)] : Confirmed;
        return new DictationText(confirmed, [.. words.Skip(Math.Max(agreed, covered))], words);
    }

    /// The last pass, after the mic is closed. Every word is confirmed; the words confirmed
    /// earlier stay as they were shown.
    public DictationText Final(string text)
    {
        var words = Words(text);
        return new DictationText([.. Confirmed, .. words.Skip(Covered(words))], [], words);
    }

    /// How many words of a new pass the confirmed words already cover. Counted by letters, not by
    /// words: a later pass may split "пригони.net" into "пригони .NET", or say "Делы" for "Делай".
    private int Covered(IReadOnlyList<string> words)
    {
        var target = Bare(ConfirmedText).Length;
        var letters = 0;
        var count = 0;
        while (count < words.Count && letters + Bare(words[count]).Length <= target)
        {
            letters += Bare(words[count]).Length;
            count++;
        }

        return count;
    }

    /// The words of a pass. Whisper writes sounds it heard in brackets, "[Музыка]" or "(смех)";
    /// those are not words anybody said.
    public static IReadOnlyList<string> Words(string? text) =>
        Bracketed().Replace(text ?? "", " ")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static int Agreed(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var count = 0;
        while (count < a.Count && count < b.Count && Same(a[count], b[count]))
        {
            count++;
        }

        return count;
    }

    /// Two passes may differ in case and punctuation around a word; that is still one word.
    private static bool Same(string a, string b) => Bare(a) == Bare(b) && Bare(a).Length > 0;

    private static string Bare(string word)
    {
        var bare = new StringBuilder(word.Length);
        foreach (var c in word.ToLowerInvariant().Replace('ё', 'е'))
        {
            if (char.IsLetterOrDigit(c))
            {
                bare.Append(c);
            }
        }

        return bare.ToString();
    }

    [GeneratedRegex(@"\[[^\]]*\]|\([^)]*\)")]
    private static partial Regex Bracketed();
}
