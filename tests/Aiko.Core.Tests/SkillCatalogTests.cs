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
    public void The_count_follows_the_switches()
    {
        Assert.Equal(8, SkillCatalog.OnCount(PersonaSettings.Default));
        Assert.Equal(7, SkillCatalog.OnCount(PersonaSettings.Default.WithSkill("aiko-palette", false)));
    }
}
