namespace Aiko.Core;

/// Aiko's own skills, in the order the settings page lists them. All of them ship inside one plugin,
/// so Claude Code shows each one as aiko:name (D-234).
public static class SkillCatalog
{
    /// The plugin that holds every skill.
    public const string PluginName = "aiko";

    public static IReadOnlyList<string> All { get; } =
    [
        "copy",
        "release-gate",
        "docs-hygiene",
        "playtest",
        "blender-to-unity",
        "texturing",
        "glb-for-web",
        "palette",
        "gamedesign-research",
        "calendar",
        "drive",
    ];

    /// What the person types to call the skill. The short /name may belong to Claude Code itself or
    /// to a skill of the person's own; the full name always reaches this one.
    public static string Call(string skill) => $"/{PluginName}:{skill}";
}
