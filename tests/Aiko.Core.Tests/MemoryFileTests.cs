using System.Text.Json;
using Aiko.Core;

namespace Aiko.Core.Tests;

/// memory.json is the person's own data. What is checked here: nothing in it is lost on the way
/// through Aiko, not even the entries Aiko cannot use.
public class MemoryFileTests
{
    private static readonly DateOnly Monday = new(2026, 10, 5);
    private static readonly DateOnly Friday = new(2026, 10, 9);

    private static MemoryFile TwoFacts() => new(
        19,
        [
            new MemoryFact("F3", MemoryCategory.You, "Учит Swift, \"я дописала\"")
            {
                Meaning = "Explain Swift ideas through C#",
                Keys = ["Swift", "C++"],
                Seen = Friday,
                Created = Monday,
            },
            new MemoryFact("F12", MemoryCategory.Work, "Short answer first") { Pinned = true, Seen = Monday, Created = Monday },
        ],
        []);

    // ---- the trip through the file ----

    [Fact]
    public void The_memory_survives_a_trip_through_the_file()
    {
        var read = MemoryFile.FromJson(TwoFacts().ToJson());

        Assert.False(read.Unreadable);
        Assert.Equal(19, read.Memory.NextId);
        Assert.Empty(read.Memory.SetAside);
        Assert.Equal(2, read.Memory.Facts.Count);

        var swift = read.Memory.Facts[0];
        Assert.Equal("F3", swift.Id);
        Assert.Equal(MemoryCategory.You, swift.Category);
        Assert.Equal("Учит Swift, \"я дописала\"", swift.Text);
        Assert.Equal("Explain Swift ideas through C#", swift.Meaning);
        Assert.False(swift.Pinned);
        Assert.Equal(["Swift", "C++"], swift.Keys);
        Assert.Equal(Friday, swift.Seen);
        Assert.Equal(Monday, swift.Created);

        var habit = read.Memory.Facts[1];
        Assert.Equal(MemoryCategory.Work, habit.Category);
        Assert.True(habit.Pinned);
        Assert.Empty(habit.Keys);
        Assert.Equal("", habit.Meaning);
    }

    [Fact]
    public void A_second_trip_writes_the_same_text()
    {
        string once = TwoFacts().ToJson();

        Assert.Equal(once, MemoryFile.FromJson(once).Memory.ToJson());
    }

    [Fact]
    public void The_file_is_indented_and_names_its_schema()
    {
        string json = TwoFacts().ToJson();

        Assert.StartsWith("{" + Environment.NewLine + "  \"schema\": 1," + Environment.NewLine + "  \"nextId\": 19,", json);
        Assert.Contains("\"category\": \"you\"", json);
        Assert.Contains("\"seen\": \"2026-10-09\"", json);
        Assert.Contains("\"created\": \"2026-10-05\"", json);
        Assert.Contains("\"setAside\": []", json);
        Assert.EndsWith("}" + Environment.NewLine, json);
    }

    [Fact]
    public void Cyrillic_and_plus_signs_stay_readable_in_the_file()
    {
        string json = TwoFacts().ToJson();

        Assert.Contains("Учит Swift, \\\"я дописала\\\"", json);
        Assert.Contains("\"C++\"", json);
        Assert.DoesNotContain("\\u", json);
    }

    [Fact]
    public void An_empty_memory_is_still_a_whole_file()
    {
        using var file = JsonDocument.Parse(MemoryFile.Empty.ToJson());

        Assert.Equal(1, file.RootElement.GetProperty("schema").GetInt32());
        Assert.Equal(1, file.RootElement.GetProperty("nextId").GetInt32());
        Assert.Equal(0, file.RootElement.GetProperty("facts").GetArrayLength());
        Assert.Equal(0, file.RootElement.GetProperty("setAside").GetArrayLength());
    }

    // ---- no file, and a file that is not the memory ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \r\n ")]
    public void No_file_yet_is_an_empty_memory_with_nothing_to_keep_aside(string? json)
    {
        var read = MemoryFile.FromJson(json);

        Assert.False(read.Unreadable);
        Assert.Empty(read.Memory.Facts);
        Assert.Equal(1, read.Memory.NextId);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ \"facts\": [")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("{ \"facts\": \"none\" }")]
    [InlineData("{ \"facts\": { \"F1\": {} } }")]
    public void A_file_that_is_not_the_memory_reads_as_empty_and_is_marked(string json)
    {
        var read = MemoryFile.FromJson(json);

        Assert.True(read.Unreadable);
        Assert.Empty(read.Memory.Facts);
        Assert.Empty(read.Memory.SetAside);
        Assert.Equal(1, read.Memory.NextId);
    }

    [Theory]
    [InlineData("{ \"schema\": 2, \"nextId\": 5, \"facts\": [] }")]
    [InlineData("{ \"schema\": 99999999999, \"facts\": [] }")]
    public void A_file_from_a_newer_Aiko_is_marked_and_not_read_by_halves(string json)
    {
        var read = MemoryFile.FromJson(json);

        Assert.True(read.Unreadable);
        Assert.Empty(read.Memory.Facts);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"schema\": 1 }")]
    [InlineData("{ \"facts\": null }")]
    public void An_object_without_facts_is_an_empty_memory(string json)
    {
        var read = MemoryFile.FromJson(json);

        Assert.False(read.Unreadable);
        Assert.Empty(read.Memory.Facts);
    }

    // ---- fields that are missing ----

    [Fact]
    public void A_fact_with_only_what_is_needed_gets_the_defaults()
    {
        var fact = Assert.Single(MemoryFile.FromJson("""
            { "facts": [ { "id": "F4", "category": "Interests", "text": "Loves Slay the Spire", "seen": "2026-10-09" } ] }
            """).Memory.Facts);

        Assert.Equal(MemoryCategory.Interests, fact.Category);
        Assert.Equal("", fact.Meaning);
        Assert.False(fact.Pinned);
        Assert.Empty(fact.Keys);
        Assert.Equal(Friday, fact.Created);
    }

    [Fact]
    public void Fields_Aiko_does_not_know_are_dropped_from_a_good_fact()
    {
        var read = MemoryFile.FromJson("""
            { "next_id": 7, "facts": [
              { "id": "F4", "category": "projects", "text": "Makes Aiko", "scope": "personal", "refreshed": 3, "seen": "2026-10-09" }
            ] }
            """);

        Assert.Single(read.Memory.Facts);
        Assert.Empty(read.Memory.SetAside);
        Assert.DoesNotContain("scope", read.Memory.ToJson());
        Assert.DoesNotContain("refreshed", read.Memory.ToJson());
    }

    [Fact]
    public void More_keys_than_a_fact_may_have_are_cut_to_three()
    {
        var fact = Assert.Single(MemoryFile.FromJson("""
            { "facts": [ { "id": "F1", "category": "you", "text": "t", "keys": ["A", "B", "C", "D"], "seen": "2026-10-09" } ] }
            """).Memory.Facts);

        Assert.Equal(["A", "B", "C"], fact.Keys);
    }

    // ---- entries Aiko cannot use ----

    [Theory]
    [InlineData("""{ "category": "you", "text": "no id", "seen": "2026-10-09" }""")]
    [InlineData("""{ "id": "7", "category": "you", "text": "id without F", "seen": "2026-10-09" }""")]
    [InlineData("""{ "id": "F0", "category": "you", "text": "id zero", "seen": "2026-10-09" }""")]
    [InlineData("""{ "id": "F07", "category": "you", "text": "a second way to write 7", "seen": "2026-10-09" }""")]
    [InlineData("""{ "id": 7, "category": "you", "text": "id is a number", "seen": "2026-10-09" }""")]
    [InlineData("""{ "id": "F7", "category": "family", "text": "unknown category", "seen": "2026-10-09" }""")]
    [InlineData("""{ "id": "F7", "text": "no category", "seen": "2026-10-09" }""")]
    [InlineData("""{ "id": "F7", "category": "you", "text": "   ", "seen": "2026-10-09" }""")]
    [InlineData("""{ "id": "F7", "category": "you", "seen": "2026-10-09" }""")]
    [InlineData("""{ "id": "F7", "category": "you", "text": ["a list"], "seen": "2026-10-09" }""")]
    [InlineData("""{ "id": "F7", "category": "you", "text": "no day" }""")]
    [InlineData("""{ "id": "F7", "category": "you", "text": "a day that is not one", "seen": "09.10.2026" }""")]
    [InlineData("""{ "id": "F7", "category": "you", "text": "pinned is a word", "pinned": "yes", "seen": "2026-10-09" }""")]
    [InlineData("""{ "id": "F7", "category": "you", "text": "keys is a word", "keys": "Swift", "seen": "2026-10-09" }""")]
    [InlineData("""{ "id": "F7", "category": "you", "text": "a key is a number", "keys": ["Swift", 5], "seen": "2026-10-09" }""")]
    [InlineData("""{ "id": "F7", "category": "you", "text": "meaning is a number", "meaning": 5, "seen": "2026-10-09" }""")]
    [InlineData("""{ "id": "F7", "category": "you", "text": "created is not a day", "seen": "2026-10-09", "created": "soon" }""")]
    [InlineData("\"just a string\"")]
    [InlineData("17")]
    [InlineData("null")]
    public void An_entry_that_cannot_be_used_is_set_aside_as_it_was_written(string entry)
    {
        var read = MemoryFile.FromJson("{ \"facts\": [ " + entry + " ] }");

        Assert.False(read.Unreadable);
        Assert.Empty(read.Memory.Facts);
        Assert.Equal([entry], read.Memory.SetAside);
    }

    [Fact]
    public void The_good_facts_around_a_bad_entry_are_kept()
    {
        var read = MemoryFile.FromJson("""
            { "facts": [
              { "id": "F1", "category": "you", "text": "first", "seen": "2026-10-09" },
              { "id": "F2", "category": "family", "text": "моя сестра", "extra": { "deep": [1, 2] } },
              { "id": "F3", "category": "work", "text": "third", "seen": "2026-10-09" }
            ] }
            """);

        Assert.Equal(["F1", "F3"], read.Memory.Facts.Select(f => f.Id));
        Assert.Equal(
            ["""{ "id": "F2", "category": "family", "text": "моя сестра", "extra": { "deep": [1, 2] } }"""],
            read.Memory.SetAside);
    }

    [Fact]
    public void A_second_fact_with_an_id_already_taken_is_set_aside()
    {
        var read = MemoryFile.FromJson("""
            { "facts": [
              { "id": "F1", "category": "you", "text": "first", "seen": "2026-10-09" },
              { "id": "F1", "category": "you", "text": "second", "seen": "2026-10-09" }
            ] }
            """);

        Assert.Equal("first", Assert.Single(read.Memory.Facts).Text);
        Assert.Contains("second", Assert.Single(read.Memory.SetAside));
    }

    [Fact]
    public void What_was_set_aside_survives_every_later_trip_through_the_file()
    {
        const string Broken = """{ "id": "F2", "category": "family", "text": "моя сестра + \"кот\"", "n": 1.50 }""";
        var first = MemoryFile.FromJson("""
            { "schema": 1, "nextId": 3, "facts": [
              { "id": "F1", "category": "you", "text": "first", "seen": "2026-10-09" },
            """ + Broken + "] }");

        string written = first.Memory.ToJson();
        var second = MemoryFile.FromJson(written);
        var third = MemoryFile.FromJson(second.Memory.ToJson());

        Assert.Equal([Broken], first.Memory.SetAside);
        Assert.Contains(Broken, written);
        Assert.Equal([Broken], second.Memory.SetAside);
        Assert.Equal([Broken], third.Memory.SetAside);
        Assert.Single(third.Memory.Facts);
    }

    [Fact]
    public void An_entry_in_setAside_is_never_read_back_as_a_fact()
    {
        var read = MemoryFile.FromJson("""
            { "facts": [], "setAside": [ { "id": "F5", "category": "you", "text": "looks fine", "seen": "2026-10-09" } ] }
            """);

        Assert.Empty(read.Memory.Facts);
        Assert.Single(read.Memory.SetAside);
    }

    // ---- the number of the next fact ----

    [Theory]
    [InlineData("""{ "nextId": 19, "facts": [ { "id": "F4", "category": "you", "text": "t", "seen": "2026-10-09" } ] }""", 19)]
    [InlineData("""{ "nextId": 2, "facts": [ { "id": "F4", "category": "you", "text": "t", "seen": "2026-10-09" } ] }""", 5)]
    [InlineData("""{ "facts": [ { "id": "F10", "category": "you", "text": "t", "seen": "2026-10-09" } ] }""", 11)]
    [InlineData("""{ "nextId": "many", "facts": [ { "id": "F4", "category": "you", "text": "t", "seen": "2026-10-09" } ] }""", 5)]
    [InlineData("""{ "nextId": -3, "facts": [] }""", 1)]
    [InlineData("""{ "nextId": 8, "facts": [] }""", 8)]
    public void The_next_id_is_never_one_a_fact_already_has(string json, int next)
    {
        Assert.Equal(next, MemoryFile.FromJson(json).Memory.NextId);
    }

    [Fact]
    public void A_memory_built_with_a_small_next_id_is_written_with_a_free_one()
    {
        var memory = TwoFacts() with { NextId = 1 };

        Assert.Equal(13, memory.FirstFreeNumber);
        Assert.Contains("\"nextId\": 13", memory.ToJson());
    }

    // ---- ids ----

    [Theory]
    [InlineData("F1", 1)]
    [InlineData("F12", 12)]
    [InlineData("F0", null)]
    [InlineData("F-1", null)]
    [InlineData("F 1", null)]
    [InlineData("F01", null)]
    [InlineData("f1", null)]
    [InlineData("F", null)]
    [InlineData("12", null)]
    [InlineData("F99999999999", null)]
    [InlineData(null, null)]
    public void An_id_is_F_and_a_number_from_one_up(string? id, int? number)
    {
        Assert.Equal(number, MemoryFact.NumberOf(id));
    }

    [Fact]
    public void A_fact_is_found_by_its_id_however_the_model_typed_it()
    {
        Assert.Equal("F12", TwoFacts().Find(" f12 ")?.Id);
        Assert.Null(TwoFacts().Find("F1"));
        Assert.Null(TwoFacts().Find(null));
    }
}
