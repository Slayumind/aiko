using Aiko.Core;

namespace Aiko.Core.Tests;

/// The two texts the model reads about the memory, and the note for a turn. Claude Code cuts a
/// hook output at 10,000 characters, so the worst case of each is measured here.
public class MemoryPromptTests
{
    private const int HookOutputLimit = 10_000;

    private static readonly DateOnly Today = new(2026, 10, 11);

    // ---- the rules ----

    [Fact]
    public void The_rules_end_with_the_day()
    {
        string rules = MemoryPrompt.Rules(new DateOnly(2026, 1, 9));

        Assert.StartsWith("# Aiko memory\n\n", rules);
        Assert.EndsWith("\n\nToday: 2026-01-09.", rules);
    }

    [Fact]
    public void The_rules_fit_one_hook_output()
    {
        Assert.InRange(MemoryPrompt.Rules(Today).Length, 1, HookOutputLimit);
    }

    [Fact]
    public void The_rules_say_nothing_about_sides_or_scope()
    {
        string rules = MemoryPrompt.Rules(Today);

        Assert.DoesNotContain("scope", rules);
        Assert.DoesNotContain("side", rules);
        Assert.DoesNotContain("{", rules);
    }

    [Theory]
    [InlineData("at most 200 characters")]
    [InlineData("One thought per fact")]
    [InlineData("\"migration is half done\"")]
    [InlineData("only what stays true")]
    [InlineData("refresh it by id, once per session")]
    [InlineData("When \"How to work with me\" is full")]
    [InlineData("Do not skip the save")]
    [InlineData("Save silently")]
    public void The_rules_carry_what_the_product_added_to_the_spike(string phrase)
    {
        Assert.Contains(phrase, string.Join(' ', MemoryPrompt.Rules(Today).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
    }

    [Fact]
    public void The_rules_name_the_limits_the_code_enforces()
    {
        string rules = MemoryPrompt.Rules(Today);

        Assert.Contains($"at most {MemoryRules.MaxLength} characters", rules);
        Assert.Contains($"1-{MemoryRules.MaxKeys} keys", rules);
        Assert.Contains($"\"{MemoryRules.Title(MemoryCategory.Work)}\"", rules);
        Assert.Contains(MemoryPrompt.FactsHeading[2..], rules);
    }

    [Fact]
    public void The_rules_have_no_carriage_returns_whatever_the_checkout_did()
    {
        Assert.DoesNotContain('\r', MemoryPrompt.Rules(Today));
    }

    // ---- the facts ----

    [Fact]
    public void An_empty_memory_shows_every_category_as_empty()
    {
        Assert.Equal(
            "# Aiko memory: facts\n\n"
            + "## You (0/8)\n- (empty)\n\n"
            + "## How to work with me (0/12)\n- (empty)\n\n"
            + "## Projects (0/12)\n- (empty)\n\n"
            + "## Interests and taste (0/10)\n- (empty)",
            MemoryPrompt.Facts(MemoryFile.Empty));
    }

    [Fact]
    public void A_fact_shows_its_meaning_pin_and_keys_only_when_it_has_them()
    {
        var memory = new MemoryFile(
            20,
            [
                new MemoryFact("F12", MemoryCategory.Projects, "Makes Aiko") { Meaning = "A tray app", Keys = ["Aiko", "трей"], Seen = Today },
                new MemoryFact("F1", MemoryCategory.You, "Name is Sasha") { Meaning = "Call them Sasha", Pinned = true, Seen = Today },
                new MemoryFact("F2", MemoryCategory.You, "Learns Swift") { Keys = ["Swift"], Seen = Today },
                new MemoryFact("F3", MemoryCategory.Work, "Short answer first") { Seen = Today },
                new MemoryFact("F4", MemoryCategory.Work, "Plan before code") { Pinned = true, Seen = Today },
            ],
            ["{ \"id\": \"F9\", \"text\": \"set aside\" }"]);

        Assert.Equal(
            "# Aiko memory: facts\n\n"
            + "## You (2/8)\n"
            + "- [F1] Name is Sasha — Call them Sasha · pinned\n"
            + "- [F2] Learns Swift · keys: Swift\n\n"
            + "## How to work with me (2/12)\n"
            + "- [F3] Short answer first\n"
            + "- [F4] Plan before code · pinned\n\n"
            + "## Projects (1/12)\n"
            + "- [F12] Makes Aiko — A tray app · keys: Aiko, трей\n\n"
            + "## Interests and taste (0/10)\n- (empty)",
            MemoryPrompt.Facts(memory));
    }

    [Fact]
    public void The_facts_carry_no_dates()
    {
        var memory = new MemoryFile(2, [new MemoryFact("F1", MemoryCategory.You, "Learns Swift") { Seen = Today, Created = Today }], []);

        Assert.DoesNotContain("2026", MemoryPrompt.Facts(memory));
    }

    /// Every category at its quota, every fact at the length limit and with the longest line a
    /// fact of that kind can have: three keys of one letter where keys are allowed (each key
    /// costs a separator the limit does not count), a pin where they are not.
    [Theory]
    [InlineData(1)]
    [InlineData(99_958)]
    [InlineData(999_999_958)]
    public void A_full_memory_of_the_longest_facts_fits_one_hook_output(int firstNumber)
    {
        var memory = FullOfLongestFacts(firstNumber, pinEverything: false);

        Assert.Equal(42, memory.Facts.Count);
        Assert.All(memory.Facts, fact => Assert.Equal(MemoryRules.MaxLength, MemoryRules.LengthOf(fact)));
        Assert.InRange(MemoryPrompt.Facts(memory).Length, 9_000, HookOutputLimit);
    }

    [Fact]
    public void A_full_memory_of_pinned_facts_fits_too()
    {
        var memory = FullOfLongestFacts(99_958, pinEverything: true);

        Assert.All(memory.Facts, fact => Assert.True(fact.Pinned));
        Assert.InRange(MemoryPrompt.Facts(memory).Length, 9_000, HookOutputLimit);
    }

    [Fact]
    public void The_longest_facts_are_ones_the_tool_would_really_save()
    {
        var memory = MemoryFile.Empty with { NextId = 99_958 };
        foreach (var category in MemoryRules.Categories)
        {
            for (int i = 0; i < MemoryRules.Quota(category); i++)
            {
                var edit = MemoryEdits.Remember(
                    memory,
                    new MemoryRequest
                    {
                        Category = MemoryRules.NameOf(category),
                        Text = new string('t', 100),
                        Meaning = new string('m', category == MemoryCategory.Work ? 100 : 97),
                        Keys = ["A", "B", "C"],
                        Pin = category == MemoryCategory.Work,
                    },
                    Today);

                Assert.StartsWith("Saved", edit.Reply);
                memory = edit.Memory;
            }
        }

        Assert.Equal(MemoryPrompt.Facts(FullOfLongestFacts(99_958, pinEverything: false)), MemoryPrompt.Facts(memory));
    }

    private static MemoryFile FullOfLongestFacts(int firstNumber, bool pinEverything)
    {
        var facts = new List<MemoryFact>();
        foreach (var category in MemoryRules.Categories)
        {
            for (int i = 0; i < MemoryRules.Quota(category); i++)
            {
                bool keyed = !pinEverything && MemoryRules.TakesKeys(category, pinned: false);
                facts.Add(new MemoryFact(MemoryFact.IdFor(firstNumber + facts.Count), category, new string('t', 100))
                {
                    Meaning = new string('m', keyed ? 97 : 100),
                    Keys = keyed ? ["A", "B", "C"] : [],
                    Pinned = !keyed,
                    Seen = Today,
                    Created = Today,
                });
            }
        }

        return new MemoryFile(firstNumber + facts.Count, facts, []);
    }

    // ---- the note for a turn ----

    [Fact]
    public void The_note_tells_the_model_to_stay_silent_when_there_is_nothing_to_save()
    {
        Assert.StartsWith("Aiko memory check:", MemoryNudge.Text);
        Assert.Contains("call `remember` before you start the task", MemoryNudge.Text);
        Assert.EndsWith("ignore this note and do not mention it.", MemoryNudge.Text);
        Assert.DoesNotContain('\n', MemoryNudge.Text);
    }

    [Theory]
    [InlineData("у меня баг", true)]
    [InlineData("I like short answers", true)]
    [InlineData("запусти тесты", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void The_note_goes_to_messages_in_the_first_person(string? prompt, bool nudge)
    {
        Assert.Equal(nudge, MemoryNudge.Applies(prompt));
    }
}
