using System.Text;

namespace Aiko.Core.Terminal;

/// The model in the line under the input. Waiting is set while Claude Code has not named the
/// full id yet: the line shows the family with "…" in a quiet colour.
public readonly record struct ModelLabel(string Text, bool Waiting);

/// The name of a model as the line under the input shows it: "Opus 5.5" for "claude-opus-5-5".
public static class ModelName
{
    private const string Prefix = "claude-";
    private const string Switched = "<local-command-stdout>Set model to ";
    private const string NotYet = " …";

    /// Asked is the model sent to Claude Code that it has not confirmed. Known is the last model
    /// Claude Code named, or the one kept for the session. A short name like "sonnet" has no
    /// version, so the line waits for Claude Code to name the full id.
    public static ModelLabel Label(string? asked, string? known)
    {
        var askedText = asked?.Trim() ?? "";
        if (askedText.Length > 0)
        {
            return new(Display(askedText) + NotYet, true);
        }

        var knownText = known?.Trim() ?? "";
        if (knownText.Length == 0)
        {
            return new("", false);
        }

        return knownText.StartsWith(Prefix, StringComparison.Ordinal)
            ? new(Display(knownText), false)
            : new(Display(knownText) + NotYet, true);
    }

    /// The full id from what Claude Code writes after a change of model:
    /// "&lt;local-command-stdout&gt;Set model to `sonnet (claude-sonnet-5-5)`". Null for any other text.
    public static string? FromSwitch(string? text)
    {
        var at = text?.IndexOf(Switched, StringComparison.Ordinal) ?? -1;
        if (text is null || at < 0)
        {
            return null;
        }

        var open = text.IndexOf('(', at);
        var close = open < 0 ? -1 : text.IndexOf(')', open);
        if (close < 0)
        {
            return null;
        }

        var id = text[(open + 1)..close].Trim();
        return id.Length > Prefix.Length && id.StartsWith(Prefix, StringComparison.Ordinal) ? id : null;
    }

    /// An id that does not look like a Claude model comes back as it is.
    public static string Display(string? id)
    {
        var text = id?.Trim() ?? "";
        if (text.Length == 0)
        {
            return "";
        }

        var words = new List<string>();
        var numbers = new List<string>();
        var body = text.StartsWith(Prefix, StringComparison.Ordinal) ? text[Prefix.Length..] : text;

        foreach (var part in body.Split('-'))
        {
            if (IsDate(part))
            {
                continue;
            }
            if (IsShortNumber(part))
            {
                numbers.Add(part);
            }
            else if (IsWord(part))
            {
                words.Add(char.ToUpperInvariant(part[0]) + part[1..]);
            }
            else
            {
                return text;
            }
        }

        if (words.Count == 0)
        {
            return text;
        }

        var name = new StringBuilder(string.Join(' ', words));
        if (numbers.Count > 0)
        {
            name.Append(' ').Append(string.Join('.', numbers));
        }
        return name.ToString();
    }

    /// The day a model was released, the tail of older ids: 20251001.
    private static bool IsDate(string part) => part.Length == 8 && part.All(char.IsAsciiDigit);

    private static bool IsShortNumber(string part) => part.Length is > 0 and <= 2 && part.All(char.IsAsciiDigit);

    private static bool IsWord(string part) => part.Length > 0 && part.All(char.IsAsciiLetterLower);
}
