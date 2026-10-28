namespace Aiko.Core.Terminal;

/// Where "@" stands in the field and what was typed after it.
public sealed record MentionQuery(int From, string Text);

/// The files that "@" offers from the folder of the session (AIKO-330, D-342).
///
/// Paths are relative to the folder and use "/". A file whose own name has the typed letters goes
/// first, then a file whose name starts with them, then one in a shallow folder; a long path costs a
/// little. The scores follow Goose (MentionPopover.tsx; research of 2026-10-26).
public static class FileMention
{
    /// The most lines the menu shows.
    public const int Shown = 10;

    /// The "@" word the caret stands in: "@" at the start or after a space, and no space between
    /// it and the caret. Null when there is none.
    public static MentionQuery? Query(string text, int caret)
    {
        if (caret < 1 || caret > text.Length)
        {
            return null;
        }

        var at = text.LastIndexOf('@', caret - 1);
        if (at < 0 || (at > 0 && !char.IsWhiteSpace(text[at - 1])))
        {
            return null;
        }

        var typed = text[(at + 1)..caret];
        return typed.Any(char.IsWhiteSpace) ? null : new MentionQuery(at, typed);
    }

    /// The best files for what was typed, at most Shown. With nothing typed, the shallow files first.
    public static IReadOnlyList<string> Matches(IEnumerable<string> paths, string query) =>
        [.. paths
            .Select(path => (Path: path, Score: Score(path, query)))
            .Where(item => item.Score is not null)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Path.Length)
            .ThenBy(item => item.Path.ToLowerInvariant(), StringComparer.Ordinal)
            .Take(Shown)
            .Select(item => item.Path)];

    /// The field after a file is chosen: the "@" word becomes "@path " and the caret stands after it.
    public static (string Text, int Caret) Chosen(string text, MentionQuery query, int caret, string path)
    {
        var word = "@" + path + " ";

        // The whole "@" word goes, also the part of it after the caret.
        var end = caret;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        var rest = text[end..];
        if (rest.StartsWith(' '))
        {
            rest = rest[1..];
        }

        return (text[..query.From] + word + rest, query.From + word.Length);
    }

    /// The field after files are dropped or pasted into it: their paths at the caret, a path with
    /// a space in quotes, a space before them after a word and one after them, the caret after it.
    public static (string Text, int Caret) Dropped(string field, int caret, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return (field, caret);
        }

        var before = caret > 0 && !char.IsWhiteSpace(field[caret - 1]) ? " " : "";
        var inserted = before + string.Join(" ", paths.Select(path => path.Contains(' ') ? $"\"{path}\"" : path)) + " ";
        return (field.Insert(caret, inserted), caret + inserted.Length);
    }

    /// The file name part of a path, for the menu.
    public static string Name(string path) => path[(path.LastIndexOf('/') + 1)..];

    private static double? Score(string path, string query)
    {
        var depth = path.Count(c => c == '/');
        var shallow = depth switch
        {
            0 => 50,
            1 => 50,
            2 => 30,
            3 => 15,
            _ => 0,
        };

        if (query.Length == 0)
        {
            return shallow - (0.05 * path.Length);
        }

        if (path.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return null;
        }

        var name = Name(path);
        var score = 20.0 + shallow - (0.05 * path.Length);
        if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
        }

        return score;
    }
}
