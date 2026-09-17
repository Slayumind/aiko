using System.Text.Json;
using System.Text.RegularExpressions;
using Aiko.Core;

namespace Aiko.Core.Tests;

public class PersonaPromptTests
{
    private const string Bridge = @"C:\Users\Тест Юзер\AppData\Local\Slayumind.Aiko\current\Aiko.Bridge.exe";

    public static TheoryData<Temperament> AllTemperaments => new(Enum.GetValues<Temperament>());

    [Theory]
    [MemberData(nameof(AllTemperaments))]
    public void Guardrails_are_the_last_block_at_every_temperament(Temperament temperament)
    {
        var prompt = PersonaPrompt.Compose(temperament);

        Assert.EndsWith(PersonaPrompt.Guardrails.TrimEnd() + "\n", prompt);
        Assert.Contains("commit messages", prompt);
        Assert.Contains("security warnings", prompt);
    }

    /// The guardrails come last and tell Aiko to go neutral. Read alone, "neutral" takes the whole
    /// voice block with it, including the line that says she is a woman — which is how a live
    /// session ended up writing about her in the masculine. The rule has to be repeated inside the
    /// guardrails themselves, after the list, or it is not there when it is needed.
    [Theory]
    [MemberData(nameof(AllTemperaments))]
    public void The_silent_places_keep_the_language_and_the_feminine_forms(Temperament temperament)
    {
        var prompt = PersonaPrompt.Compose(temperament);
        var guardrails = prompt[prompt.IndexOf("# Where you stay silent", StringComparison.Ordinal)..];

        Assert.Contains("feminine forms", guardrails);
        Assert.Contains("the user's language", guardrails);
    }

    /// A working day is a row of reports, plans and warnings. Without this the persona goes quiet
    /// at the first one and never comes back.
    [Theory]
    [MemberData(nameof(AllTemperaments))]
    public void The_silence_belongs_to_the_place_and_not_to_the_session(Temperament temperament)
    {
        var prompt = PersonaPrompt.Compose(temperament);

        Assert.Contains("the silence belongs to the place", prompt);
        Assert.Contains("not one long silent place", prompt);
    }

    [Fact]
    public void Each_temperament_has_its_own_block()
    {
        var blocks = Enum.GetValues<Temperament>().Select(PersonaPrompt.Compose).ToList();

        Assert.Equal(blocks.Count, blocks.Distinct().Count());
        Assert.Contains("# Temperament: musou", PersonaPrompt.Compose(Temperament.Musou));
    }

    [Theory]
    [MemberData(nameof(AllTemperaments))]
    public void Only_the_allowed_Japanese_appears(Temperament temperament)
    {
        var runs = Regex.Matches(PersonaPrompt.StyleFile(temperament), @"[\u3040-\u30ff\u4e00-\u9fff]+")
            .Select(m => m.Value)
            .Distinct();

        Assert.All(runs, run => Assert.Contains(run, PersonaPrompt.AllowedJapanese));
    }

    /// Being a woman is who Aiko is, not how loud she talks, so it lives next to her name. Russian
    /// examples cover the short adjectives too («готова», «уверена»): those slipped past the old rule.
    [Theory]
    [MemberData(nameof(AllTemperaments))]
    public void Who_she_is_says_she_is_a_woman_with_Russian_examples(Temperament temperament)
    {
        var prompt = PersonaPrompt.Compose(temperament);
        var character = prompt[..prompt.IndexOf("# How you talk", StringComparison.Ordinal)];

        Assert.Contains("You are a woman", character);
        foreach (var form in new[] { "проверила", "нашла", "готова", "уверена" })
        {
            Assert.Contains(form, character);
        }
    }

    /// A game named at the end of every report about patches and checklists reads as a tic. A game is
    /// named only when the thing at hand really works like it, at every temperament.
    [Theory]
    [MemberData(nameof(AllTemperaments))]
    public void A_game_is_named_only_for_a_real_likeness(Temperament temperament)
    {
        var prompt = PersonaPrompt.Compose(temperament);

        Assert.Contains("Name a game only when", prompt);
        Assert.Contains("never as the closing joke", prompt);
        Assert.DoesNotContain("in any topic", prompt);
        Assert.DoesNotContain("creative task", prompt);
    }

    [Fact]
    public void The_favourite_games_are_named()
    {
        var prompt = PersonaPrompt.Compose(Temperament.Normal);

        foreach (var game in new[] { "Undertale", "Souls", "Elden Ring", "Slay the Spire", "Skyrim" })
        {
            Assert.Contains(game, prompt);
        }
    }

    [Theory]
    [MemberData(nameof(AllTemperaments))]
    public void The_style_keeps_the_coding_instructions_and_turns_on_with_the_plugin(Temperament temperament)
    {
        var style = PersonaPrompt.StyleFile(temperament);

        Assert.StartsWith("---\nname: Aiko\n", style);
        Assert.Contains("\nkeep-coding-instructions: true\n", style);
        Assert.Contains("\nforce-for-plugin: true\n", style);
    }

    [Fact]
    public void The_plugin_has_a_manifest_a_style_and_hooks()
    {
        var files = PersonaPlugin.Files(Temperament.Normal, Bridge);

        Assert.Equal([".claude-plugin/plugin.json", "hooks/hooks.json", "output-styles/aiko.md"], files.Keys.Order(StringComparer.Ordinal));
        using var manifest = JsonDocument.Parse(files[".claude-plugin/plugin.json"]);
        Assert.Equal("aiko-persona", manifest.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void Hooks_run_the_bridge_without_a_shell_even_with_spaces_and_cyrillic_in_the_path()
    {
        using var hooks = JsonDocument.Parse(PersonaPlugin.Files(Temperament.Normal, Bridge)["hooks/hooks.json"]);
        var events = hooks.RootElement.GetProperty("hooks");

        Assert.Equal(PersonaPlugin.HookEvents, events.EnumerateObject().Select(e => e.Name));
        foreach (var e in events.EnumerateObject())
        {
            var hook = e.Value[0].GetProperty("hooks")[0];
            Assert.Equal("command", hook.GetProperty("type").GetString());
            Assert.Equal(Bridge, hook.GetProperty("command").GetString());
            Assert.Equal(["hook"], hook.GetProperty("args").EnumerateArray().Select(a => a.GetString()));
            Assert.True(hook.GetProperty("async").GetBoolean());
        }
    }

    [Fact]
    public void The_hash_is_stable_and_follows_the_content()
    {
        var normal = PersonaPlugin.ContentHash(PersonaPlugin.Files(Temperament.Normal, Bridge));

        Assert.Matches("^[0-9a-f]{12}$", normal);
        Assert.Equal(normal, PersonaPlugin.ContentHash(PersonaPlugin.Files(Temperament.Normal, Bridge)));
        Assert.NotEqual(normal, PersonaPlugin.ContentHash(PersonaPlugin.Files(Temperament.Musou, Bridge)));
        Assert.NotEqual(normal, PersonaPlugin.ContentHash(PersonaPlugin.Files(Temperament.Normal, @"D:\Aiko.Bridge.exe")));
    }
}
