using System.Text.Json;

namespace Aiko.Core.Tests;

/// The skills plugin is described twice: in its own manifest and in the marketplace that lists it.
/// The two texts drifted apart once, and a new skill was in neither.
public class PluginManifestTests
{
    private static readonly string Root = SpecCases.Repository();

    [Fact]
    public void The_marketplace_describes_the_plugin_in_the_same_words_as_the_plugin()
    {
        var plugin = Read("plugins", "aiko", ".claude-plugin", "plugin.json");
        var marketplace = Read(".claude-plugin", "marketplace.json");

        var listed = marketplace.GetProperty("plugins").EnumerateArray().Single(entry => entry.GetProperty("name").GetString() == "aiko");

        Assert.Equal(plugin.GetProperty("description").GetString(), listed.GetProperty("description").GetString());
    }

    private static JsonElement Read(params string[] path) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine([Root, .. path]))).RootElement.Clone();
}
