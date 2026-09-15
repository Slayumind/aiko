using Aiko.Core;

namespace Aiko.Core.Tests;

public class PersonaSettingsTests
{
    [Fact]
    public void By_default_Aiko_is_normal_chibi_with_every_skill_on()
    {
        var persona = PersonaSettings.Default;

        Assert.Equal(Temperament.Normal, persona.Temperament);
        Assert.Equal(FaceStyle.Chibi, persona.Face);
        Assert.Empty(persona.DisabledSkills);
        Assert.True(persona.IsSkillOn("aiko-copy"));
    }

    [Fact]
    public void A_skill_that_arrives_with_an_update_is_on()
    {
        var persona = PersonaSettings.Default.WithSkill("aiko-copy", false);

        Assert.True(persona.IsSkillOn("aiko-gamedesign-research"));
    }

    [Fact]
    public void Switching_a_skill_off_and_on_again_leaves_nothing_behind()
    {
        var off = PersonaSettings.Default.WithSkill("aiko-palette", false);
        var on = off.WithSkill("aiko-palette", true);

        Assert.False(off.IsSkillOn("aiko-palette"));
        Assert.Equal(PersonaSettings.Default, on);
    }

    [Fact]
    public void Switching_a_skill_to_the_state_it_has_changes_nothing()
    {
        var persona = PersonaSettings.Default.WithSkill("aiko-copy", false);

        Assert.Same(persona, persona.WithSkill("aiko-copy", false));
    }

    [Fact]
    public void The_persona_survives_a_trip_through_the_file()
    {
        var persona = (PersonaSettings.Default with { Temperament = Temperament.Musou, Face = FaceStyle.Emoji })
            .WithSkill("aiko-texturing", false)
            .WithSkill("aiko-copy", false);

        var back = PersonaSettings.FromJson(persona.ToJson());

        Assert.Equal(persona, back);
        Assert.Equal(["aiko-copy", "aiko-texturing"], back.DisabledSkills);
    }

    [Fact]
    public void Choices_are_written_as_words()
    {
        var json = (PersonaSettings.Default with { Temperament = Temperament.Musou, Face = FaceStyle.Emoji }).ToJson();

        Assert.Contains("\"Musou\"", json);
        Assert.Contains("\"Emoji\"", json);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{ \"temperament\": ")]
    [InlineData("{ \"temperament\": \"Shouting\" }")]
    [InlineData("{ \"face\": \"Pixel\" }")]
    public void A_broken_file_or_an_unknown_choice_reads_as_the_defaults(string json)
    {
        Assert.Equal(PersonaSettings.Default, PersonaSettings.FromJson(json));
    }

    [Fact]
    public void Empty_and_repeated_skill_names_in_the_file_are_dropped()
    {
        var persona = PersonaSettings.FromJson("""{ "disabledSkills": ["aiko-copy", "", "aiko-copy", null] }""");

        Assert.Equal(["aiko-copy"], persona.DisabledSkills);
    }

    [Fact]
    public void A_file_without_a_skill_list_has_every_skill_on()
    {
        var persona = PersonaSettings.FromJson("""{ "temperament": "Quiet", "disabledSkills": null }""");

        Assert.Equal(Temperament.Quiet, persona.Temperament);
        Assert.Empty(persona.DisabledSkills);
    }
}
