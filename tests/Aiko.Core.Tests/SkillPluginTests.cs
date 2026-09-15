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
        var files = new[] { File("skills/aiko-copy/SKILL.md", "a"), File(".claude-plugin/plugin.json", "{}") };

        var hash = SkillPlugin.ContentHash(files);

        Assert.Matches("^[0-9a-f]{12}$", hash);
        Assert.Equal(hash, SkillPlugin.ContentHash(files.Reverse()));
        Assert.Equal(hash, SkillPlugin.ContentHash([File(@"skills\aiko-copy\SKILL.md", "a"), File(@".claude-plugin\plugin.json", "{}")]));
        Assert.NotEqual(hash, SkillPlugin.ContentHash([File("skills/aiko-copy/SKILL.md", "b"), File(".claude-plugin/plugin.json", "{}")]));
    }

    [Fact]
    public void The_copy_carries_the_skill_version_with_the_hash()
    {
        var manifest = SkillPlugin.VersionedManifest("""{ "name": "aiko-copy", "version": "1.2.0", "license": "Apache-2.0" }""", "0123456789ab");

        var root = JsonNode.Parse(manifest!)!.AsObject();
        Assert.Equal("1.2.0+0123456789ab", root["version"]!.GetValue<string>());
        Assert.Equal("Apache-2.0", root["license"]!.GetValue<string>());
        Assert.Equal("1.2.0+0123456789ab", SkillPlugin.VersionOf(manifest));

        // Copied again, the old hash is replaced, not added to.
        Assert.Equal("1.2.0+ffffffffffff", SkillPlugin.VersionOf(SkillPlugin.VersionedManifest(manifest!, "ffffffffffff")));
        Assert.Equal("0.0.0+0123456789ab", SkillPlugin.VersionOf(SkillPlugin.VersionedManifest("""{ "name": "aiko-copy" }""", "0123456789ab")));
        Assert.Null(SkillPlugin.VersionedManifest("not json", "0123456789ab"));
    }

    [Fact]
    public void Only_skills_from_the_catalog_are_read_from_a_manifest()
    {
        Assert.Equal(new SkillPlugin("aiko-copy", "Text."), SkillPlugin.FromManifest("""{ "name": "aiko-copy", "description": "Text." }"""));
        Assert.Null(SkillPlugin.FromManifest("""{ "name": "someone-else" }"""));
        Assert.Null(SkillPlugin.FromManifest(null));
    }

    [Fact]
    public void The_marketplace_lists_skills_from_folders_after_the_persona_in_catalog_order()
    {
        var json = AikoMarketplace.Json("\"x.exe\" plugin aiko-persona", [new SkillPlugin("aiko-palette", "P."), new SkillPlugin("aiko-copy", "C.")]);

        var plugins = JsonNode.Parse(json)!["plugins"]!.AsArray();
        Assert.Equal(["aiko-persona", "aiko-copy", "aiko-palette"], plugins.Select(p => p!["name"]!.GetValue<string>()));
        Assert.Equal("./plugins/aiko-copy", plugins[1]!["source"]!.GetValue<string>());
    }

    // ---- the plugins in the repository ----

    private static string Repository()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !System.IO.File.Exists(Path.Combine(folder.FullName, "Aiko.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("the repository root was not found");
    }

    public static TheoryData<string> ShippedSkills()
    {
        var data = new TheoryData<string>();
        foreach (var folder in Directory.GetDirectories(Path.Combine(Repository(), SkillPlugin.FolderName)))
        {
            data.Add(Path.GetFileName(folder));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ShippedSkills))]
    public void A_shipped_skill_is_one_plugin_with_one_skill_of_the_same_name(string name)
    {
        var root = Path.Combine(Repository(), SkillPlugin.FolderName, name);

        Assert.Contains(name, SkillCatalog.All);
        var manifest = SkillPlugin.FromManifest(System.IO.File.ReadAllText(Path.Combine(root, ".claude-plugin", "plugin.json")));
        Assert.Equal(name, manifest?.Name);
        Assert.False(string.IsNullOrWhiteSpace(manifest?.Description));

        var skill = System.IO.File.ReadAllText(Path.Combine(root, "skills", name, "SKILL.md"));
        Assert.StartsWith("---", skill);
        Assert.Contains($"\nname: {name}\n", skill.ReplaceLineEndings("\n"));
        Assert.Contains("\ndescription: ", skill.ReplaceLineEndings("\n"));
        Assert.DoesNotContain("slayumind", skill, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_public_marketplace_lists_every_shipped_skill()
    {
        using var json = JsonDocument.Parse(System.IO.File.ReadAllText(Path.Combine(Repository(), ".claude-plugin", "marketplace.json")));
        var listed = json.RootElement.GetProperty("plugins").EnumerateArray()
            .Select(p => (p.GetProperty("name").GetString(), p.GetProperty("source").GetString()))
            .ToList();

        var shipped = Directory.GetDirectories(Path.Combine(Repository(), SkillPlugin.FolderName))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .Select(name => (name, $"./plugins/{name}"))
            .ToList();

        Assert.Equal(shipped, listed.OrderBy(p => p.Item1, StringComparer.Ordinal).ToList());
    }
}
