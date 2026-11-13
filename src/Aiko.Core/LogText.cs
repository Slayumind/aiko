using System.Text.RegularExpressions;

namespace Aiko.Core;

/// A line for the log of Aiko without full paths (D-420). PRIVACY.md promises that the log has no
/// working folders, and the message of a system error often names the file it failed on:
/// "Could not find a part of the path 'C:\Users\me\work\a.txt'". A path keeps only its last part, so
/// the line still says which file. Rows: spec/cases/log-paths.
public static partial class LogText
{
    public static string WithoutPaths(string line)
    {
        var quoted = Quoted().Replace(line, match => match.Groups["q"].Value + LastPart(match.Groups["path"].Value) + match.Groups["q"].Value);
        return Bare().Replace(quoted, match => LastPart(match.Value));
    }

    private static string LastPart(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        var cut = trimmed.LastIndexOfAny(['\\', '/']);
        return cut < 0 ? trimmed : trimmed[(cut + 1)..];
    }

    /// A path in quotes may have spaces in it: 'C:\Users\me\personal projects\a.txt'.
    [GeneratedRegex("""(?<q>['"])(?<path>(?:[A-Za-z]:[\\/]|\\\\|/(?:Users|home|private|var|tmp|Volumes)/)[^'"\r\n]*)\k<q>""")]
    private static partial Regex Quoted();

    /// A path without quotes ends at the first space.
    [GeneratedRegex("""(?:(?<![\w])[A-Za-z]:[\\/]|\\\\(?=\w)|(?<![\w.~])/(?:Users|home|private|var|tmp|Volumes)/)[^\s'"<>|]*""")]
    private static partial Regex Bare();
}
