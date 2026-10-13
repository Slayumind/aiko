using System.IO;
using System.Text.RegularExpressions;

namespace Aiko.Core.Tests;

/// The log of Aiko never holds a word of a conversation (D-298). A promise like that is broken by
/// one handy line in the wrong file, so these tests read the source of the terminal.
public class TerminalPrivacyTests
{
    [Fact]
    public void Only_the_terminal_log_writes_to_the_log()
    {
        var writers = TerminalSources()
            .Where(file => Regex.IsMatch(File.ReadAllText(file), @"\bLog\s*\.\s*Write\s*\("))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(["TerminalLog.cs"], writers);
    }

    [Fact]
    public void The_terminal_log_takes_no_text_but_the_name_of_an_environment()
    {
        var seam = File.ReadAllText(Path.Combine(Repository(), "src", "Aiko.Core", "Terminal", "ConversationProcess.cs"));
        var sources = new[]
        {
            seam[seam.IndexOf("interface ITerminalLog", StringComparison.Ordinal)..],
            File.ReadAllText(Path.Combine(Repository(), "src", "Aiko.App", "Terminal", "TerminalLog.cs")),
        };

        var methods = 0;
        foreach (var source in sources)
        {
            foreach (Match method in Regex.Matches(source, @"\bvoid\s+(\w+)\s*\(([^)]*)\)"))
            {
                methods++;
                foreach (var parameter in method.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    Assert.True(
                        Regex.IsMatch(parameter, @"^(bool|int)\s+\w+$") || parameter == "string environment",
                        $"{method.Groups[1].Value} takes \"{parameter}\". The terminal log takes numbers, yes or no, and the name of an environment.");
                }
            }
        }

        // Four in the seam, the same four and the three of the doors in the app.
        Assert.Equal(11, methods);
    }

    private static IEnumerable<string> TerminalSources() =>
        new[] { "Aiko.Core", "Aiko.App" }
            .Select(project => Path.Combine(Repository(), "src", project, "Terminal"))
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories));

    private static string Repository()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Aiko.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("the repository root was not found");
    }
}
