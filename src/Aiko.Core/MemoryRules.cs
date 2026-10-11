using System.Text.RegularExpressions;

namespace Aiko.Core;

/// The numbers and shapes of the memory: how many facts a category holds, how long a fact may be,
/// what may be a key and when a key is found in a message.
///
/// The limits are sized so the whole memory fits one hook output, which Claude Code cuts at
/// 10,000 characters: 42 facts of 200 characters and their headings (MemoryPrompt.Facts).
public static class MemoryRules
{
    /// Text, meaning and keys of one fact together, counted in UTF-16 units as string.Length does.
    public const int MaxLength = 200;

    public const int MaxKeys = 3;

    /// How many more letters a word may have after a key: «маком» is still «мак», «макрос» is not.
    public const int MaxKeyTail = 2;

    /// A key this long that ends with a Cyrillic letter is compared without that letter (D-280).
    public const int MinStemLength = 5;

    /// How far a word may go on after a key that lost its last letter.
    public const int MaxStemTail = 3;

    public static readonly IReadOnlyList<MemoryCategory> Categories =
        [MemoryCategory.You, MemoryCategory.Work, MemoryCategory.Projects, MemoryCategory.Interests];

    public static int Quota(MemoryCategory category) => category switch
    {
        MemoryCategory.You => 8,
        MemoryCategory.Work => 12,
        MemoryCategory.Projects => 12,
        MemoryCategory.Interests => 10,
        _ => 0,
    };

    /// The heading the model reads. English whatever the interface language, like the persona.
    public static string Title(MemoryCategory category) => category switch
    {
        MemoryCategory.You => "You",
        MemoryCategory.Work => "How to work with me",
        MemoryCategory.Projects => "Projects",
        MemoryCategory.Interests => "Interests and taste",
        _ => "",
    };

    /// The word in the file and in the tool call.
    public static string NameOf(MemoryCategory category) => category switch
    {
        MemoryCategory.You => "you",
        MemoryCategory.Work => "work",
        MemoryCategory.Projects => "projects",
        MemoryCategory.Interests => "interests",
        _ => "",
    };

    public static MemoryCategory? CategoryOf(string? name)
    {
        foreach (var category in Categories)
        {
            if (NameOf(category).Equals(name?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return category;
            }
        }

        return null;
    }

    public static int LengthOf(string text, string meaning, IEnumerable<string> keys) =>
        text.Length + meaning.Length + keys.Sum(key => key.Length);

    public static int LengthOf(MemoryFact fact) => LengthOf(fact.Text, fact.Meaning, fact.Keys);

    public static bool Fits(int length) => length <= MaxLength;

    /// Pinned facts never age, and work habits are rules, not topics: neither needs keys.
    public static bool TakesKeys(MemoryCategory category, bool pinned) =>
        category != MemoryCategory.Work && !pinned;

    /// A key must look like a name or a term: a capital letter first, or a Latin letter, a digit,
    /// #, + or a dot anywhere. A common word as a key would refresh its fact on every other message.
    public static bool IsNameLike(string key) =>
        key.Length > 0 && (char.IsUpper(key[0]) || key.Any(IsTermCharacter));

    /// Keys are trimmed, the ones that are not names are listed apart so the reply can name them,
    /// and the first three good ones are kept.
    public static CleanedKeys CleanKeys(IEnumerable<string?>? keys)
    {
        var given = (keys ?? []).Select(key => key?.Trim() ?? "").Where(key => key.Length > 0).ToList();

        return new CleanedKeys(
            [.. given.Where(IsNameLike).Take(MaxKeys)],
            [.. given.Where(key => !IsNameLike(key))]);
    }

    /// A key is found when it starts a word of the message and the word goes on for at most two
    /// more letters, so a case ending or a short suffix still counts. Letters are any letters,
    /// not only Latin ones, and case does not matter.
    ///
    /// A Russian ending often replaces the last letter of the word instead of following it: the
    /// key "Реликварий" never stands whole in "с реликварием". So a longer key that ends with a
    /// Cyrillic letter is compared without it, and the word may go on one letter further.
    public static bool KeyMatches(string key, string prompt)
    {
        if (key.Length == 0)
        {
            return false;
        }

        var (searched, tail) = LosesLastLetter(key) ? (key[..^1], MaxStemTail) : (key, MaxKeyTail);
        return Regex.IsMatch(
            prompt,
            @"(?<!\w)" + Regex.Escape(searched) + @"\w{0," + tail + @"}(?!\w)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// Latin names do not decline, and without its last letter "Unity" would find "United".
    public static bool LosesLastLetter(string key) => key.Length >= MinStemLength && IsCyrillicLetter(key[^1]);

    /// The fact that goes first when a category is full: the one not seen for the longest time,
    /// and of two seen on the same day the one with the smaller id. Null when every fact is pinned.
    public static MemoryFact? OldestUnpinned(IEnumerable<MemoryFact> facts) =>
        facts.Where(f => !f.Pinned).OrderBy(f => f.Seen).ThenBy(f => f.Number).FirstOrDefault();

    /// The letters of the Cyrillic block. U+0482 to U+0489 in the middle of it are signs, not letters.
    private static bool IsCyrillicLetter(char c) => c is (>= '\u0400' and <= '\u0481') or (>= '\u048A' and <= '\u04FF');

    private static bool IsTermCharacter(char c) => char.IsAsciiLetterOrDigit(c) || c is '#' or '+' or '.';
}

/// Rejected holds the keys that looked like common words.
public sealed record CleanedKeys(IReadOnlyList<string> Kept, IReadOnlyList<string> Rejected)
{
    public static readonly CleanedKeys None = new([], []);
}
