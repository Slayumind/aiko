using System.Text;

namespace Aiko.Core.Terminal;

/// The name of a model as the line under the input shows it: "Opus 5.5" for "claude-opus-5-5".
public static class ModelName
{
    private const string Prefix = "claude-";

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
