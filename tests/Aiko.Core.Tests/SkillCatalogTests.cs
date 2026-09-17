namespace Aiko.Core.Tests;

public class SkillCatalogTests
{
    [Fact]
    public void Every_skill_has_a_short_name_and_is_listed_once()
    {
        Assert.Equal(11, SkillCatalog.All.Count);
        Assert.All(SkillCatalog.All, name => Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", name));
        Assert.All(SkillCatalog.All, name => Assert.False(name.StartsWith("aiko-", StringComparison.Ordinal)));
        Assert.Equal(SkillCatalog.All.Count, SkillCatalog.All.Distinct().Count());
    }

    [Fact]
    public void A_skill_is_called_through_the_plugin_name()
    {
        Assert.Equal("/aiko:copy", SkillCatalog.Call("copy"));
        Assert.Equal("/aiko:gamedesign-research", SkillCatalog.Call("gamedesign-research"));
    }
}
