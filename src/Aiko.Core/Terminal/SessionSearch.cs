namespace Aiko.Core.Terminal;

/// A conversation the palette found (D-347, D-353). The name is cut into parts so the match can be
/// marked. Snippet is the place in the first message that matched, when the name did not. Source
/// is where a conversation outside Aiko was started; null for a session of Aiko.
public sealed record SearchHit(
    string Id,
    string Environment,
    string Folder,
    IReadOnlyList<TextPart> Name,
    IReadOnlyList<TextPart> Snippet,
    long LastUsed,
    ConversationSource? Source);

/// What the palette found, in three groups: sessions in the list, closed ones, and conversations
/// started outside Aiko (D-364).
public sealed record SearchResult(IReadOnlyList<SearchHit> Open, IReadOnlyList<SearchHit> Closed, IReadOnlyList<SearchHit> Outside)
{
    public int Count => Open.Count + Closed.Count + Outside.Count;
}

/// The palette of sessions (D-346, D-347, D-353): by the name and by the first message of the
/// person, in every environment. The conversations are every one Claude Code keeps in the folders
/// of the environments; one with the id of a session of Aiko is that session.
public static class SessionSearch
{
    /// How many closed sessions and outside conversations the palette shows at most, each.
    public const int MoreLimit = 20;

    /// How many of them an empty query shows: the last ones used.
    public const int Recent = 5;

    /// Characters of the message before the match, and in the whole snippet.
    private const int Before = 24;
    private const int SnippetLength = 80;

    /// An empty query gives the open sessions and the last closed and outside ones, the latest
    /// first. A query gives the ones whose name matches first, then the ones where only the first
    /// message does.
    public static SearchResult Find(TerminalSessions sessions, IReadOnlyList<Conversation> conversations, string query)
    {
        var wanted = TextMatch.OneLine(query);
        var known = conversations.GroupBy(conversation => conversation.Id).ToDictionary(group => group.Key, group => group.First());
        var hits = new List<(SearchHit Hit, int Group, bool ByName)>();

        void Consider(string id, string environment, string folder, string name, long lastUsed, ConversationSource? source, int group)
        {
            var message = known.TryGetValue(id, out var conversation) ? TextMatch.OneLine(conversation.FirstMessage) : "";
            if (wanted.Length == 0)
            {
                hits.Add((Hit(), group, true));
            }
            else if (TextMatch.Parts(name, wanted) is { } parts)
            {
                hits.Add((Hit(parts), group, true));
            }
            else if (Snippet(message, wanted) is { } snippet)
            {
                hits.Add((Hit(snippet: snippet), group, false));
            }

            SearchHit Hit(IReadOnlyList<TextPart>? parts = null, IReadOnlyList<TextPart>? snippet = null) =>
                new(id, environment, SessionName.FolderName(folder), parts ?? [new TextPart(name, false)], snippet ?? [], lastUsed, source);
        }

        foreach (var session in sessions.Sessions)
        {
            Consider(session.Id, session.Environment, session.Folder, session.Name, session.LastUsed, null, session.Closed ? 1 : 0);
        }

        foreach (var conversation in known.Values.Where(conversation => sessions.Find(conversation.Id) is null))
        {
            Consider(conversation.Id, conversation.Environment, conversation.Folder, conversation.Name, conversation.LastUsed, conversation.Source, 2);
        }

        // The order is stable: of two used at one moment, the one seen first stays higher.
        IReadOnlyList<SearchHit> Ordered(int group, int limit) => [.. hits
            .Where(hit => hit.Group == group)
            .OrderBy(hit => hit.ByName ? 0 : 1)
            .ThenByDescending(hit => hit.Hit.LastUsed)
            .Select(hit => hit.Hit)
            .Take(limit)];

        var more = wanted.Length == 0 ? Recent : MoreLimit;
        return new SearchResult(Ordered(0, int.MaxValue), Ordered(1, more), Ordered(2, more));
    }

    /// The place of the message around the first match: a few words before it and the rest up to
    /// the length of the snippet, cut on a space, with "…" where the message goes on.
    private static List<TextPart>? Snippet(string message, string wanted)
    {
        var at = TextMatch.IndexOf(message, wanted, 0);
        if (at < 0)
        {
            return null;
        }

        var start = 0;
        if (at > Before)
        {
            var space = message.IndexOf(' ', at - Before);
            start = space >= 0 && space < at ? space + 1 : at - Before;
        }

        var end = message.Length;
        if (end - start > SnippetLength)
        {
            var limit = Math.Max(start + SnippetLength, at + wanted.Length);
            var space = message.LastIndexOf(' ', Math.Min(limit, message.Length - 1));
            end = space > at + wanted.Length ? space : Math.Min(limit, message.Length);
        }

        var parts = TextMatch.Parts(message[start..end], wanted)!.ToList();
        if (start > 0)
        {
            parts.Insert(0, new TextPart("…", false));
        }

        if (end < message.Length)
        {
            parts.Add(new TextPart("…", false));
        }

        return Joined(parts);
    }

    /// Neighbours that are both plain become one part.
    private static List<TextPart> Joined(List<TextPart> parts)
    {
        var joined = new List<TextPart>();
        foreach (var part in parts)
        {
            if (joined.Count > 0 && !joined[^1].Match && !part.Match)
            {
                joined[^1] = new TextPart(joined[^1].Text + part.Text, false);
            }
            else
            {
                joined.Add(part);
            }
        }

        return joined;
    }
}
