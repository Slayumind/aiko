using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aiko.Core.Tests;

public class SkillPluginTests
{
    private static (string, byte[]) File(string path, string text) => (path, Encoding.UTF8.GetBytes(text));

    [Fact]
    public void The_hash_changes_with_any_file_and_not_with_the_order_or_the_slashes()
    {
        var files = new[] { File("skills/copy/SKILL.md", "a"), File(".claude-plugin/plugin.json", "{}") };

        var hash = SkillPlugin.ContentHash(files);

        Assert.Matches("^[0-9a-f]{12}$", hash);
        Assert.Equal(hash, SkillPlugin.ContentHash(files.Reverse()));
        Assert.Equal(hash, SkillPlugin.ContentHash([File(@"skills\copy\SKILL.md", "a"), File(@".claude-plugin\plugin.json", "{}")]));
        Assert.NotEqual(hash, SkillPlugin.ContentHash([File("skills/copy/SKILL.md", "b"), File(".claude-plugin/plugin.json", "{}")]));
    }

    [Fact]
    public void The_copy_carries_the_plugin_version_with_the_hash()
    {
        var manifest = SkillPlugin.VersionedManifest("""{ "name": "aiko", "version": "1.2.0", "license": "Apache-2.0" }""", "0123456789ab");

        var root = JsonNode.Parse(manifest!)!.AsObject();
        Assert.Equal("1.2.0+0123456789ab", root["version"]!.GetValue<string>());
        Assert.Equal("Apache-2.0", root["license"]!.GetValue<string>());
        Assert.Equal("1.2.0+0123456789ab", SkillPlugin.VersionOf(manifest));

        // Copied again, the old hash is replaced, not added to.
        Assert.Equal("1.2.0+ffffffffffff", SkillPlugin.VersionOf(SkillPlugin.VersionedManifest(manifest!, "ffffffffffff")));
        Assert.Equal("0.0.0+0123456789ab", SkillPlugin.VersionOf(SkillPlugin.VersionedManifest("""{ "name": "aiko" }""", "0123456789ab")));
        Assert.Null(SkillPlugin.VersionedManifest("not json", "0123456789ab"));
    }

    [Fact]
    public void Only_the_skills_plugin_is_read_from_a_manifest()
    {
        Assert.Equal(new SkillPlugin("aiko", "Text."), SkillPlugin.FromManifest("""{ "name": "aiko", "description": "Text." }"""));
        Assert.Null(SkillPlugin.FromManifest("""{ "name": "aiko-copy" }"""));
        Assert.Null(SkillPlugin.FromManifest("""{ "name": "someone-else" }"""));
        Assert.Null(SkillPlugin.FromManifest(null));
    }

    [Fact]
    public void The_marketplace_lists_the_persona_and_then_the_skills_plugin()
    {
        var json = AikoMarketplace.Json("\"x.exe\" plugin aiko-persona", new SkillPlugin("aiko", "All skills."));

        var plugins = JsonNode.Parse(json)!["plugins"]!.AsArray();
        Assert.Equal(["aiko-persona", "aiko"], plugins.Select(p => p!["name"]!.GetValue<string>()));
        Assert.Equal("./plugins/aiko", plugins[1]!["source"]!.GetValue<string>());
        Assert.Single(JsonNode.Parse(AikoMarketplace.Json("\"x.exe\" plugin aiko-persona"))!["plugins"]!.AsArray());
    }

    // ---- the plugin in the repository ----

    private static string Repository()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !System.IO.File.Exists(Path.Combine(folder.FullName, "Aiko.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("the repository root was not found");
    }

    private static string PluginFolder() => Path.Combine(Repository(), SkillPlugin.FolderName, SkillCatalog.PluginName);

    public static TheoryData<string> ShippedSkills()
    {
        var data = new TheoryData<string>();
        foreach (var folder in Directory.GetDirectories(Path.Combine(PluginFolder(), SkillPlugin.SkillsFolder)))
        {
            data.Add(Path.GetFileName(folder));
        }

        return data;
    }

    [Fact]
    public void One_plugin_ships_and_it_holds_every_skill_of_the_catalog()
    {
        Assert.Equal([SkillCatalog.PluginName], Directory.GetDirectories(Path.Combine(Repository(), SkillPlugin.FolderName)).Select(Path.GetFileName));

        var manifest = SkillPlugin.FromManifest(System.IO.File.ReadAllText(Path.Combine(PluginFolder(), ".claude-plugin", "plugin.json")));
        Assert.Equal(SkillCatalog.PluginName, manifest?.Name);
        Assert.False(string.IsNullOrWhiteSpace(manifest?.Description));

        var folders = Directory.GetDirectories(Path.Combine(PluginFolder(), SkillPlugin.SkillsFolder)).Select(Path.GetFileName).Order(StringComparer.Ordinal);
        Assert.Equal(SkillCatalog.All.Order(StringComparer.Ordinal), folders);
    }

    [Theory]
    [MemberData(nameof(ShippedSkills))]
    public void A_shipped_skill_is_named_after_its_folder(string name)
    {
        var skill = System.IO.File.ReadAllText(Path.Combine(PluginFolder(), SkillPlugin.SkillsFolder, name, "SKILL.md")).ReplaceLineEndings("\n");

        Assert.StartsWith("---", skill);
        Assert.Contains($"\nname: {name}\n", skill);
        Assert.Contains("\ndescription: ", skill);
        Assert.DoesNotContain("slayumind", skill, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_public_marketplace_lists_the_one_plugin()
    {
        using var json = JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine(Repository(), ".claude-plugin", "marketplace.json")));
        var listed = json.RootElement.GetProperty("plugins").EnumerateArray()
            .Select(p => (p.GetProperty("name").GetString(), p.GetProperty("source").GetString()))
            .ToList();

        Assert.Equal([(SkillCatalog.PluginName, $"./plugins/{SkillCatalog.PluginName}")], listed);
    }
}
