using System.Text;

namespace Aiko.Core.Terminal;

/// Gives the voice of the personality its colour in the terminal (D-457). The personality writes its
/// opening and closing lines in italics, and Claude Code sends italics as a style of the terminal.
/// This turns that style into a colour on the way to the screen, so Claude Code still draws
/// everything. Output comes in pieces, and a piece can end in the middle of a sequence: such an end
/// waits for the next piece.
public sealed class VoiceTint(byte red, byte green, byte blue)
{
    private const char Escape = '\u001b';

    /// Longer than any style sequence: what grows past it is not one, and goes out as it is.
    private const int MostHeld = 128;

    private readonly string _colour = $"38;2;{red};{green};{blue}";
    private string _held = "";
    private bool _voice;

    public string Apply(string piece)
    {
        var text = _held + piece;
        _held = "";
        var result = new StringBuilder(text.Length + 16);
        var at = 0;
        while (at < text.Length)
        {
            if (text[at] != Escape)
            {
                result.Append(text[at++]);
                continue;
            }

            var end = SequenceEnd(text, at);
            if (end is null)
            {
                if (text.Length - at <= MostHeld)
                {
                    _held = text[at..];
                    return result.ToString();
                }

                result.Append(text[at++]);
                continue;
            }

            var length = end.Value - at + 1;
            result.Append(text[end.Value] == 'm' && text[at + 1] == '[' ? Style(text.Substring(at + 2, length - 3), text.Substring(at, length)) : text.Substring(at, length));
            at += length;
        }

        return result.ToString();
    }

    /// The last character of the sequence at this place; null when it has not all come yet. Only
    /// CSI sequences are followed. After any other escape the next character ends it.
    private static int? SequenceEnd(string text, int at)
    {
        if (at + 1 >= text.Length)
        {
            return null;
        }

        if (text[at + 1] != '[')
        {
            return at + 1;
        }

        for (var i = at + 2; i < text.Length; i++)
        {
            if (text[i] is >= '@' and <= '~')
            {
                return i;
            }
        }

        return null;
    }

    private string Style(string parameters, string original)
    {
        if (parameters.Any(c => c is not (>= '0' and <= '9' or ';')))
        {
            return original;
        }

        var codes = parameters.Split(';');
        var written = new List<string>(codes.Length);
        for (var i = 0; i < codes.Length; i++)
        {
            var code = codes[i].TrimStart('0');
            switch (code)
            {
                case "":
                    _voice = false;
                    written.Add(codes[i]);
                    break;
                case "3":
                    _voice = true;
                    written.Add(_colour);
                    break;
                case "23" when _voice:
                    _voice = false;
                    written.Add("39");
                    break;
                case "39" when _voice:
                    written.Add(_colour);
                    break;
                case "38" or "48" or "58":
                    // A colour carries its own numbers: none of them is a style.
                    var extra = i + 1 < codes.Length ? codes[i + 1] switch { "5" => 2, "2" => 4, _ => 0 } : 0;
                    var last = Math.Min(codes.Length - 1, i + extra);
                    written.AddRange(codes[i..(last + 1)]);
                    i = last;
                    break;
                default:
                    written.Add(codes[i]);
                    break;
            }
        }

        return $"{Escape}[{string.Join(';', written)}m";
    }
}
