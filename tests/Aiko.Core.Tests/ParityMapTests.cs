using System.Text.RegularExpressions;

namespace Aiko.Core.Tests;

/// The two maps of the cores, mac/PARITY.md and spec/cases/README.md, against the files on the disk.
/// Both fell behind during a month of work on the terminal: the map named deleted files and missed
/// new ones. A map that drifts now fails here, not in a review.
public partial class ParityMapTests
{
    private static readonly string Root = SpecCases.Repository();

    [Fact]
    public void Every_file_the_parity_map_names_is_on_the_disk()
    {
        var onDisk = SourceFiles("src", "mac", "tests").Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);

        var missing = FileName().Matches(Map()).Select(match => match.Value).Distinct().Where(name => !onDisk.Contains(name));

        Assert.Empty(missing);
    }

    [Fact]
    public void Every_file_of_the_two_cores_is_in_the_parity_map()
    {
        var map = Map();
        var files = SourceFiles(Path.Combine("src", "Aiko.Core")).Where(path => path.EndsWith(".cs", StringComparison.Ordinal))
            .Concat(SourceFiles(Path.Combine("mac", "Sources", "AikoKit")).Where(path => path.EndsWith(".swift", StringComparison.Ordinal)));

        var missing = files.Select(Path.GetFileNameWithoutExtension).Where(name => !Regex.IsMatch(map, $@"\b{name}\b"));

        Assert.Empty(missing);
    }

    [Fact]
    public void The_table_of_shared_cases_names_every_folder_and_no_other()
    {
        var cases = Path.Combine(Root, "spec", "cases");
        var table = TableFolder().Matches(File.ReadAllText(Path.Combine(cases, "README.md"))).Select(match => match.Groups[1].Value);

        var folders = Directory.GetDirectories(cases).Select(Path.GetFileName);

        Assert.Equal(folders.Order(StringComparer.Ordinal), table.Order(StringComparer.Ordinal));
    }

    private static string Map() => File.ReadAllText(Path.Combine(Root, "mac", "PARITY.md"));

    /// Source files under the given folders of the repository, without build output.
    private static IEnumerable<string> SourceFiles(params string[] folders) =>
        folders.SelectMany(folder => Directory.EnumerateFiles(Path.Combine(Root, folder), "*", SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or ".build"));

    /// A file name the map can mention: "Notices.cs", "IslandView.swift", "Tokens.xaml", "App.xaml.cs",
    /// and a part of a class in a file of its own: "TerminalHost.Settings.cs", "TerminalHost+Settings.swift".
    [GeneratedRegex(@"\b[A-Za-z][A-Za-z0-9]*([.+][A-Za-z][A-Za-z0-9]*)?(\.xaml)?\.(cs|swift|xaml)\b")]
    private static partial Regex FileName();

    /// The folder at the start of a row of the table: "| `notices/` | ...".
    [GeneratedRegex(@"^\| `([a-z0-9-]+)/`", RegexOptions.Multiline)]
    private static partial Regex TableFolder();
}
