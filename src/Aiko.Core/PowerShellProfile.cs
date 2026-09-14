using System.Text.RegularExpressions;

namespace Aiko.Core;

/// A function in a PowerShell profile, by line numbers counted from 1.
public sealed record ProfileFunction(string Name, int FirstLine, int LastLine);

/// Finds the profile functions that switch Claude Code accounts, and turns them off and on.
///
/// A PowerShell function runs before any program in PATH. A function called claude, or claude-work,
/// would quietly take the place of Aiko's commands, and the folder bindings would look broken. The
/// author's own profile shows the shape to expect: a helper sets CLAUDE_CONFIG_DIR, and claude,
/// claude-work and claude-personal only call the helper. So a function counts when its name is
/// claude or starts with claude-, when it sets the variable, or when it calls a function that counts.
///
/// Turning off comments the lines out with a marker, and turning on removes exactly that, so the
/// file comes back byte for byte. A profile this scanner cannot read with confidence is left alone.
public static partial class PowerShellProfile
{
    public const string Prefix = "# aiko-off ";
    public const string BeginMarker = "# aiko-off-begin ";
    public const string EndMarker = "# aiko-off-end";
    public const string EndOfFileMarker = "# aiko-off-end-of-file";
    private const string Note = "Aiko turned this function off. Removing Aiko turns it back on.";

    public static IReadOnlyList<ProfileFunction> FindClaudeSwitchers(string text)
    {
        var lines = SplitLines(text);
        var functions = FindFunctions(lines);
        if (functions is null)
        {
            return [];
        }

        var bodies = functions.ToDictionary(
            f => f,
            f => string.Join('\n', lines.Skip(f.FirstLine - 1).Take(f.LastLine - f.FirstLine + 1)));

        var found = functions
            .Where(f => IsClaudeName(f.Name) || bodies[f].Contains("CLAUDE_CONFIG_DIR", StringComparison.OrdinalIgnoreCase))
            .ToHashSet();

        // A function that calls one already found switches accounts too. Repeat until nothing new.
        bool grew;
        do
        {
            grew = false;
            foreach (var function in functions.Where(f => !found.Contains(f)))
            {
                if (found.Any(f => Calls(bodies[function], f.Name)))
                {
                    found.Add(function);
                    grew = true;
                }
            }
        }
        while (grew);

        return functions.Where(found.Contains).ToList();
    }

    public static string TurnOff(string text, IEnumerable<ProfileFunction> functions)
    {
        var lines = SplitLines(text);
        var chosen = functions.ToList();

        // A function inside another one is already turned off with it.
        var outermost = chosen.Where(f => !chosen.Any(o => o != f && o.FirstLine <= f.FirstLine && o.LastLine >= f.LastLine));

        foreach (var function in outermost.OrderByDescending(f => f.FirstLine))
        {
            var ending = LineEnding(lines[function.FirstLine - 1]);
            for (var i = function.FirstLine - 1; i < function.LastLine; i++)
            {
                lines[i] = Prefix + lines[i];
            }

            // The last line of a file may have no line break. The marker below needs one, and the
            // other end marker tells TurnOn to take that break away again.
            var last = function.LastLine - 1;
            if (!lines[last].EndsWith('\n'))
            {
                lines[last] += ending;
                lines.Insert(function.LastLine, EndOfFileMarker);
            }
            else
            {
                lines.Insert(function.LastLine, EndMarker + ending);
            }
            // ASCII only: the app writes the file back byte for byte, whatever encoding it is in.
            lines.Insert(function.FirstLine - 1, BeginMarker + function.Name + ": " + Note + ending);
        }

        return string.Concat(lines);
    }

    public static string TurnOn(string text)
    {
        var lines = SplitLines(text);
        var result = new List<string>(lines.Count);
        var inside = false;

        foreach (var line in lines)
        {
            if (line.StartsWith(BeginMarker, StringComparison.Ordinal))
            {
                inside = true;
                continue;
            }

            if (inside && line.TrimEnd('\r', '\n') == EndMarker)
            {
                inside = false;
                continue;
            }

            if (inside && line == EndOfFileMarker)
            {
                inside = false;
                if (result.Count > 0)
                {
                    result[^1] = result[^1].TrimEnd('\n').TrimEnd('\r');
                }

                continue;
            }

            result.Add(inside && line.StartsWith(Prefix, StringComparison.Ordinal) ? line[Prefix.Length..] : line);
        }

        return string.Concat(result);
    }

    public static bool HasTurnedOff(string text) => text.Contains(BeginMarker, StringComparison.Ordinal);

    private static bool IsClaudeName(string name) =>
        name.Equals("claude", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("claude-", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("claude_", StringComparison.OrdinalIgnoreCase);

    private static bool Calls(string body, string name) =>
        Regex.IsMatch(body, @"(?<![\w-])" + Regex.Escape(name) + @"(?![\w-])", RegexOptions.IgnoreCase);

    /// Functions at any depth, by a small scanner that knows strings, here-strings and comments,
    /// which is where a brace can hide. Null means the braces did not add up.
    private static List<ProfileFunction>? FindFunctions(List<string> lines)
    {
        var functions = new List<ProfileFunction>();
        var open = new Stack<(string? Name, int Line)>();
        string? pendingName = null;
        var pendingLine = 0;
        var state = ScanState.Code;
        var hereQuote = '\0';

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex].TrimEnd('\r', '\n');
            var i = 0;

            if (state == ScanState.HereString)
            {
                if (line.StartsWith(hereQuote + "@", StringComparison.Ordinal))
                {
                    state = ScanState.Code;
                    i = 2;
                }
                else
                {
                    continue;
                }
            }

            if (state == ScanState.Code && pendingName is null)
            {
                var match = FunctionHead().Match(line[i..]);
                if (match.Success)
                {
                    pendingName = match.Groups["name"].Value;
                    pendingLine = lineIndex + 1;
                    i += match.Index + match.Length;
                }
            }

            for (; i < line.Length; i++)
            {
                var c = line[i];
                switch (state)
                {
                    case ScanState.BlockComment:
                        if (c == '>' && i > 0 && line[i - 1] == '#')
                        {
                            state = ScanState.Code;
                        }

                        break;

                    case ScanState.Single:
                        if (c == '\'')
                        {
                            state = ScanState.Code;
                        }

                        break;

                    case ScanState.Double:
                        if (c == '`')
                        {
                            i++;
                        }
                        else if (c == '"')
                        {
                            state = ScanState.Code;
                        }

                        break;

                    default:
                        if (c == '#')
                        {
                            i = line.Length;
                        }
                        else if (c == '<' && i + 1 < line.Length && line[i + 1] == '#')
                        {
                            state = ScanState.BlockComment;
                            i++;
                        }
                        else if (c == '@' && i + 1 == line.Length - 1 && line[i + 1] is '"' or '\'')
                        {
                            state = ScanState.HereString;
                            hereQuote = line[i + 1];
                            i = line.Length;
                        }
                        else if (c == '\'')
                        {
                            state = ScanState.Single;
                        }
                        else if (c == '"')
                        {
                            state = ScanState.Double;
                        }
                        else if (c == '{')
                        {
                            open.Push((pendingName, pendingName is null ? lineIndex + 1 : pendingLine));
                            pendingName = null;
                        }
                        else if (c == '}')
                        {
                            if (open.Count == 0)
                            {
                                return null;
                            }

                            var (name, first) = open.Pop();
                            if (name is not null)
                            {
                                functions.Add(new ProfileFunction(name, first, lineIndex + 1));
                            }
                        }

                        break;
                }
            }

            // A string that does not close on its line is legal, but this scanner cannot follow it.
            if (state is ScanState.Single or ScanState.Double)
            {
                return null;
            }
        }

        return open.Count == 0 && state == ScanState.Code ? functions.OrderBy(f => f.FirstLine).ToList() : null;
    }

    private static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                lines.Add(text[start..(i + 1)]);
                start = i + 1;
            }
        }

        if (start < text.Length)
        {
            lines.Add(text[start..]);
        }

        return lines;
    }

    private static string LineEnding(string line) =>
        line.EndsWith("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    [GeneratedRegex(@"^\s*(?:function|filter)\s+(?<name>[\w:-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex FunctionHead();

    private enum ScanState
    {
        Code,
        Single,
        Double,
        BlockComment,
        HereString,
    }
}
