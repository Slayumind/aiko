using System.Text.RegularExpressions;

namespace Aiko.Core.Tests;

/// The rules of the design system, read from the source of the window. A screen names tokens and
/// shared styles, so a change of the look is made in one place and nothing old stays behind.
public class DesignSystemTests
{
    private static readonly string App = Path.Combine(FindRepository(), "src", "Aiko.App");

    private static string FindRepository()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !Directory.Exists(Path.Combine(folder.FullName, "src", "Aiko.App")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new DirectoryNotFoundException("src/Aiko.App");
    }

    private static IEnumerable<string> Sources(params string[] patterns) =>
        patterns.SelectMany(pattern => Directory.EnumerateFiles(App, pattern, SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"));

    private static string Name(string path) => Path.GetRelativePath(App, path);

    /// Comments may name anything.
    private static string WithoutComments(string text) =>
        Regex.Replace(Regex.Replace(text, "<!--.*?-->", "", RegexOptions.Singleline), @"^\s*//.*$", "", RegexOptions.Multiline);

    private static void NoneMatch(string pattern, IEnumerable<string> files, params string[] allowed)
    {
        var found = files
            .Where(path => !allowed.Contains(Name(path)))
            .Where(path => Regex.IsMatch(WithoutComments(File.ReadAllText(path)), pattern))
            .Select(Name)
            .ToList();
        Assert.True(found.Count == 0, $"{pattern}: {string.Join(", ", found)}");
    }

    [Fact]
    public void Colours_are_written_only_among_the_tokens() =>
        NoneMatch("\"#[0-9A-Fa-f]{6,8}\"", Sources("*.xaml", "*.cs"), Path.Combine("Theme", "Tokens.xaml"));

    [Fact]
    public void Text_sizes_come_from_tokens() =>
        NoneMatch(@"FontSize\s*=\s*""?\d", Sources("*.xaml", "*.cs"), Path.Combine("Theme", "Tokens.xaml"));

    [Fact]
    public void Corners_are_never_rounded() =>
        NoneMatch("CornerRadius", Sources("*.xaml", "*.cs"));

    [Fact]
    public void Every_drawn_line_stands_on_whole_pixels()
    {
        NoneMatch(@"<Border\b[^>]*\b(BorderThickness=""(?!0"")|Height=""1""|Width=""1"")", Sources("*.xaml"), Path.Combine("Island", "IslandPanel.xaml"));
        NoneMatch(@"new Border\s*\{[^}]*\b(BorderThickness\s*=|Height\s*=\s*1\b|Width\s*=\s*1\b)", Sources("*.cs"));
    }

    [Fact]
    public void Every_token_is_in_use()
    {
        var tokens = Path.Combine(App, "Theme", "Tokens.xaml");
        var everything = string.Concat(Sources("*.xaml", "*.cs").Select(File.ReadAllText));
        var unused = Regex.Matches(File.ReadAllText(tokens), "x:Key=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .Where(key => Regex.Matches(everything, $@"(?<![\w]){Regex.Escape(key)}(?![\w])").Count < 2)
            .ToList();
        Assert.True(unused.Count == 0, string.Join(", ", unused));
    }
}
