using System.Text.Json;

namespace Aiko.Core.Tests;

/// The case files under spec/cases, which the Swift tests of AikoKit read too.
///
/// A rule that is a table of inputs and answers lives there instead of twice in two languages, so
/// it cannot change in one core and stay as it was in the other.
public static class SpecCases
{
    public static string Folder(string name) => Path.Combine(Repository(), "spec", "cases", name);

    public static string File(string name, string file) => Path.Combine(Folder(name), file);

    public static byte[] Bytes(string name, string file) => System.IO.File.ReadAllBytes(File(name, file));

    public static string Text(string name, string file) => System.IO.File.ReadAllText(File(name, file));

    /// The whole file as JSON. The document is kept alive by the caller through the element it
    /// returns, so the cases are read once per test.
    public static JsonElement Json(string name, string file = "cases.json") =>
        JsonDocument.Parse(Text(name, file)).RootElement.Clone();

    /// The tests run from bin/, so the repository is the first folder above with the solution in it.
    public static string Repository()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !System.IO.File.Exists(Path.Combine(folder.FullName, "Aiko.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("Aiko.slnx not found.");
    }
}
