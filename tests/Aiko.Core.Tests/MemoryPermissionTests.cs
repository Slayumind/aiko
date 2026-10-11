using System.Text.Json;
using Aiko.Core;

namespace Aiko.Core.Tests;

/// The one permission rule Aiko writes into a person's settings.json for the memory tools (D-279).
/// It is their file: only our rule may move, and what they wrote must come through untouched.
public class MemoryPermissionTests
{
    private const string Folder = @"C:\Users\someone\.claude";
    private const string Rule = "mcp__plugin_aiko-persona_memory";

    private static string Set(string json, bool allowed) =>
        SettingsJsonPatch.TrySetMemoryPermission(json, allowed, out var patched) ? patched : "(unchanged)";

    private static string[] Allow(string json) =>
        JsonDocument.Parse(json).RootElement.GetProperty("permissions").GetProperty("allow")
            .EnumerateArray().Select(rule => rule.GetString()!).ToArray();

    [Fact]
    public void The_rule_is_added_to_a_file_without_permissions()
    {
        var patched = Set("""{ "model": "opus" }""", allowed: true);

        Assert.Equal([Rule], Allow(patched));
        Assert.Equal("opus", JsonDocument.Parse(patched).RootElement.GetProperty("model").GetString());
        Assert.True(SettingsJsonPatch.HasMemoryPermission(patched));
    }

    [Fact]
    public void The_rule_joins_the_rules_the_person_already_has()
    {
        var patched = Set("""{ "permissions": { "allow": ["Bash(git status)"], "deny": ["Read(.env)"] } }""", allowed: true);

        Assert.Equal(["Bash(git status)", Rule], Allow(patched));
        Assert.Equal(
            "Read(.env)",
            JsonDocument.Parse(patched).RootElement.GetProperty("permissions").GetProperty("deny")[0].GetString());
    }

    [Fact]
    public void A_rule_that_is_there_is_not_added_twice()
    {
        var once = Set("{}", allowed: true);

        Assert.Equal("(unchanged)", Set(once, allowed: true));
    }

    [Theory]
    [InlineData("""{ "permissions": "all" }""")]
    [InlineData("""{ "permissions": { "allow": "Bash" } }""")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void A_file_of_a_shape_we_do_not_know_is_left_alone(string json)
    {
        Assert.Equal("(unchanged)", Set(json, allowed: true));
        Assert.Equal("(unchanged)", Set(json, allowed: false));
    }

    [Fact]
    public void Taking_the_rule_out_keeps_the_other_rules()
    {
        var with = Set("""{ "permissions": { "allow": ["Bash(git status)"] } }""", allowed: true);

        var without = Set(with, allowed: false);

        Assert.Equal(["Bash(git status)"], Allow(without));
        Assert.False(SettingsJsonPatch.HasMemoryPermission(without));
    }

    [Fact]
    public void Taking_the_rule_out_leaves_no_empty_keys_behind()
    {
        var without = Set(Set("""{ "model": "opus" }""", allowed: true), allowed: false);

        var root = JsonDocument.Parse(without).RootElement;
        Assert.False(root.TryGetProperty("permissions", out _));
        Assert.Equal("opus", root.GetProperty("model").GetString());
    }

    [Fact]
    public void Taking_the_rule_out_keeps_permissions_that_still_say_something()
    {
        var with = Set("""{ "permissions": { "deny": ["Read(.env)"] } }""", allowed: true);

        var permissions = JsonDocument.Parse(Set(with, allowed: false)).RootElement.GetProperty("permissions");

        Assert.False(permissions.TryGetProperty("allow", out _));
        Assert.Equal(1, permissions.GetProperty("deny").GetArrayLength());
    }

    [Fact]
    public void Nothing_to_take_out_changes_nothing()
    {
        Assert.Equal("(unchanged)", Set("""{ "permissions": { "allow": ["Bash(git status)"] } }""", allowed: false));
    }

    [Fact]
    public void The_rule_counts_as_an_entry_of_aiko()
    {
        var with = Set("{}", allowed: true);

        Assert.True(SettingsJsonPatch.HasAnyAikoEntries(PlatformConventions.Windows, with));
        Assert.False(SettingsJsonPatch.HasAnyAikoEntries(PlatformConventions.Windows, """{ "permissions": { "allow": ["Bash"] } }"""));
    }

    [Fact]
    public void The_editor_backs_the_file_up_before_the_first_change_and_writes_once()
    {
        var files = new FakeFiles().With(ClaudeSettingsEditor.PathIn(Folder), """{ "model": "opus" }""");
        var editor = new ClaudeSettingsEditor(files, PlatformConventions.Windows);

        var first = editor.SetMemoryPermission(Folder, allowed: true);
        var second = editor.SetMemoryPermission(Folder, allowed: true);

        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Equal("""{ "model": "opus" }""", files.Read(ClaudeSettingsEditor.BackupPathIn(Folder)));
        Assert.Equal([Rule], Allow(files.Read(ClaudeSettingsEditor.PathIn(Folder))));
    }

    [Fact]
    public void Removing_aiko_from_a_folder_takes_the_rule_with_it()
    {
        var files = new FakeFiles().With(ClaudeSettingsEditor.PathIn(Folder), """{ "model": "opus" }""");
        var editor = new ClaudeSettingsEditor(files, PlatformConventions.Windows);
        editor.SetMemoryPermission(Folder, allowed: true);

        var outcome = editor.Remove(Folder);

        Assert.True(outcome.Changed);
        Assert.False(SettingsJsonPatch.HasMemoryPermission(files.Read(ClaudeSettingsEditor.PathIn(Folder))));
        Assert.False(files.Has(ClaudeSettingsEditor.BackupPathIn(Folder)));
    }
}
