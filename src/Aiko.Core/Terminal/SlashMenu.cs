namespace Aiko.Core.Terminal;

/// A command Claude Code knows: a built-in one, a skill or a command of a plugin. Claude Code gives
/// them with descriptions in its answer to "initialize" (spike of 2026-10-16, RESEARCH).
public sealed record SlashCommand(
    string Name,
    string Description,
    string ArgumentHint,
    IReadOnlyList<string> Aliases,
    bool Builtin);

/// One line of the menu: the command and the part of its name that matches what was typed.
/// MatchLength is 0 when nothing was typed after "/", or when only an alias matched.
public sealed record SlashMatch(SlashCommand Command, int MatchStart, int MatchLength);

/// The menu of commands that "/" opens at the start of the field (AIKO-330).
public static class SlashMenu
{
    /// Commands for the screen of Claude Code itself, which do nothing in a conversation of Aiko:
    /// terminal_slash_commands of its init (2.1.301).
    private static readonly HashSet<string> TerminalOnly = ["doctor", "color", "focus", "reload-plugins"];

    /// What was typed after "/", while the caret is still in the first word; null when the menu
    /// is closed: no "/" at the very start, a space before the caret, or the caret before "/".
    public static string? Query(string text, int caret)
    {
        if (!text.StartsWith('/') || caret < 1 || caret > text.Length)
        {
            return null;
        }

        var typed = text[1..caret];
        return typed.Any(char.IsWhiteSpace) ? null : typed;
    }

    /// The commands to show for what was typed, best first. Hidden ones never show. A name that
    /// starts with the query goes first, then a name with a part that starts with it ("copy" in
    /// "aiko:copy"), then an alias, then a name that only has it inside. Built-in commands go
    /// before skills inside each group, and names go in the order of the alphabet.
    public static IReadOnlyList<SlashMatch> Matches(IEnumerable<SlashCommand> commands, string query)
    {
        var shown = commands.Where(command => !IsHidden(command.Name));
        var found = new List<(SlashMatch Match, int Rank)>();
        foreach (var command in shown)
        {
            if (Rank(command, query) is { } rank)
            {
                found.Add((rank.Match, rank.Order));
            }
        }

        return [.. found
            .OrderBy(item => item.Rank)
            .ThenBy(item => item.Match.Command.Builtin ? 0 : 1)
            .ThenBy(item => item.Match.Command.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Match)];
    }

    /// The text the field gets when a command is chosen: the command and a space for its
    /// arguments.
    public static string Chosen(SlashCommand command) => "/" + command.Name + " ";

    private static bool IsHidden(string name) => name.StartsWith("__", StringComparison.Ordinal) || TerminalOnly.Contains(name);

    private static (SlashMatch Match, int Order)? Rank(SlashCommand command, string query)
    {
        var name = command.Name;
        if (query.Length == 0)
        {
            return (new SlashMatch(command, 0, 0), 0);
        }

        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return (new SlashMatch(command, 0, query.Length), 0);
        }

        for (var at = 1; at < name.Length; at++)
        {
            if (name[at - 1] is ':' or '-' or '_'
                && at + query.Length <= name.Length
                && string.Compare(name, at, query, 0, query.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return (new SlashMatch(command, at, query.Length), 1);
            }
        }

        if (command.Aliases.Any(alias => alias.StartsWith(query, StringComparison.OrdinalIgnoreCase)))
        {
            return (new SlashMatch(command, 0, 0), 2);
        }

        var inside = name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        return inside >= 0 ? (new SlashMatch(command, inside, query.Length), 3) : null;
    }
}
