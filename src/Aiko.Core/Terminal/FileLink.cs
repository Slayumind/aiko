namespace Aiko.Core.Terminal;

/// A file an answer points at, with the line when it names one: "src/Form.cs:42".
///
/// This only says that the words look like a file. Whether the file is there is asked by the
/// window, which knows the folder of the conversation: "string.Empty" looks like a file too.
public sealed record FileLink(string Path, int? Line)
{
    private const int MaxExtension = 16;
    private const int MaxLineDigits = 9;

    public static FileLink? Parse(string? text)
    {
        var path = (text ?? "").Trim(' ', '\t');
        // A dash first would reach VS Code as an option of its own, not as a file.
        if (path.Length == 0 || path.IndexOfAny(['\n', '\r']) >= 0 || path.StartsWith('-'))
        {
            return null;
        }

        var line = TakeLine(ref path);

        // A colon is the mark of a drive and nothing else: "C:\work". Anywhere further it means
        // an address with a scheme, such as mailto: or https:.
        if (path.IndexOfAny(['<', '>', '"', '|', '?', '*', '`']) >= 0 || path.LastIndexOf(':') > 1 || path.StartsWith(':'))
        {
            return null;
        }

        var name = path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
        var dot = name.LastIndexOf('.');
        if (dot < 0 || name.Length < 2)
        {
            return null;
        }

        // The ending has a letter in it: "3.14" and "v0.4.1" are numbers, not files.
        var extension = name[(dot + 1)..];
        return extension.Length is > 0 and <= MaxExtension
            && extension.All(char.IsAsciiLetterOrDigit)
            && extension.Any(char.IsAsciiLetter)
            ? new FileLink(path, line)
            : null;
    }

    /// Cuts ":42", ":42:7" or "#L42" off the end and gives the line. A column is dropped.
    private static int? TakeLine(ref string path)
    {
        var anchor = path.LastIndexOf("#L", StringComparison.Ordinal);
        if (anchor > 0 && NumberIn(path[(anchor + 2)..]) is { } anchored)
        {
            path = path[..anchor];
            return Positive(anchored);
        }

        if (TakeNumber(ref path) is not { } last)
        {
            return null;
        }
        return Positive(TakeNumber(ref path) ?? last);
    }

    private static int? TakeNumber(ref string path)
    {
        var colon = path.LastIndexOf(':');
        if (colon <= 0 || NumberIn(path[(colon + 1)..]) is not { } number)
        {
            return null;
        }

        path = path[..colon];
        return number;
    }

    private static int? NumberIn(string digits) =>
        digits.Length is > 0 and <= MaxLineDigits && digits.All(char.IsAsciiDigit) ? int.Parse(digits) : null;

    private static int? Positive(int line) => line > 0 ? line : null;
}

/// Where a link of an answer leads.
public abstract record LinkTarget
{
    /// An address for the browser. Only http and https: nothing else is opened from an answer.
    public sealed record Web(string Url) : LinkTarget;

    /// A file for the editor.
    public sealed record File(FileLink Link) : LinkTarget;

    /// Nothing the terminal opens.
    public sealed record None : LinkTarget;

    public static LinkTarget Of(string? href)
    {
        var target = (href ?? "").Trim(' ', '\t');
        if ((target.StartsWith("https://", StringComparison.Ordinal) && target.Length > 8)
            || (target.StartsWith("http://", StringComparison.Ordinal) && target.Length > 7))
        {
            return new Web(target);
        }

        return FileLink.Parse(target) is { } link ? new File(link) : new None();
    }
}
