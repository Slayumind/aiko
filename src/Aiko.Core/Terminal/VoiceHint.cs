using System.Text.RegularExpressions;

namespace Aiko.Core.Terminal;

/// A sentence cut out as the hint given back: how many parts it had and how many of them were items
/// of the hint. For the log, which keeps numbers and never the words (PRIVACY.md).
public sealed record EchoCut(int Parts, int InHint);

/// A phrase without the hint given back. LookAlikes: sentences kept although three or more of
/// their parts are items of the hint, so the log can show a near miss.
public sealed record Unechoed(string Text, IReadOnlyList<EchoCut> Dropped, int LookAlikes);

/// What the recogniser is told before it listens (D-313): the folder of the session, its branch,
/// the names of files from the conversation and the keys of Aiko's memory. These are the names a
/// person spells their own way, the words recognition gets wrong first. The facts of the memory
/// never go, only their keys.
public static class VoiceHint
{
    /// Whisper reads about 220 tokens of a prompt; OpenAI reads a similar amount. 400 characters
    /// stays inside both for Russian and English.
    public const int MaxLength = 400;

    /// Words of the terminal a person says about their work, in the language Whisper hears. They
    /// come right after the folder and the branch, so a long list of files never cuts them (AIKO-311).
    private static readonly IReadOnlyDictionary<string, string[]> TerminalWords = new Dictionary<string, string[]>
    {
        ["ru"] = ["Enter", "Esc", "поле ввода", "сессия", "отрисовано"],
        ["en"] = ["Enter", "Esc", "input field", "session", "rendered"],
    };

    /// files are paths or names; only the name is kept. memoryKeys is null when the memory is off.
    /// language is "ru" or "en", or null for no words of the terminal.
    public static string Build(
        string? folder,
        string? branch,
        IEnumerable<string> files,
        IEnumerable<string>? memoryKeys,
        string? language = null,
        int maxLength = MaxLength)
    {
        var items = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? item)
        {
            var text = item?.Trim();
            if (!string.IsNullOrEmpty(text) && seen.Add(text))
            {
                items.Add(text);
            }
        }

        Add(LastPart(folder));
        Add(branch);
        foreach (var word in language is not null && TerminalWords.TryGetValue(language, out var words) ? words : [])
        {
            Add(word);
        }

        foreach (var file in files)
        {
            Add(LastPart(file));
        }

        foreach (var key in memoryKeys ?? [])
        {
            Add(key);
        }

        var hint = "";
        foreach (var item in items)
        {
            var longer = hint.Length == 0 ? item : $"{hint}, {item}";
            if (longer.Length + 1 > maxLength)
            {
                break;
            }

            hint = longer;
        }

        return hint.Length == 0 ? "" : hint + ".";
    }

    /// The language Whisper is told to hear: the one chosen on the Terminal page, and with System
    /// the language of the computer (D-314, D-326). Automatic detection was worse and slower in
    /// the spike.
    public static string Language(AikoLanguage setting, string systemLanguage) => setting switch
    {
        AikoLanguage.Russian => "ru",
        AikoLanguage.English => "en",
        _ => systemLanguage.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en",
    };

    /// Whether a sentence is the hint given back. A recogniser that does not catch the sound may
    /// answer with its prompt: on 2026-10-15 OpenAI wrote the whole hint for an English sentence.
    /// A sentence of three or more parts, every one an item of the hint, is that. So is one of four
    /// or more items and one other part: on 2026-10-23 OpenAI gave the hint back in the middle of
    /// speech five times, with the folder kodland spelled кодland.
    public static bool IsEcho(string phrase, string hint)
    {
        var items = Parts(hint).ToHashSet();
        var parts = Parts(phrase).ToList();
        var inHint = parts.Count(items.Contains);
        return items.Count > 0 && parts.Count >= 3
            && (inHint == parts.Count || (inHint >= 4 && inHint == parts.Count - 1));
    }

    /// The phrase without the sentences that are the hint given back (IsEcho); the speech around
    /// them stays. What was cut and what nearly was is counted for the log.
    public static Unechoed WithoutEcho(string phrase, string hint)
    {
        var items = Parts(hint).ToHashSet();
        var kept = new List<string>();
        var dropped = new List<EchoCut>();
        var lookAlikes = 0;
        foreach (var sentence in SentenceEnd.Split(phrase.Trim()).Where(s => s.Length > 0))
        {
            var parts = Parts(sentence).ToList();
            var inHint = parts.Count(items.Contains);
            if (IsEcho(sentence, hint))
            {
                dropped.Add(new EchoCut(parts.Count, inHint));
                continue;
            }

            if (items.Count > 0 && inHint >= 3)
            {
                lookAlikes++;
            }

            kept.Add(sentence);
        }

        return new Unechoed(string.Join(' ', kept), dropped, lookAlikes);
    }

    /// The space after the end of a sentence. The dot of a file name has no space after it.
    private static readonly Regex SentenceEnd = new(@"(?<=[.!?…])\s+", RegexOptions.Compiled);

    private static IEnumerable<string> Parts(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim().TrimEnd('.').Trim().ToLowerInvariant().Replace('ё', 'е'))
            .Where(part => part.Length > 0);

    /// The files a conversation read or changed, the newest first, each once. The newest go first
    /// because the hint is cut from the end.
    public static IReadOnlyList<string> Files(IEnumerable<FeedItem> items) =>
        [.. items.OfType<FeedItem.Step>()
            .Select(step => step.Line)
            .Where(line => line.Kind is StepKind.Read or StepKind.Edit && !line.Hidden && line.Subject.Length > 0)
            .Select(line => line.Subject)
            .Reverse()
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// The branch a .git/HEAD file names: "ref: refs/heads/feature/voice" is feature/voice. A HEAD
    /// that holds a commit (a detached checkout) names no branch.
    public static string? Branch(string? head)
    {
        const string Prefix = "ref: refs/heads/";
        var line = head?.Trim();
        return line is not null && line.StartsWith(Prefix, StringComparison.Ordinal) && line.Length > Prefix.Length
            ? line[Prefix.Length..]
            : null;
    }

    /// In a worktree .git is a file, "gitdir: <folder>", and HEAD sits in that folder.
    public static string? GitFolder(string? dotGitFile)
    {
        const string Prefix = "gitdir:";
        var line = dotGitFile?.Trim();
        return line is not null && line.StartsWith(Prefix, StringComparison.Ordinal) && line.Length > Prefix.Length
            ? line[Prefix.Length..].Trim()
            : null;
    }

    /// The last part of a path written on either system: C:\work\Aiko\, /Users/me/Aiko.
    private static string? LastPart(string? path) =>
        path?.TrimEnd('\\', '/').Split('\\', '/').LastOrDefault();
}
