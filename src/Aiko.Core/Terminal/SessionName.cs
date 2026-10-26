namespace Aiko.Core.Terminal;

/// The name of a session (D-299). The form does not ask for one: an empty field gives the folder
/// with a number, "aiko 3", and the form shows that name before the session is made.
public static class SessionName
{
    /// The folder with the lowest number no session of that folder has yet.
    public static string Default(string folder, IReadOnlyList<string> taken)
    {
        var name = FolderName(folder);
        for (var number = 1; ; number++)
        {
            var candidate = $"{name} {number}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// The longest name taken from a message, in UTF-16 units (Crush; research of 2026-10-26).
    public const int MostFromMessage = 50;

    /// The name a new session gets from its first message (D-342): its first line with the spaces
    /// squeezed, cut to MostFromMessage at a space when one comes after the first 20 units. A message
    /// with no words gives fallback.
    public static string FromMessage(string message, string fallback)
    {
        var line = message.Replace("\r\n", "\n").Split('\n').Select(part => part.Trim(' ', '\t')).FirstOrDefault(part => part.Length > 0);
        if (line is null)
        {
            return fallback;
        }

        line = string.Join(' ', line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries));
        if (line.Length <= MostFromMessage)
        {
            return line;
        }

        var cut = line.LastIndexOf(' ', MostFromMessage);
        var end = cut >= 20 ? cut : MostFromMessage;
        if (char.IsHighSurrogate(line[end - 1]))
        {
            end--;
        }

        return line[..end].TrimEnd(' ', ',', ';', ':', '-', '—');
    }

    /// A name as it is kept: one line, without spaces around it.
    public static string Clean(string? name) =>
        (name ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim(' ');

    /// A folder as it is kept: without a slash at the end, so one folder is one group. The root
    /// of a disk keeps its slash.
    public static string FolderPath(string? folder)
    {
        var path = (folder ?? "").Trim(' ');
        while (path.Length > 1 && path[^1] is '/' or '\\' && path[^2] != ':')
        {
            path = path[..^1];
        }
        return path;
    }

    /// The last part of a path, with a slash of either kind: the name of the folder. A root has
    /// no last part and is named as it is written.
    /// The name of the folder this folder sits in, shown beside its name in the form of a new
    /// session instead of the whole path (D-332). A drive or the root has none.
    public static string ParentName(string folder)
    {
        var path = FolderPath(folder).TrimEnd('/', '\\');
        var cut = path.LastIndexOfAny(['/', '\\']);
        if (cut <= 0)
        {
            return "";
        }

        var parent = path[..cut];
        return parent.EndsWith(':') ? "" : FolderName(parent);
    }

    public static string FolderName(string folder)
    {
        var path = FolderPath(folder);
        var name = path.TrimEnd('/', '\\');
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..];
        return name.Length > 0 ? name : path;
    }
}
