namespace Aiko.Core.Terminal;

/// A piece of a line in a list that is searched: a match is drawn marked.
public sealed record TextPart(string Text, bool Match);

/// How the lists of the terminal match what the person types (D-346): as one piece of text, with
/// no care for the case of letters and for ё against е.
public static class TextMatch
{
    /// The text cut into parts at every match, or null when nothing matches. An empty query
    /// matches nothing here: the caller decides what an empty query shows.
    public static IReadOnlyList<TextPart>? Parts(string text, string query)
    {
        var wanted = OneLine(query);
        if (wanted.Length == 0)
        {
            return null;
        }

        var parts = new List<TextPart>();
        var from = 0;
        for (var at = IndexOf(text, wanted, 0); at >= 0; at = IndexOf(text, wanted, from))
        {
            if (at > from)
            {
                parts.Add(new TextPart(text[from..at], false));
            }

            parts.Add(new TextPart(text.Substring(at, wanted.Length), true));
            from = at + wanted.Length;
        }

        if (parts.Count == 0)
        {
            return null;
        }

        if (from < text.Length)
        {
            parts.Add(new TextPart(text[from..], false));
        }

        return parts;
    }

    /// Where the query stands in the text, letter for letter after folding, or -1.
    public static int IndexOf(string text, string wanted, int from)
    {
        for (var at = from; at + wanted.Length <= text.Length; at++)
        {
            var same = true;
            for (var i = 0; i < wanted.Length && same; i++)
            {
                same = Fold(text[at + i]) == Fold(wanted[i]);
            }

            if (same)
            {
                return at;
            }
        }

        return -1;
    }

    /// The text on one line: every run of spaces and line breaks is one space.
    public static string OneLine(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static char Fold(char letter)
    {
        var lower = char.ToLowerInvariant(letter);
        return lower == 'ё' ? 'е' : lower;
    }
}

/// A folder the quick pick offers for a new session (D-350): its name with the match marked, and
/// the folder it is in.
public sealed record FolderHit(string Folder, IReadOnlyList<TextPart> Name, string Parent);

/// The quick pick of a folder: the folders the sessions of an environment ran in, the latest used
/// first, closed sessions counted. Other folders, such as those of conversations started outside
/// Aiko, may be added with the time they were used.
public static class FolderPick
{
    /// How many folders the pick shows at most.
    public const int Limit = 20;

    public static IReadOnlyList<FolderHit> Find(
        TerminalSessions sessions, string environment, string query, IEnumerable<(string Folder, long LastUsed)>? others = null)
    {
        var used = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        void Note(string folder, long lastUsed)
        {
            var place = SessionName.FolderPath(folder);
            if (place.Length == 0)
            {
                return;
            }

            if (used.TryGetValue(place, out var known))
            {
                used[place] = Math.Max(known, lastUsed);
                return;
            }

            used[place] = lastUsed;
            order.Add(place);
        }

        foreach (var session in sessions.Sessions.Where(session => session.Environment == environment))
        {
            Note(session.Folder, session.LastUsed);
        }

        foreach (var (folder, lastUsed) in others ?? [])
        {
            Note(folder, lastUsed);
        }

        // The order is stable: of two folders used at one moment, the one seen first stays higher.
        var latest = order.OrderByDescending(folder => used[folder]).ToList();
        var wanted = TextMatch.OneLine(query);
        if (wanted.Length == 0)
        {
            return [.. latest.Take(Limit).Select(folder => Hit(folder, [new TextPart(SessionName.FolderName(folder), false)]))];
        }

        // A match in the name of the folder comes before a match only in the path above it.
        var byName = latest
            .Select(folder => (Folder: folder, Name: TextMatch.Parts(SessionName.FolderName(folder), wanted)))
            .Where(found => found.Name is not null)
            .Select(found => Hit(found.Folder, found.Name!));
        var byPath = latest
            .Where(folder => TextMatch.Parts(SessionName.FolderName(folder), wanted) is null && TextMatch.Parts(folder, wanted) is not null)
            .Select(folder => Hit(folder, [new TextPart(SessionName.FolderName(folder), false)]));

        return [.. byName.Concat(byPath).Take(Limit)];
    }

    private static FolderHit Hit(string folder, IReadOnlyList<TextPart> name) =>
        new(folder, name, SessionName.ParentName(folder));
}
