using Aiko.Core;

namespace Aiko.Core.Tests;

public class PersonaSettingsTests
{
    [Fact]
    public void By_default_Aiko_is_normal()
    {
        var persona = PersonaSettings.Default;

        Assert.Equal(Temperament.Normal, persona.Temperament);
    }

    [Fact]
    public void The_persona_survives_a_trip_through_the_file()
    {
        var persona = PersonaSettings.Default with { Temperament = Temperament.Musou };

        var back = PersonaSettings.FromJson(persona.ToJson());

        Assert.Equal(persona, back);
    }

    [Fact]
    public void Choices_are_written_as_words()
    {
        var json = (PersonaSettings.Default with { Temperament = Temperament.Musou }).ToJson();

        Assert.Contains("\"Musou\"", json);
    }

    [Fact]
    public void The_face_style_of_an_older_file_is_left_behind()
    {
        var persona = PersonaSettings.FromJson("{ \"temperament\": \"Musou\", \"face\": \"Emoji\" }");

        Assert.Equal(Temperament.Musou, persona.Temperament);
        Assert.DoesNotContain("face", persona.ToJson());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{ \"temperament\": ")]
    [InlineData("{ \"temperament\": \"Shouting\" }")]
    public void A_broken_file_or_an_unknown_choice_reads_as_the_defaults(string json)
    {
        Assert.Equal(PersonaSettings.Default, PersonaSettings.FromJson(json));
    }

    /// The skills had switches of their own until 0.2.1 and one switch for all until 0.3.1. They
    /// come with Aiko now (D-285), and a file from those days must still read.
    [Theory]
    [InlineData("""{ "temperament": "Quiet", "disabledSkills": ["aiko-copy", "aiko-palette"] }""")]
    [InlineData("""{ "temperament": "Quiet", "skillsOn": false }""")]
    public void A_file_with_a_skills_switch_of_an_older_aiko_still_reads(string json)
    {
        var persona = PersonaSettings.FromJson(json);

        Assert.Equal(Temperament.Quiet, persona.Temperament);
        Assert.DoesNotContain("skillsOn", persona.ToJson());
        Assert.DoesNotContain("disabledSkills", persona.ToJson());
    }
}
