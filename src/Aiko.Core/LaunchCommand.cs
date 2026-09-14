using System.Text;

namespace Aiko.Core;

/// The name of the command that starts Claude Code in one environment: "aiko-work".
///
/// Until the person types a name of their own, the command follows the environment name, so
/// renaming Work to Job makes it aiko-job. The command is a file in PATH, which is why the name
/// is plain latin letters, digits and hyphens: it has to be typed in PowerShell, cmd and Git Bash
/// alike.
public static class LaunchCommand
{
    public const string Prefix = "aiko-";
    public const int MaxLength = 40;

    public static string FromEnvironmentName(string environmentName)
    {
        var slug = Slug(environmentName);
        return Prefix + (slug.Length > 0 ? slug : "env");
    }

    public static CommandProblem Check(string command, IEnumerable<string> takenByOthers)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return CommandProblem.Empty;
        }

        if (command.Length > MaxLength)
        {
            return CommandProblem.TooLong;
        }

        if (!command.All(IsAllowed) || command[0] == '-')
        {
            return CommandProblem.BadCharacters;
        }

        // The shim itself is claude.exe. A command with that name would start itself forever.
        if (command.Equals("claude", StringComparison.OrdinalIgnoreCase))
        {
            return CommandProblem.Reserved;
        }

        return takenByOthers.Any(other => other.Equals(command, StringComparison.OrdinalIgnoreCase))
            ? CommandProblem.Taken
            : CommandProblem.None;
    }

    private static bool IsAllowed(char c) => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';

    /// Latin letters stay, Russian letters are spelled in latin, everything else becomes one hyphen.
    /// Also names the folder of a new environment, so a command and its folder read the same.
    public static string Slug(string name)
    {
        var builder = new StringBuilder();
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(c);
            }
            else if (Cyrillic.TryGetValue(c, out var latin))
            {
                builder.Append(latin);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length > MaxLength - Prefix.Length ? slug[..(MaxLength - Prefix.Length)].TrimEnd('-') : slug;
    }

    private static readonly Dictionary<char, string> Cyrillic = new()
    {
        ['а'] = "a",
        ['б'] = "b",
        ['в'] = "v",
        ['г'] = "g",
        ['д'] = "d",
        ['е'] = "e",
        ['ё'] = "e",
        ['ж'] = "zh",
        ['з'] = "z",
        ['и'] = "i",
        ['й'] = "y",
        ['к'] = "k",
        ['л'] = "l",
        ['м'] = "m",
        ['н'] = "n",
        ['о'] = "o",
        ['п'] = "p",
        ['р'] = "r",
        ['с'] = "s",
        ['т'] = "t",
        ['у'] = "u",
        ['ф'] = "f",
        ['х'] = "kh",
        ['ц'] = "ts",
        ['ч'] = "ch",
        ['ш'] = "sh",
        ['щ'] = "shch",
        ['ъ'] = "",
        ['ы'] = "y",
        ['ь'] = "",
        ['э'] = "e",
        ['ю'] = "yu",
        ['я'] = "ya",
    };
}

public enum CommandProblem
{
    None,
    Empty,
    TooLong,
    BadCharacters,
    Reserved,
    Taken,
}
