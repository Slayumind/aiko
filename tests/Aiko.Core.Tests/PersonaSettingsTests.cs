using Aiko.Core;

namespace Aiko.Core.Tests;

public class PersonaSettingsTests
{
    [Fact]
    public void By_default_Aiko_is_normal_chibi_with_the_skills_on()
    {
        var persona = PersonaSettings.Default;

        Assert.Equal(Temperament.Normal, persona.Temperament);
        Assert.Equal(FaceStyle.Chibi, persona.Face);
        Assert.True(persona.SkillsOn);
    }

    [Fact]
    public void The_persona_survives_a_trip_through_the_file()
    {
        var persona = PersonaSettings.Default with { Temperament = Temperament.Musou, Face = FaceStyle.Emoji, SkillsOn = false };

        var back = PersonaSettings.FromJson(persona.ToJson());

        Assert.Equal(persona, back);
        Assert.Contains("\"skillsOn\": false", persona.ToJson());
        Assert.DoesNotContain("disabledSkills", persona.ToJson());
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

    // ---- files written when each skill had its own switch ----

    [Theory]
    [InlineData("""{ "temperament": "Quiet" }""")]
    [InlineData("""{ "temperament": "Quiet", "disabledSkills": null }""")]
    [InlineData("""{ "temperament": "Quiet", "disabledSkills": [] }""")]
    [InlineData("""{ "temperament": "Quiet", "disabledSkills": ["aiko-copy", "aiko-palette"] }""")]
    [InlineData("""{ "temperament": "Quiet", "disabledSkills": ["copy", "", null] }""")]
    public void An_old_file_with_any_skill_left_on_has_the_skills_on(string json)
    {
        var persona = PersonaSettings.FromJson(json);

        Assert.Equal(Temperament.Quiet, persona.Temperament);
        Assert.True(persona.SkillsOn);
    }

    [Fact]
    public void An_old_file_with_every_skill_off_has_the_skills_off()
    {
        var oldNames = PersonaSettings.FromJson("""
            { "disabledSkills": ["aiko-copy", "aiko-release-gate", "aiko-docs-hygiene", "aiko-blender-to-unity",
                                 "aiko-texturing", "aiko-glb-for-web", "aiko-palette", "aiko-gamedesign-research"] }
            """);
        var newNames = PersonaSettings.FromJson("""
            { "disabledSkills": ["copy", "release-gate", "docs-hygiene", "blender-to-unity",
                                 "texturing", "glb-for-web", "palette", "gamedesign-research"] }
            """);

        Assert.False(oldNames.SkillsOn);
        Assert.False(newNames.SkillsOn);
    }

    [Fact]
    public void The_new_switch_wins_over_an_old_list_in_the_same_file()
    {
        var persona = PersonaSettings.FromJson("""{ "skillsOn": true, "disabledSkills": ["copy", "release-gate", "docs-hygiene", "blender-to-unity", "texturing", "glb-for-web", "palette", "gamedesign-research"] }""");

        Assert.True(persona.SkillsOn);
    }
}
