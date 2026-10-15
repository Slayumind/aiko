namespace Aiko.Core.Terminal;

/// What the recogniser is told before it listens (D-313): the folder of the session, its branch,
/// the names of files from the conversation and the keys of Aiko's memory. These are the names a
/// person spells their own way, the words recognition gets wrong first. The facts of the memory
/// never go, only their keys.
public static class VoiceHint
{
    /// Whisper reads about 220 tokens of a prompt; OpenAI reads a similar amount. 400 characters
    /// stays inside both for Russian and English.
    public const int MaxLength = 400;

    /// files are paths or names; only the name is kept. memoryKeys is null when the memory is off.
    public static string Build(
        string? folder,
        string? branch,
        IEnumerable<string> files,
        IEnumerable<string>? memoryKeys,
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

    /// The language the recogniser is told to hear: the language of Aiko's interface (D-314).
    /// Automatic detection was worse and slower in the spike.
    public static string Language(AikoLanguage setting, string systemLanguage) => setting switch
    {
        AikoLanguage.Russian => "ru",
        AikoLanguage.English => "en",
        _ => systemLanguage.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en",
    };

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
