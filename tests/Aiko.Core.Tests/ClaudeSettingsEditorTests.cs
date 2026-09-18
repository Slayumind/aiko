using Aiko.Core;

namespace Aiko.Core.Tests;

/// Writing to a file that belongs to another program.
///
/// Removal walks every Claude Code folder on the machine and rewrites what it finds, and until now
/// none of it had a test. These cover the paths that end badly: no backup, a backup already there,
/// a file the user changed after installing, and a folder Aiko cannot read.
public class ClaudeSettingsEditorTests
{
    private const string Folder = @"C:\Users\someone\.claude";
    private const string Bridge = @"C:\Users\someone\AppData\Local\Aiko\current\Aiko.Bridge.exe";
    private const string Moved = @"D:\Aiko\Aiko.Bridge.exe";

    private static string Settings => ClaudeSettingsEditor.PathIn(Folder);

    private static string Backup => ClaudeSettingsEditor.BackupPathIn(Folder);

    private static string Command(string path = Bridge) => BridgeCommand.For(path);

    [Fact]
    public void A_copy_is_made_before_the_first_change()
    {
        var original = """{ "statusLine": { "type": "command", "command": "mine.sh" } }""";
        var files = new FakeFiles().With(Settings, original);

        new ClaudeSettingsEditor(files, Windows).Add(Folder, Command());

        Assert.True(files.Has(Backup));
        Assert.Equal(original, files.Read(Backup));
    }

    [Fact]
    public void A_copy_that_already_exists_is_left_alone()
    {
        // A second copy would save our own edit and lose what the user had.
        var files = new FakeFiles()
            .With(Settings, """{ "statusLine": { "type": "command", "command": "new.sh" } }""")
            .With(Backup, """{ "statusLine": { "type": "command", "command": "original.sh" } }""");

        new ClaudeSettingsEditor(files, Windows).Add(Folder, Command());

        Assert.Contains("original.sh", files.Read(Backup));
    }

    [Fact]
    public void A_folder_with_no_settings_file_gets_one()
    {
        var files = new FakeFiles();

        var outcome = new ClaudeSettingsEditor(files, Windows).Add(Folder, Command());

        Assert.True(outcome.Changed);
        Assert.True(files.Has(Settings));
        // Nothing existed, so there was nothing worth copying.
        Assert.False(files.Has(Backup));
    }

    [Fact]
    public void The_file_is_written_through_a_temporary_one()
    {
        // A half written settings file breaks Claude Code, not just Aiko.
        var files = new FakeFiles();

        new ClaudeSettingsEditor(files, Windows).Add(Folder, Command());

        Assert.Contains(files.Writes, w => w.EndsWith(".aiko.tmp", StringComparison.Ordinal));
        Assert.DoesNotContain(files.Writes, w => w == Settings);
    }

    [Fact]
    public void Adding_our_line_twice_writes_nothing_the_second_time()
    {
        var files = new FakeFiles();
        var editor = new ClaudeSettingsEditor(files, Windows);
        editor.Add(Folder, Command());
        files.Writes.Clear();

        var outcome = editor.Add(Folder, Command());

        Assert.False(outcome.Changed);
        Assert.Empty(files.Writes);
    }

    [Fact]
    public void Removal_puts_back_the_line_the_user_had()
    {
        var files = new FakeFiles()
            .With(Settings, """{ "statusLine": { "type": "command", "command": "mine.sh" } }""");
        var editor = new ClaudeSettingsEditor(files, Windows);
        editor.Add(Folder, Command());

        var outcome = editor.Remove(Folder);

        Assert.True(outcome.Changed);
        Assert.Contains("mine.sh", files.Read(Settings));
        Assert.DoesNotContain(SettingsJsonPatch.WrappedKey, files.Read(Settings));
    }

    [Fact]
    public void Removal_drops_the_copy_it_made()
    {
        var files = new FakeFiles()
            .With(Settings, """{ "statusLine": { "type": "command", "command": "mine.sh" } }""");
        var editor = new ClaudeSettingsEditor(files, Windows);
        editor.Add(Folder, Command());

        editor.Remove(Folder);

        // The copy was insurance while Aiko was in the file, and the readme promises there is
        // nothing left to clean up by hand.
        Assert.False(files.Has(Backup));
    }

    [Fact]
    public void Removal_keeps_what_the_user_changed_after_installing()
    {
        var files = new FakeFiles();
        var editor = new ClaudeSettingsEditor(files, Windows);
        editor.Add(Folder, Command());

        // They edited the file themselves while Aiko was in it.
        var edited = System.Text.Json.Nodes.JsonNode.Parse(files.Read(Settings))!.AsObject();
        edited["model"] = "opus";
        files.With(Settings, edited.ToJsonString());

        editor.Remove(Folder);

        // Restoring the copy wholesale would have thrown this away.
        Assert.Contains("opus", files.Read(Settings));
    }

    [Fact]
    public void Removal_deletes_a_settings_file_Aiko_made_from_nothing()
    {
        // Windows Sandbox: no settings.json before Aiko, and "{}" left behind after it.
        var files = new FakeFiles();
        var editor = new ClaudeSettingsEditor(files, Windows);
        editor.Add(Folder, Command());

        editor.Remove(Folder);

        Assert.False(files.Has(Settings));
    }

    [Fact]
    public void Removal_keeps_a_file_Aiko_made_once_something_else_was_written_to_it()
    {
        var files = new FakeFiles();
        var editor = new ClaudeSettingsEditor(files, Windows);
        editor.Add(Folder, Command());

        var edited = System.Text.Json.Nodes.JsonNode.Parse(files.Read(Settings))!.AsObject();
        edited["model"] = "opus";
        files.With(Settings, edited.ToJsonString());

        editor.Remove(Folder);

        Assert.True(files.Has(Settings));
        Assert.Contains("opus", files.Read(Settings));
    }

    [Fact]
    public void Removal_keeps_an_empty_file_that_was_there_before_Aiko()
    {
        // It was the user's file, even if it said nothing. A copy was made of it, and that copy is
        // what tells the two cases apart.
        var files = new FakeFiles().With(Settings, "{}");
        var editor = new ClaudeSettingsEditor(files, Windows);
        editor.Add(Folder, Command());

        editor.Remove(Folder);

        Assert.True(files.Has(Settings));
    }

    [Fact]
    public void Removal_from_a_folder_Aiko_never_touched_changes_nothing()
    {
        var theirs = """{ "model": "sonnet" }""";
        var files = new FakeFiles().With(Settings, theirs);

        var outcome = new ClaudeSettingsEditor(files, Windows).Remove(Folder);

        Assert.False(outcome.Changed);
        Assert.Equal(theirs, files.Read(Settings));
        Assert.Empty(files.Writes);
    }

    [Fact]
    public void Removal_from_an_empty_folder_changes_nothing()
    {
        var files = new FakeFiles();

        var outcome = new ClaudeSettingsEditor(files, Windows).Remove(Folder);

        Assert.False(outcome.Changed);
        Assert.False(files.Has(Settings));
    }

    [Fact]
    public void A_file_Aiko_may_not_touch_is_reported_and_not_lost()
    {
        var files = new FakeFiles().With(Settings, """{ "model": "opus" }""");
        files.Unreadable.Add(Settings);

        var outcome = new ClaudeSettingsEditor(files, Windows).Add(Folder, Command());

        Assert.False(outcome.Changed);
        Assert.NotEqual(PatchProblem.None, outcome.Problem);
        Assert.Empty(files.Writes);
    }

    [Fact]
    public void Without_a_bridge_path_nothing_is_written()
    {
        var files = new FakeFiles();

        var outcome = new ClaudeSettingsEditor(files, Windows).Add(Folder, string.Empty);

        Assert.False(outcome.Changed);
        Assert.NotEqual(PatchProblem.None, outcome.Problem);
        Assert.Empty(files.Writes);
    }

    [Fact]
    public void Repair_fixes_our_line_after_a_reinstall_elsewhere()
    {
        var files = new FakeFiles();
        var editor = new ClaudeSettingsEditor(files, Windows);
        editor.Add(Folder, Command());

        var outcome = editor.RepairIfOurs(Folder, Command(Moved));

        Assert.True(outcome.Changed);
        Assert.Contains(Moved.Replace(@"\", @"\\"), files.Read(Settings));
    }

    [Fact]
    public void Repair_never_installs_the_line_for_somebody_who_said_no()
    {
        // The wizard's "not now" is an answer, and startup is not the place to overrule it.
        var files = new FakeFiles().With(Settings, """{ "model": "opus" }""");

        var outcome = new ClaudeSettingsEditor(files, Windows).RepairIfOurs(Folder, Command());

        Assert.False(outcome.Changed);
        Assert.Empty(files.Writes);
        Assert.DoesNotContain("statusLine", files.Read(Settings));
    }

    [Fact]
    public void Repair_leaves_somebody_elses_line_alone()
    {
        var theirs = """{ "statusLine": { "type": "command", "command": "mine.sh" } }""";
        var files = new FakeFiles().With(Settings, theirs);

        var outcome = new ClaudeSettingsEditor(files, Windows).RepairIfOurs(Folder, Command());

        Assert.False(outcome.Changed);
        Assert.Equal(theirs, files.Read(Settings));
    }

    [Fact]
    public void Repair_with_nothing_to_fix_writes_nothing()
    {
        var files = new FakeFiles();
        var editor = new ClaudeSettingsEditor(files, Windows);
        editor.Add(Folder, Command());
        files.Writes.Clear();

        var outcome = editor.RepairIfOurs(Folder, Command());

        Assert.False(outcome.Changed);
        Assert.Empty(files.Writes);
    }
}
