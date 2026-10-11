using Aiko.Core;

namespace Aiko.Core.Tests;

/// What the remember and forget tools and a message of the person do to the memory. Every branch
/// answers the model in words, so the words are checked too: the model acts on them.
public class MemoryEditsTests
{
    private static readonly DateOnly Today = new(2026, 10, 11);
    private static readonly DateOnly Yesterday = new(2026, 10, 10);
    private static readonly DateOnly LastMonth = new(2026, 9, 11);

    private static MemoryFact Fact(int number, MemoryCategory category, DateOnly seen, bool pinned = false) =>
        new(MemoryFact.IdFor(number), category, $"fact {number}") { Pinned = pinned, Seen = seen, Created = LastMonth };

    /// A category filled to its quota, ids from 1, all seen yesterday.
    private static MemoryFile Full(MemoryCategory category, bool pinned = false)
    {
        var facts = Enumerable.Range(1, MemoryRules.Quota(category))
            .Select(number => Fact(number, category, Yesterday, pinned))
            .ToList();

        return new MemoryFile(facts.Count + 1, facts, []);
    }

    private static MemoryRequest New(string category, string? text, params string[] keys) =>
        new() { Category = category, Text = text, Keys = keys };

    // ---- a new fact ----

    [Fact]
    public void A_new_fact_gets_the_next_id_and_today_as_both_days()
    {
        var memory = new MemoryFile(19, [Fact(4, MemoryCategory.You, Yesterday)], ["{}"]);

        var edit = MemoryEdits.Remember(
            memory,
            new MemoryRequest { Category = "projects", Text = "Makes the Aiko tray app", Meaning = "Knows WPF", Keys = ["Aiko"] },
            Today);

        var fact = edit.Memory.Facts[1];
        Assert.True(edit.Changed);
        Assert.Equal("Saved F19.", edit.Reply);
        Assert.Equal("F19", fact.Id);
        Assert.Equal(MemoryCategory.Projects, fact.Category);
        Assert.Equal("Makes the Aiko tray app", fact.Text);
        Assert.Equal("Knows WPF", fact.Meaning);
        Assert.False(fact.Pinned);
        Assert.Equal(["Aiko"], fact.Keys);
        Assert.Equal(Today, fact.Seen);
        Assert.Equal(Today, fact.Created);
        Assert.Equal(20, edit.Memory.NextId);
        Assert.Equal(["{}"], edit.Memory.SetAside);
    }

    [Fact]
    public void The_memory_that_came_in_is_not_changed()
    {
        var memory = new MemoryFile(2, [Fact(1, MemoryCategory.You, Yesterday)], []);

        MemoryEdits.Remember(memory, New("you", "Learns Swift"), Today);
        MemoryEdits.Forget(memory, "F1");

        Assert.Equal(2, memory.NextId);
        Assert.Equal("F1", Assert.Single(memory.Facts).Id);
    }

    [Theory]
    [InlineData("family")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unknown_category_is_refused(string? category)
    {
        var edit = MemoryEdits.Remember(MemoryFile.Empty, new MemoryRequest { Category = category, Text = "text" }, Today);

        Assert.False(edit.Changed);
        Assert.Same(MemoryFile.Empty, edit.Memory);
        Assert.Equal($"Unknown category {category}. Use one of: you, work, projects, interests. Nothing was saved.", edit.Reply);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public void A_new_fact_without_text_is_refused(string? text)
    {
        var edit = MemoryEdits.Remember(MemoryFile.Empty, New("you", text), Today);

        Assert.False(edit.Changed);
        Assert.Empty(edit.Memory.Facts);
        Assert.Equal("A new fact needs text. Nothing was saved.", edit.Reply);
    }

    [Fact]
    public void A_fact_is_kept_as_one_line()
    {
        var edit = MemoryEdits.Remember(
            MemoryFile.Empty,
            new MemoryRequest { Category = "you", Text = "  Learns Swift\n## Projects\r\n- [F1] ", Meaning = "a\tb" },
            Today);

        Assert.Equal("Learns Swift ## Projects - [F1]", edit.Memory.Facts[0].Text);
        Assert.Equal("a b", edit.Memory.Facts[0].Meaning);
    }

    // ---- too long ----

    [Fact]
    public void A_fact_that_is_too_long_is_not_saved_and_the_reply_gives_both_numbers()
    {
        var edit = MemoryEdits.Remember(
            MemoryFile.Empty,
            new MemoryRequest { Category = "you", Text = new string('a', 150), Meaning = new string('b', 46), Keys = ["Swift"] },
            Today);

        Assert.False(edit.Changed);
        Assert.Empty(edit.Memory.Facts);
        Assert.Equal(1, edit.Memory.FirstFreeNumber);
        Assert.Equal(
            "Too long: the fact, its meaning and keys are 201 characters together, and the limit is 200."
            + " Nothing was saved. Shorten it to one thought and call again.",
            edit.Reply);
    }

    [Fact]
    public void Keys_that_are_not_kept_do_not_count_against_the_length()
    {
        var work = MemoryEdits.Remember(MemoryFile.Empty, New("work", new string('a', 200), "Swift"), Today);
        var common = MemoryEdits.Remember(MemoryFile.Empty, New("you", new string('a', 200), "вопрос"), Today);

        Assert.True(work.Changed);
        Assert.True(common.Changed);
    }

    [Fact]
    public void The_length_is_checked_before_the_quota_so_nothing_is_forgotten_for_a_fact_that_is_refused()
    {
        var full = Full(MemoryCategory.You);

        var edit = MemoryEdits.Remember(full, New("you", new string('a', 201)), Today);

        Assert.False(edit.Changed);
        Assert.Same(full, edit.Memory);
        Assert.StartsWith("Too long", edit.Reply);
    }

    // ---- keys ----

    [Fact]
    public void Common_words_are_not_kept_as_keys_and_the_reply_names_them()
    {
        var edit = MemoryEdits.Remember(MemoryFile.Empty, New("you", "Learns Swift", "Swift", "вопрос", "коротко"), Today);

        Assert.True(edit.Changed);
        Assert.Equal(["Swift"], edit.Memory.Facts[0].Keys);
        Assert.Equal(
            "Saved F1. Keys not kept, they are common words: вопрос, коротко. Use names or terms, or no keys.",
            edit.Reply);
    }

    [Fact]
    public void A_work_habit_takes_no_keys()
    {
        var edit = MemoryEdits.Remember(MemoryFile.Empty, New("work", "Short answer first", "Swift", "вопрос"), Today);

        Assert.Empty(edit.Memory.Facts[0].Keys);
        Assert.Equal("Saved F1.", edit.Reply);
    }

    [Fact]
    public void A_pinned_fact_takes_no_keys()
    {
        var edit = MemoryEdits.Remember(
            MemoryFile.Empty,
            new MemoryRequest { Category = "you", Text = "Name is Sasha", Pin = true, Keys = ["Sasha"] },
            Today);

        Assert.True(edit.Memory.Facts[0].Pinned);
        Assert.Empty(edit.Memory.Facts[0].Keys);
    }

    // ---- a full category ----

    [Theory]
    [InlineData(MemoryCategory.You, 8)]
    [InlineData(MemoryCategory.Work, 12)]
    [InlineData(MemoryCategory.Projects, 12)]
    [InlineData(MemoryCategory.Interests, 10)]
    public void Each_category_has_its_quota(MemoryCategory category, int quota)
    {
        Assert.Equal(quota, MemoryRules.Quota(category));
        Assert.Equal(42, MemoryRules.Categories.Sum(MemoryRules.Quota));
    }

    [Fact]
    public void The_last_free_place_is_taken_without_forgetting_anything()
    {
        var almost = Full(MemoryCategory.Interests);
        almost = almost with { Facts = [.. almost.Facts.Skip(1)] };

        var edit = MemoryEdits.Remember(almost, New("interests", "Loves Elden Ring"), Today);

        Assert.Equal("Saved F11.", edit.Reply);
        Assert.Equal(10, edit.Memory.Facts.Count);
    }

    [Fact]
    public void A_full_category_forgets_the_fact_not_seen_for_the_longest_time()
    {
        var full = Full(MemoryCategory.Projects);
        full = full with { Facts = [.. full.Facts.Select(f => f.Id == "F7" ? f with { Seen = LastMonth } : f)] };

        var edit = MemoryEdits.Remember(full, New("projects", "Makes a card game"), Today);

        Assert.True(edit.Changed);
        Assert.Equal("Saved F13. Memory was full, forgot F7: fact 7", edit.Reply);
        Assert.Null(edit.Memory.Find("F7"));
        Assert.NotNull(edit.Memory.Find("F13"));
        Assert.Equal(12, edit.Memory.Facts.Count);
    }

    [Fact]
    public void Of_facts_seen_on_the_same_day_the_smaller_id_goes_first_counted_as_a_number()
    {
        var facts = new[] { 10, 9, 11, 12, 13, 14, 15, 16 }.Select(n => Fact(n, MemoryCategory.You, Yesterday)).ToList();

        var edit = MemoryEdits.Remember(new MemoryFile(17, facts, []), New("you", "Learns Swift"), Today);

        Assert.Contains("forgot F9: fact 9", edit.Reply);
    }

    [Fact]
    public void A_pinned_fact_is_passed_over_even_when_it_is_the_oldest()
    {
        var full = Full(MemoryCategory.You);
        full = full with
        {
            Facts = [.. full.Facts.Select(f => f.Id is "F1" or "F2" ? f with { Pinned = true, Seen = LastMonth } : f)],
        };

        var edit = MemoryEdits.Remember(full, New("you", "Learns Swift"), Today);

        Assert.Contains("forgot F3: fact 3", edit.Reply);
        Assert.NotNull(edit.Memory.Find("F1"));
        Assert.NotNull(edit.Memory.Find("F2"));
    }

    [Fact]
    public void Facts_of_other_categories_are_never_forgotten_to_make_room()
    {
        var full = Full(MemoryCategory.You);
        var withOld = full with { Facts = [Fact(40, MemoryCategory.Interests, LastMonth), .. full.Facts] };

        var edit = MemoryEdits.Remember(withOld, New("you", "Learns Swift"), Today);

        Assert.NotNull(edit.Memory.Find("F40"));
        Assert.Contains("forgot F1:", edit.Reply);
    }

    [Fact]
    public void The_reply_carries_both_notes_when_a_key_was_dropped_and_a_fact_forgotten()
    {
        var edit = MemoryEdits.Remember(Full(MemoryCategory.You), New("you", "Learns Swift", "вопрос"), Today);

        Assert.Equal(
            "Saved F9. Keys not kept, they are common words: вопрос. Use names or terms, or no keys."
            + " Memory was full, forgot F1: fact 1",
            edit.Reply);
    }

    [Fact]
    public void A_category_where_every_fact_is_pinned_saves_nothing_and_says_so()
    {
        var full = Full(MemoryCategory.You, pinned: true);

        var edit = MemoryEdits.Remember(full, New("you", "Learns Swift"), Today);

        Assert.False(edit.Changed);
        Assert.Same(full, edit.Memory);
        Assert.Equal(
            "\"You\" is full and every fact in it is pinned, so none can be forgotten to make room."
            + " Nothing was saved. Merge two close facts into one (remember with an id, then forget the other),"
            + " then call again.",
            edit.Reply);
    }

    [Fact]
    public void Full_work_habits_save_nothing_and_ask_the_model_to_merge_or_drop()
    {
        var full = Full(MemoryCategory.Work);

        var edit = MemoryEdits.Remember(full, New("work", "Plan before code"), Today);

        var lines = edit.Reply.Split('\n');
        Assert.False(edit.Changed);
        Assert.Same(full, edit.Memory);
        Assert.Equal(14, lines.Length);
        Assert.Equal("\"How to work with me\" is full (12 facts). Nothing was saved.", lines[0]);
        Assert.Equal("[F1] fact 1", lines[1]);
        Assert.Equal("[F12] fact 12", lines[12]);
        Assert.Equal(
            "Merge two close facts into one (remember with an id, then forget the other),"
            + " or forget the one that matters least. Then call again.",
            lines[13]);
    }

    [Fact]
    public void After_the_model_forgets_one_work_habit_the_new_one_is_saved()
    {
        var room = MemoryEdits.Forget(Full(MemoryCategory.Work), "F5").Memory;

        var edit = MemoryEdits.Remember(room, New("work", "Plan before code"), Today);

        Assert.Equal("Saved F13.", edit.Reply);
        Assert.Equal(12, edit.Memory.Facts.Count);
    }

    // ---- an existing fact ----

    private static MemoryFile OneProject() => new(
        4,
        [
            new MemoryFact("F3", MemoryCategory.Projects, "Makes Aiko")
            {
                Meaning = "A tray app",
                Keys = ["Aiko"],
                Seen = LastMonth,
                Created = LastMonth,
            },
        ],
        []);

    [Fact]
    public void An_unknown_id_changes_nothing()
    {
        var memory = OneProject();

        var edit = MemoryEdits.Remember(memory, new MemoryRequest { Id = "F9", Category = "you", Text = "new" }, Today);

        Assert.False(edit.Changed);
        Assert.Same(memory, edit.Memory);
        Assert.Equal("No fact F9. Nothing changed.", edit.Reply);
    }

    [Fact]
    public void An_id_alone_refreshes_the_fact_and_keeps_the_rest()
    {
        var edit = MemoryEdits.Remember(OneProject(), new MemoryRequest { Id = "f3", Category = "projects" }, Today);

        var fact = Assert.Single(edit.Memory.Facts);
        Assert.True(edit.Changed);
        Assert.Equal("Refreshed F3.", edit.Reply);
        Assert.Equal(Today, fact.Seen);
        Assert.Equal(LastMonth, fact.Created);
        Assert.Equal("Makes Aiko", fact.Text);
        Assert.Equal("A tray app", fact.Meaning);
        Assert.Equal(["Aiko"], fact.Keys);
        Assert.Equal(4, edit.Memory.NextId);
    }

    [Fact]
    public void The_fields_that_were_given_are_changed()
    {
        var edit = MemoryEdits.Remember(
            OneProject(),
            new MemoryRequest { Id = "F3", Text = "Makes Aiko, a tray app", Meaning = "Knows WPF and SwiftUI", Keys = ["Aiko", "трей", "WPF"] },
            Today);

        var fact = Assert.Single(edit.Memory.Facts);
        Assert.Equal("Makes Aiko, a tray app", fact.Text);
        Assert.Equal("Knows WPF and SwiftUI", fact.Meaning);
        Assert.Equal(["Aiko", "WPF"], fact.Keys);
        Assert.Equal("Refreshed F3. Keys not kept, they are common words: трей. Use names or terms, or no keys.", edit.Reply);
    }

    [Fact]
    public void A_fact_stays_in_its_category_whatever_the_call_says()
    {
        var edit = MemoryEdits.Remember(OneProject(), new MemoryRequest { Id = "F3", Category = "interests" }, Today);

        Assert.Equal(MemoryCategory.Projects, edit.Memory.Facts[0].Category);
    }

    [Fact]
    public void Pinning_a_fact_takes_its_keys_away()
    {
        var edit = MemoryEdits.Remember(OneProject(), new MemoryRequest { Id = "F3", Pin = true, Keys = ["Aiko"] }, Today);

        Assert.True(edit.Memory.Facts[0].Pinned);
        Assert.Empty(edit.Memory.Facts[0].Keys);
        Assert.Equal("Refreshed F3.", edit.Reply);
    }

    [Fact]
    public void A_fact_can_be_unpinned_and_given_keys_in_one_call()
    {
        var pinned = MemoryEdits.Remember(OneProject(), new MemoryRequest { Id = "F3", Pin = true }, Yesterday).Memory;

        var edit = MemoryEdits.Remember(pinned, new MemoryRequest { Id = "F3", Pin = false, Keys = ["Aiko"] }, Today);

        Assert.False(edit.Memory.Facts[0].Pinned);
        Assert.Equal(["Aiko"], edit.Memory.Facts[0].Keys);
    }

    [Fact]
    public void A_change_that_makes_the_fact_too_long_changes_nothing()
    {
        var memory = OneProject();

        var edit = MemoryEdits.Remember(memory, new MemoryRequest { Id = "F3", Meaning = new string('m', 187) }, Today);

        Assert.False(edit.Changed);
        Assert.Same(memory, edit.Memory);
        Assert.Equal(
            "Too long: the fact, its meaning and keys are 201 characters together, and the limit is 200."
            + " Nothing changed. Shorten it to one thought and call again.",
            edit.Reply);
    }

    [Fact]
    public void The_same_change_one_character_shorter_goes_through()
    {
        var edit = MemoryEdits.Remember(OneProject(), new MemoryRequest { Id = "F3", Meaning = new string('m', 186) }, Today);

        Assert.True(edit.Changed);
        Assert.Equal(200, MemoryRules.LengthOf(edit.Memory.Facts[0]));
    }

    // ---- forgetting ----

    [Fact]
    public void A_forgotten_fact_is_gone_and_its_id_is_not_given_again()
    {
        var forgotten = MemoryEdits.Forget(OneProject(), "F3");
        var next = MemoryEdits.Remember(forgotten.Memory, New("you", "Learns Swift"), Today);

        Assert.True(forgotten.Changed);
        Assert.Equal("Forgot F3.", forgotten.Reply);
        Assert.Empty(forgotten.Memory.Facts);
        Assert.Equal("F4", next.Memory.Facts[0].Id);
    }

    [Theory]
    [InlineData("F9")]
    [InlineData("")]
    [InlineData(null)]
    public void Forgetting_an_unknown_id_changes_nothing(string? id)
    {
        var memory = OneProject();

        var edit = MemoryEdits.Forget(memory, id);

        Assert.False(edit.Changed);
        Assert.Same(memory, edit.Memory);
        Assert.Equal($"No fact {id}. Nothing changed.", edit.Reply);
    }

    // ---- a key in the person's message ----

    private static MemoryFile Keyed() => new(
        5,
        [
            new MemoryFact("F1", MemoryCategory.You, "Learns Swift") { Keys = ["Swift"], Seen = LastMonth, Created = LastMonth },
            new MemoryFact("F2", MemoryCategory.You, "Works on a Mac") { Keys = ["Мак", "macOS"], Seen = LastMonth, Created = LastMonth },
            new MemoryFact("F3", MemoryCategory.You, "Writes C#") { Keys = ["C#"], Seen = Today, Created = LastMonth },
            new MemoryFact("F4", MemoryCategory.Work, "Short answer first") { Seen = LastMonth, Created = LastMonth },
        ],
        ["{}"]);

    [Fact]
    public void A_key_in_the_message_makes_its_fact_seen_today()
    {
        var touch = MemoryEdits.TouchByPrompt(Keyed(), "собери экран на SwiftUI и проверь на маке", Today);

        Assert.Equal(["F1", "F2"], touch.Touched);
        Assert.Equal(Today, touch.Memory.Find("F1")!.Seen);
        Assert.Equal(Today, touch.Memory.Find("F2")!.Seen);
        Assert.Equal(LastMonth, touch.Memory.Find("F4")!.Seen);
        Assert.Equal(LastMonth, touch.Memory.Find("F1")!.Created);
        Assert.Equal(["{}"], touch.Memory.SetAside);
    }

    [Fact]
    public void A_fact_already_seen_today_is_not_named_again()
    {
        var touch = MemoryEdits.TouchByPrompt(Keyed(), "перепиши это на C#", Today);

        Assert.Empty(touch.Touched);
    }

    [Fact]
    public void A_message_without_keys_gives_back_the_same_memory()
    {
        var memory = Keyed();

        var touch = MemoryEdits.TouchByPrompt(memory, "напиши макрос для swiftness", Today);

        Assert.Empty(touch.Touched);
        Assert.Same(memory, touch.Memory);
    }

    [Fact]
    public void A_touched_fact_is_no_longer_the_first_to_be_forgotten()
    {
        var full = Full(MemoryCategory.Projects);
        full = full with { Facts = [.. full.Facts.Select(f => f.Id == "F1" ? f with { Keys = ["Ember"] } : f)] };

        var touched = MemoryEdits.TouchByPrompt(full, "что там с Ember?", Today).Memory;
        var edit = MemoryEdits.Remember(touched, New("projects", "Makes a card game"), Today);

        Assert.Contains("forgot F2:", edit.Reply);
    }
}
