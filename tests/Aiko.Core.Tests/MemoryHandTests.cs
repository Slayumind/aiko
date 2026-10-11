using Aiko.Core;

namespace Aiko.Core.Tests;

/// What the person does to the memory in the settings window, and what the window shows of it.
/// Nothing here may happen behind the person's back: a full category refuses instead of dropping
/// an old fact, and every refusal has a name the window can put into words.
public class MemoryHandTests
{
    private static readonly DateOnly Today = new(2026, 10, 11);
    private static readonly DateOnly LastMonth = new(2026, 9, 11);

    private static MemoryFact Fact(int number, MemoryCategory category, string text = "", params string[] keys) =>
        new(MemoryFact.IdFor(number), category, text.Length > 0 ? text : $"fact {number}")
        {
            Keys = keys,
            Seen = LastMonth,
            Created = LastMonth,
        };

    private static MemoryFile Full(MemoryCategory category)
    {
        var facts = Enumerable.Range(1, MemoryRules.Quota(category)).Select(n => Fact(n, category)).ToList();
        return new MemoryFile(facts.Count + 1, facts, []);
    }

    // ---- a new fact ----

    [Fact]
    public void A_fact_added_by_hand_is_one_line_has_no_keys_and_is_seen_today()
    {
        var memory = new MemoryFile(19, [Fact(4, MemoryCategory.You)], ["{}"]);

        var edit = MemoryHand.Add(memory, MemoryCategory.Work, "  Сначала \n короткий ответ ", " Потом\tподробности ", Today);

        var fact = edit.Memory.Facts[1];
        Assert.Equal(MemoryHandOutcome.Done, edit.Outcome);
        Assert.True(edit.Changed);
        Assert.Equal("F19", fact.Id);
        Assert.Equal("Сначала короткий ответ", fact.Text);
        Assert.Equal("Потом подробности", fact.Meaning);
        Assert.Empty(fact.Keys);
        Assert.False(fact.Pinned);
        Assert.Equal(Today, fact.Seen);
        Assert.Equal(Today, fact.Created);
        Assert.Equal(20, edit.Memory.NextId);
        Assert.Equal(["{}"], edit.Memory.SetAside);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public void A_fact_without_text_is_not_added(string? text)
    {
        var edit = MemoryHand.Add(MemoryFile.Empty, MemoryCategory.You, text, "meaning", Today);

        Assert.Equal(MemoryHandOutcome.Empty, edit.Outcome);
        Assert.False(edit.Changed);
        Assert.Same(MemoryFile.Empty, edit.Memory);
    }

    [Fact]
    public void A_fact_over_the_limit_is_not_added_and_one_at_the_limit_is()
    {
        var atTheLimit = MemoryHand.Add(MemoryFile.Empty, MemoryCategory.You, new string('a', 150), new string('b', 50), Today);
        var over = MemoryHand.Add(MemoryFile.Empty, MemoryCategory.You, new string('a', 150), new string('b', 51), Today);

        Assert.Equal(MemoryHandOutcome.Done, atTheLimit.Outcome);
        Assert.Equal(MemoryHandOutcome.TooLong, over.Outcome);
        Assert.Empty(over.Memory.Facts);
    }

    [Theory]
    [InlineData(MemoryCategory.You)]
    [InlineData(MemoryCategory.Work)]
    [InlineData(MemoryCategory.Projects)]
    [InlineData(MemoryCategory.Interests)]
    public void A_full_category_refuses_and_drops_nothing(MemoryCategory category)
    {
        var memory = Full(category);

        var edit = MemoryHand.Add(memory, category, "One more", "", Today);

        Assert.Equal(MemoryHandOutcome.Full, edit.Outcome);
        Assert.Same(memory, edit.Memory);
    }

    [Fact]
    public void A_full_category_does_not_close_the_others()
    {
        var edit = MemoryHand.Add(Full(MemoryCategory.You), MemoryCategory.Work, "One more", "", Today);

        Assert.Equal(MemoryHandOutcome.Done, edit.Outcome);
    }

    // ---- an edit ----

    [Fact]
    public void An_edit_changes_the_words_and_keeps_the_rest()
    {
        var memory = new MemoryFile(3, [Fact(1, MemoryCategory.Projects, "Makes Aiko", "Aiko") with { Meaning = "Own product" }], []);

        var edit = MemoryHand.Edit(memory, "F1", "Makes Aiko, a tray app", "Own product");

        var fact = edit.Memory.Facts[0];
        Assert.Equal(MemoryHandOutcome.Done, edit.Outcome);
        Assert.Equal("Makes Aiko, a tray app", fact.Text);
        Assert.Equal(["Aiko"], fact.Keys);
        Assert.Equal(LastMonth, fact.Seen);
        Assert.Equal(LastMonth, fact.Created);
    }

    [Fact]
    public void Clearing_the_meaning_field_clears_the_meaning()
    {
        var memory = new MemoryFile(2, [Fact(1, MemoryCategory.You, "Learns Swift") with { Meaning = "Explain through C#" }], []);

        var edit = MemoryHand.Edit(memory, "F1", "Learns Swift", "  ");

        Assert.Equal("", edit.Memory.Facts[0].Meaning);
    }

    [Fact]
    public void An_edit_that_changes_nothing_does_not_write()
    {
        var memory = new MemoryFile(2, [Fact(1, MemoryCategory.You, "Learns Swift")], []);

        var edit = MemoryHand.Edit(memory, "F1", " Learns  Swift ", "");

        Assert.Equal(MemoryHandOutcome.NothingToDo, edit.Outcome);
        Assert.False(edit.Changed);
    }

    [Fact]
    public void An_edit_counts_the_keys_of_the_fact_against_the_limit()
    {
        var memory = new MemoryFile(2, [Fact(1, MemoryCategory.Projects, "Makes Aiko", "Code Crawler")], []);

        var fits = MemoryHand.Edit(memory, "F1", new string('a', MemoryRules.MaxLength - 12), "");
        var over = MemoryHand.Edit(memory, "F1", new string('a', MemoryRules.MaxLength - 11), "");

        Assert.Equal(MemoryHandOutcome.Done, fits.Outcome);
        Assert.Equal(MemoryHandOutcome.TooLong, over.Outcome);
    }

    [Fact]
    public void An_edit_can_shorten_a_fact_that_was_too_long()
    {
        var memory = new MemoryFile(2, [Fact(1, MemoryCategory.Work, new string('a', 312))], []);

        var edit = MemoryHand.Edit(memory, "F1", "Short now", "");

        Assert.Equal(MemoryHandOutcome.Done, edit.Outcome);
        Assert.False(MemoryPage.From(edit.Memory).Sections[1].Facts[0].TooLong);
    }

    [Fact]
    public void An_edit_to_no_text_is_refused()
    {
        var memory = new MemoryFile(2, [Fact(1, MemoryCategory.You)], []);

        Assert.Equal(MemoryHandOutcome.Empty, MemoryHand.Edit(memory, "F1", " ", "meaning").Outcome);
    }

    // ---- pin, remove, erase ----

    [Fact]
    public void A_pinned_fact_loses_its_keys_and_does_not_get_them_back()
    {
        var memory = new MemoryFile(2, [Fact(1, MemoryCategory.Projects, "Makes Aiko", "Aiko")], []);

        var pinned = MemoryHand.Pin(memory, "F1", true);
        var unpinned = MemoryHand.Pin(pinned.Memory, "F1", false);

        Assert.True(pinned.Memory.Facts[0].Pinned);
        Assert.Empty(pinned.Memory.Facts[0].Keys);
        Assert.False(unpinned.Memory.Facts[0].Pinned);
        Assert.Empty(unpinned.Memory.Facts[0].Keys);
    }

    [Fact]
    public void Pinning_what_is_pinned_does_not_write()
    {
        var memory = new MemoryFile(2, [Fact(1, MemoryCategory.You) with { Pinned = true }], []);

        Assert.Equal(MemoryHandOutcome.NothingToDo, MemoryHand.Pin(memory, "F1", true).Outcome);
    }

    [Fact]
    public void Removing_a_fact_keeps_the_others_and_the_numbers()
    {
        var memory = new MemoryFile(3, [Fact(1, MemoryCategory.You), Fact(2, MemoryCategory.Work)], []);

        var edit = MemoryHand.Remove(memory, "F1");

        Assert.Equal(["F2"], edit.Memory.Facts.Select(f => f.Id));
        Assert.Equal(3, edit.Memory.FirstFreeNumber);
    }

    [Fact]
    public void A_fact_that_is_gone_is_named_as_gone_by_every_action()
    {
        var memory = new MemoryFile(2, [Fact(1, MemoryCategory.You)], []);

        Assert.Equal(MemoryHandOutcome.NoFact, MemoryHand.Edit(memory, "F9", "text", "").Outcome);
        Assert.Equal(MemoryHandOutcome.NoFact, MemoryHand.Pin(memory, "F9", true).Outcome);
        Assert.Equal(MemoryHandOutcome.NoFact, MemoryHand.Remove(memory, "F9").Outcome);
    }

    [Fact]
    public void Erasing_takes_the_facts_and_the_records_kept_aside_but_not_the_numbers()
    {
        var memory = new MemoryFile(8, [Fact(1, MemoryCategory.You), Fact(7, MemoryCategory.Work)], ["{}"]);

        var edit = MemoryHand.EraseAll(memory);
        var next = MemoryHand.Add(edit.Memory, MemoryCategory.You, "A new start", "", Today);

        Assert.Equal(MemoryHandOutcome.Done, edit.Outcome);
        Assert.Empty(edit.Memory.Facts);
        Assert.Empty(edit.Memory.SetAside);
        Assert.Equal("F8", next.Memory.Facts[0].Id);
    }

    [Fact]
    public void Erasing_an_empty_memory_does_not_write()
    {
        Assert.Equal(MemoryHandOutcome.NothingToDo, MemoryHand.EraseAll(MemoryFile.Empty).Outcome);
    }

    // ---- what the window shows ----

    [Fact]
    public void The_page_has_the_four_categories_in_order_with_counts_and_quotas()
    {
        var memory = new MemoryFile(4, [Fact(1, MemoryCategory.Work), Fact(2, MemoryCategory.You), Fact(3, MemoryCategory.Work)], ["{}", "1"]);

        var page = MemoryPage.From(memory);

        Assert.Equal(
            [MemoryCategory.You, MemoryCategory.Work, MemoryCategory.Projects, MemoryCategory.Interests],
            page.Sections.Select(s => s.Category));
        Assert.Equal([1, 2, 0, 0], page.Sections.Select(s => s.Count));
        Assert.Equal([8, 12, 12, 10], page.Sections.Select(s => s.Quota));
        Assert.Equal(3, page.Count);
        Assert.Equal(42, page.Capacity);
        Assert.Equal(2, page.SetAside);
        Assert.False(page.Empty);
    }

    [Fact]
    public void An_empty_memory_is_empty_and_one_with_only_records_aside_is_not()
    {
        Assert.True(MemoryPage.From(MemoryFile.Empty).Empty);
        Assert.False(MemoryPage.From(new MemoryFile(1, [], ["{}"])).Empty);
    }

    [Fact]
    public void A_fact_over_the_limit_is_marked_and_counted()
    {
        var memory = new MemoryFile(3, [Fact(1, MemoryCategory.Work, new string('a', 200)), Fact(2, MemoryCategory.Work, new string('a', 312))], []);

        var page = MemoryPage.From(memory);

        Assert.False(page.Sections[1].Facts[0].TooLong);
        Assert.True(page.Sections[1].Facts[1].TooLong);
        Assert.Equal(312, page.Sections[1].Facts[1].Length);
        Assert.Equal(1, page.TooLong);
    }

    [Fact]
    public void A_category_at_its_quota_is_full()
    {
        var page = MemoryPage.From(Full(MemoryCategory.You));

        Assert.True(page.Sections[0].Full);
        Assert.False(page.Sections[1].Full);
    }

    [Fact]
    public void The_count_under_the_field_is_the_count_the_save_will_check()
    {
        var fact = Fact(1, MemoryCategory.Projects, "Makes Aiko", "Aiko", "C#");

        Assert.Equal(0, MemoryPage.LengthOf(null, null));
        Assert.Equal(9, MemoryPage.LengthOf("  one \n two ", " ab "));
        Assert.Equal(9 + 6, MemoryPage.LengthOf("one two", "ab", fact));
        Assert.Equal(MemoryRules.LengthOf(fact), MemoryPage.LengthOf(fact.Text, fact.Meaning, fact));
    }
}
