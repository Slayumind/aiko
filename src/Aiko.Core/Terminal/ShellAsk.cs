namespace Aiko.Core.Terminal;

/// What a command line asks Claude Code (D-358, D-360): to explain a failed command, and to turn words
/// into a command. Both go through claude -p in the limits of the environment; this class only writes
/// the questions and reads the answers. Rows: spec/cases/shell-ask.
public static class ShellAsk
{
    /// The most of an output that goes into a question: its end, where the error usually is.
    public const int MostOutput = 6000;

    /// Words that ask for a command rather than run one: only a line that starts with "#", the way Warp
    /// asks (D-420). Russian letters alone are not a request: a path or a commit message has them too,
    /// and a request sends the line to Claude Code and spends the limits.
    public static bool IsRequest(string text)
    {
        var trimmed = text.Trim();
        return trimmed.StartsWith('#') && RequestText(trimmed).Length >= 2;
    }

    /// The request without the "#" that marked it.
    public static string RequestText(string text) => text.Trim().TrimStart('#').Trim();

    /// The last part of a folder path, with either kind of slash: "C:\Users\me\aiko" is "aiko".
    public static string FolderName(string folder)
    {
        var trimmed = folder.TrimEnd('\\', '/');
        var cut = trimmed.LastIndexOfAny(['\\', '/']);
        return cut < 0 ? trimmed : trimmed[(cut + 1)..];
    }

    public static string Explain(string shell, string command, int exitCode, string output, string language)
    {
        var tail = output.Length > MostOutput ? output[^MostOutput..] : output;
        return $"""
            A command failed in {Name(shell)} on Windows.
            Command: {command}
            Exit code: {exitCode}
            Its output (the end of it):
            {tail}

            In {language}, say in two to four short sentences why it failed and what to do. Plain text only,
            no Markdown, no code blocks. If a fixed command helps, put it on a line of its own.
            """;
    }

    /// folder: the folder of the session. Only its name goes into the question, not the path above it
    /// (D-420): the path names the person and their other folders.
    public static string Suggest(string shell, string folder, string request) =>
        $"""
        Write one command for {Name(shell)} on Windows, in the current folder ({FolderName(folder)}), that does this: {RequestText(request)}
        Answer with the command only: one line, no explanation, no Markdown, no code block.
        """;

    /// The command in an answer: the first line that is not a fence of a code block, without the
    /// marks a model puts around a command now and then.
    public static string? CommandIn(string answer)
    {
        foreach (var raw in answer.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("```", StringComparison.Ordinal))
            {
                continue;
            }

            line = line.Trim('`').Trim();
            foreach (var prompt in new[] { "$ ", "> ", "PS> " })
            {
                if (line.StartsWith(prompt, StringComparison.Ordinal))
                {
                    line = line[prompt.Length..].Trim();
                }
            }

            return line.Length > 0 ? line : null;
        }

        return null;
    }

    /// The lines of an explanation, each broken to fit the width in characters; a long word is cut.
    public static IReadOnlyList<string> Wrap(string text, int width)
    {
        width = Math.Max(10, width);
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", "").Trim().Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var piece = word;
                while (piece.Length > width)
                {
                    if (line.Length > 0)
                    {
                        lines.Add(line);
                        line = "";
                    }

                    lines.Add(piece[..width]);
                    piece = piece[width..];
                }

                if (line.Length == 0)
                {
                    line = piece;
                }
                else if (line.Length + 1 + piece.Length <= width)
                {
                    line += " " + piece;
                }
                else
                {
                    lines.Add(line);
                    line = piece;
                }
            }

            lines.Add(line);
        }

        return lines;
    }

    private static string Name(string shell) => shell switch
    {
        "bash" => "Git Bash",
        "pwsh" => "PowerShell 7",
        _ => "Windows PowerShell",
    };
}
