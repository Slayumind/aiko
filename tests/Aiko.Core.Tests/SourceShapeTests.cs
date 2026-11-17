namespace Aiko.Core.Tests;

/// Shapes of C# that compile and do something else than they say. A line was once put in after
/// "SettingsAsked += () =>", whose body started on the next line: the gear then subscribed the
/// palette once more instead of opening the settings, and the old body ran once when the window was
/// made. Both shapes of that mistake are looked for in every project.
public class SourceShapeTests
{
    [Fact]
    public void No_lambda_has_another_subscription_for_its_body_and_no_semicolon_stands_alone()
    {
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(SpecCases.Repository(), "src"), "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var next = i + 1 < lines.Length ? lines[i + 1].Trim() : "";
                if (lines[i].TrimEnd().EndsWith("=>", StringComparison.Ordinal) && (next.Contains(" += ", StringComparison.Ordinal) || next.Contains(" -= ", StringComparison.Ordinal)))
                {
                    found.Add($"{Path.GetFileName(file)}:{i + 1} a lambda whose body is a subscription");
                }

                if (lines[i].Trim() == ";")
                {
                    found.Add($"{Path.GetFileName(file)}:{i + 1} a semicolon on its own");
                }
            }
        }

        Assert.Empty(found);
    }
}
