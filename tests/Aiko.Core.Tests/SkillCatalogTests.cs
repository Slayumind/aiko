namespace Aiko.Core.Tests;

public class SkillCatalogTests
{
    [Fact]
    public void Every_skill_is_named_aiko_something_and_listed_once()
    {
        Assert.Equal(8, SkillCatalog.All.Count);
        Assert.All(SkillCatalog.All, name => Assert.StartsWith("aiko-", name));
        Assert.Equal(SkillCatalog.All.Count, SkillCatalog.All.Distinct().Count());
        Assert.DoesNotContain(PersonaPlugin.Name, SkillCatalog.All);
    }

    [Fact]
    public void An_own_skill_with_the_same_name_is_found_per_folder()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(@"C:\u\.claude", "skills", "aiko-copy", "SKILL.md"),
            Path.Combine(@"C:\u\.claude-work", "skills", "other", "SKILL.md"),
        };

        Assert.Equal([@"C:\u\.claude"], SkillCatalog.OwnTwinsIn("aiko-copy", [@"C:\u\.claude", @"C:\u\.claude-work"], files.Contains));
        Assert.Empty(SkillCatalog.OwnTwinsIn("aiko-palette", [@"C:\u\.claude", @"C:\u\.claude-work"], files.Contains));
    }

    [Fact]
    public void The_count_follows_the_switches()
    {
        Assert.Equal(8, SkillCatalog.OnCount(PersonaSettings.Default));
        Assert.Equal(7, SkillCatalog.OnCount(PersonaSettings.Default.WithSkill("aiko-palette", false)));
    }
}
