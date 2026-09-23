namespace Aiko.Core.Tests;

public class SkillCatalogTests
{
    [Fact]
    public void Every_skill_has_a_short_name_and_is_listed_once()
    {
        Assert.Equal(12, SkillCatalog.All.Count);
        Assert.All(SkillCatalog.All, name => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", name));
        Assert.All(SkillCatalog.All, name => Assert.False(name.StartsWith("aiko-", StringComparison.Ordinal)));
        Assert.Equal(SkillCatalog.All.Count, SkillCatalog.All.Distinct().Count());
    }

    [Fact]
    public void Every_skill_is_in_exactly_one_domain_and_the_list_follows_the_domains()
    {
        Assert.Equal([SkillDomain.Projects, SkillDomain.Games], SkillCatalog.Groups.Select(g => g.Domain));
        Assert.All(SkillCatalog.Groups, group => Assert.NotEmpty(group.Skills));
        Assert.Equal(SkillCatalog.Groups.SelectMany(g => g.Skills), SkillCatalog.All);
        Assert.Contains("calendar", SkillCatalog.Groups[0].Skills);
        Assert.Contains("playtest", SkillCatalog.Groups[1].Skills);
    }

    [Fact]
    public void A_skill_is_called_through_the_plugin_name()
    {
        Assert.Equal("/aiko:copy", SkillCatalog.Call("copy"));
        Assert.Equal("/aiko:gamedesign-research", SkillCatalog.Call("gamedesign-research"));
    }
}
