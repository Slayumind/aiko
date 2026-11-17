namespace Aiko.Core.Tests;

/// What a new version brings, from spec/cases/release-notes (D-397).
public class ReleaseNotesTests
{
    [Fact]
    public void The_list_holds_as_many_as_the_cases_say()
    {
        Assert.Equal(ReleaseNotes.MostTitles, SpecCases.Json("release-notes").GetProperty("most").GetInt32());
    }

    [Fact]
    public void Every_notes_case_gives_the_same_titles_in_both_languages()
    {
        foreach (var row in SpecCases.Json("release-notes").GetProperty("titles").EnumerateArray())
        {
            var notes = row.GetProperty("notes").GetString()!;
            Assert.Equal(Strings(row, "english"), ReleaseNotes.Titles(notes, russian: false));
            Assert.Equal(Strings(row, "russian"), ReleaseNotes.Titles(notes, russian: true));
        }
    }

    [Fact]
    public void Every_answer_of_GitHub_gives_the_same_titles()
    {
        foreach (var row in SpecCases.Json("release-notes").GetProperty("json").EnumerateArray())
        {
            var answer = row.GetProperty("answer").GetString()!;
            Assert.Equal(Strings(row, "titles"), ReleaseNotes.FromGitHubJson(answer, row.GetProperty("russian").GetBoolean()));
        }
    }

    private static string[] Strings(System.Text.Json.JsonElement row, string name) =>
        [.. row.GetProperty(name).EnumerateArray().Select(item => item.GetString()!)];
}
