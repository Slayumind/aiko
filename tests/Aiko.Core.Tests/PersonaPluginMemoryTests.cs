using System.Text.Json;
using Aiko.Core;

namespace Aiko.Core.Tests;

/// What the persona plugin gains when the memory is on in its environment: two hooks at the start
/// of a session, one beside every message, and the server with the memory tools.
public class PersonaPluginMemoryTests
{
    private const string Bridge = @"C:\Users\Саша Иванов\AppData\Local\Slayumind.Aiko\current\Aiko.Bridge.exe";

    private static IReadOnlyDictionary<string, string> WithMemory => PersonaPlugin.Files(Temperament.Normal, Bridge, memory: true);

    private static JsonElement Hooks(IReadOnlyDictionary<string, string> files) =>
        JsonDocument.Parse(files["hooks/hooks.json"]).RootElement.GetProperty("hooks");

    private static string[] Args(JsonElement hook) =>
        hook.GetProperty("args").EnumerateArray().Select(a => a.GetString()!).ToArray();

    [Fact]
    public void Without_the_memory_the_plugin_is_what_it_was()
    {
        var files = PersonaPlugin.Files(Temperament.Normal, Bridge, memory: false);

        Assert.Equal(PersonaPlugin.Files(Temperament.Normal, Bridge), files);
        Assert.False(files.ContainsKey(".mcp.json"));
        Assert.False(Hooks(files).TryGetProperty("SessionStart", out _));
    }

    [Fact]
    public void With_the_memory_the_plugin_brings_a_server_file()
    {
        Assert.Equal(
            [".claude-plugin/plugin.json", ".mcp.json", "hooks/hooks.json", "output-styles/aiko.md"],
            WithMemory.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_server_is_the_bridge_in_its_serve_mode()
    {
        using var servers = JsonDocument.Parse(WithMemory[".mcp.json"]);
        var server = servers.RootElement.GetProperty("mcpServers").GetProperty(PersonaPlugin.MemoryServer);

        Assert.Equal(Bridge, server.GetProperty("command").GetString());
        Assert.Equal(["memory", "serve"], Args(server));
        Assert.Equal(MemoryBridgeMode.Serve, MemoryBridge.ModeFor(Args(server)));
    }

    [Fact]
    public void The_rules_and_the_facts_are_two_hooks_at_the_start_of_a_session()
    {
        var start = Hooks(WithMemory).GetProperty("SessionStart")[0].GetProperty("hooks");

        Assert.Equal(2, start.GetArrayLength());
        Assert.Equal(MemoryBridgeMode.Rules, MemoryBridge.ModeFor(Args(start[0])));
        Assert.Equal(MemoryBridgeMode.Facts, MemoryBridge.ModeFor(Args(start[1])));
    }

    [Fact]
    public void A_message_runs_the_face_hook_and_then_the_memory_hook()
    {
        var prompt = Hooks(WithMemory).GetProperty("UserPromptSubmit")[0].GetProperty("hooks");

        Assert.Equal(2, prompt.GetArrayLength());
        Assert.Equal(["hook"], Args(prompt[0]));
        Assert.Equal(MemoryBridgeMode.Prompt, MemoryBridge.ModeFor(Args(prompt[1])));
    }

    [Fact]
    public void Memory_hooks_are_waited_for_because_the_model_reads_their_output()
    {
        var hooks = Hooks(WithMemory);
        var memoryHooks = hooks.GetProperty("SessionStart")[0].GetProperty("hooks").EnumerateArray()
            .Append(hooks.GetProperty("UserPromptSubmit")[0].GetProperty("hooks")[1]);

        Assert.All(memoryHooks, hook =>
        {
            Assert.False(hook.TryGetProperty("async", out _));
            Assert.Equal("command", hook.GetProperty("type").GetString());
            Assert.Equal(Bridge, hook.GetProperty("command").GetString());
        });
    }

    [Fact]
    public void The_other_events_keep_only_the_face_hook()
    {
        var hooks = Hooks(WithMemory);

        Assert.All(
            PersonaPlugin.HookEvents.Where(name => name != "UserPromptSubmit"),
            name => Assert.Equal(1, hooks.GetProperty(name)[0].GetProperty("hooks").GetArrayLength()));
    }

    [Fact]
    public void Turning_the_memory_on_changes_the_hash_so_the_plugin_is_updated()
    {
        var off = PersonaPlugin.ContentHash(PersonaPlugin.Files(Temperament.Normal, Bridge));

        Assert.NotEqual(off, PersonaPlugin.ContentHash(WithMemory));
    }

    [Fact]
    public void The_permission_names_the_server_the_way_claude_code_names_plugin_tools()
    {
        Assert.Equal("mcp__plugin_aiko-persona_memory", PersonaPlugin.MemoryPermission);
    }

    [Fact]
    public void The_plugin_has_the_memory_only_for_a_folder_of_an_environment_with_it()
    {
        var withMemory = new AikoEnvironment("Aiko", [@"C:\Users\someone\.claude"]) { Persona = true, Memory = true };
        var personaOff = new AikoEnvironment("Work", [@"C:\Users\someone\.claude-work"]) { Memory = true };
        var settings = new EnvironmentSettings([withMemory, personaOff]);

        Assert.True(PersonaPluginOutput.HasMemory(settings, @"C:\Users\someone\.claude"));
        Assert.True(PersonaPluginOutput.HasMemory(settings, @"c:\users\someone\.claude\"));
        Assert.False(PersonaPluginOutput.HasMemory(settings, @"C:\Users\someone\.claude-work"));
        Assert.False(PersonaPluginOutput.HasMemory(settings, @"C:\Users\someone\.claude-other"));
        Assert.False(PersonaPluginOutput.HasMemory(settings, null));
        Assert.False(PersonaPluginOutput.HasMemory(EnvironmentSettings.Empty, @"C:\Users\someone\.claude"));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("0123456789ab", false)]
    [InlineData("0123456789ab\n", false)]
    [InlineData("ba9876543210", true)]
    public void The_plugin_is_updated_when_its_content_is_not_the_one_noted(string? synced, bool needsUpdate)
    {
        Assert.Equal(needsUpdate, PersonaPluginOutput.NeedsUpdate(synced, "0123456789ab"));
    }

    [Fact]
    public void The_note_sits_beside_the_versions_and_is_not_taken_for_one()
    {
        var folders = AikoFolders.Windows(@"C:\Users\someone\AppData\Roaming", @"C:\Users\someone\AppData\Local");
        var note = PersonaPluginOutput.SyncedFile(folders, @"C:\Users\someone\.claude");

        Assert.Equal(PersonaPluginOutput.EnvironmentFolder(folders, @"C:\Users\someone\.claude"), Path.GetDirectoryName(note));
        Assert.Empty(PersonaPluginOutput.StaleFolders(
            [(Path.GetFileName(note), DateTimeOffset.MinValue)], "0123456789ab", DateTimeOffset.UtcNow));
    }
}
